using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static BetterSongSearch.UI.DownloadHistoryView;

namespace BetterSongSearch.Util
{
    internal static class RyuunosukeDownloader
    {
        private static HttpClient client = null;

        private static void InitClientIfNecessary()
        {
            if (client != null)
            {
                return;
            }

            client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip,
                AllowAutoRedirect = true,
                //Proxy = new WebProxy("localhost:8888")
            });

            client.DefaultRequestHeaders.Add("User-Agent", "BetterSongSearch/" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            client.Timeout = TimeSpan.FromSeconds(10);
        }

        public static async Task<string> GetSongDescription(string key, CancellationToken token)
        {
            InitClientIfNecessary();

            // TODO: Use https://ryuunosuke.com/api/v1/beatmaps/
            using HttpResponseMessage resp = await client.GetAsync($"https://api.beatsaver.com/maps/id/{key.ToLowerInvariant()}", HttpCompletionOption.ResponseHeadersRead, token);
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Unexpected HTTP response: {resp.StatusCode} {resp.ReasonPhrase}");
            }

            using StreamReader reader = new StreamReader(await resp.Content.ReadAsStreamAsync());
            using JsonTextReader jsonReader = new JsonTextReader(reader);
            JsonSerializer ser = new JsonSerializer();

            return ser.Deserialize<JObject>(jsonReader).GetValue("description").Value<string>();
        }

        public static async Task BeatmapDownload(DownloadHistoryEntry entry, CancellationToken token, Action<float> progressCb)
        {
            InitClientIfNecessary();

            string folderName = $"{entry.key} ({entry.songName} - {entry.levelAuthorName}) [ryuunosuke.moe]";

            Client dl = new Client(client, $"https://cdn.ryuunosuke.moe/beatmaps/{entry.key}/{entry.hash}/{entry.hash}.zip".ToLowerInvariant(), (p) =>
            {
                entry.status = DownloadHistoryEntry.DownloadStatus.Downloading;
                progressCb(p);
            });
            byte[] res;
            CancellationTokenSource t = new CancellationTokenSource();
            token.Register(t.Cancel);
            try
            {
                res = await dl.Load(t.Token);
            }
            catch (Exception ex)
            {
                t.Cancel();
                throw ex;
            }

            using MemoryStream s = new MemoryStream(res);
            entry.status = DownloadHistoryEntry.DownloadStatus.Extracting;
            progressCb(0);

            // Not async'ing this as BeatmapDownload() is supposed to be called in a task
            ExtractZip(s, folderName, t.Token, progressCb);
        }

        private static void ExtractZip(Stream zipStream, string basePath, CancellationToken token, Action<float> progressCb, bool overwrite = false)
        {
            string path = Path.Combine(CustomLevelPathHelper.customLevelsDirectoryPath, string.Concat(basePath.Split(Path.GetInvalidFileNameChars())).Trim());

            if (!overwrite && Directory.Exists(path))
            {
                int pathNum = 1;
                while (Directory.Exists(path + $" ({pathNum})"))
                {
                    pathNum++;
                }

                path += $" ({pathNum})";
            }

            int steps;
            int progress = 0;
            Dictionary<string, byte[]> files;

            // Unzip everything to memory first so we dont end up writing half a song incase something breaks
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                byte[] buf = new byte[2 ^ 15];

                using MemoryStream ms = new MemoryStream();
                steps = archive.Entries.Count() * 2;
                files = new Dictionary<string, byte[]>();

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Dont extract directories / sub-files
                    if (!entry.FullName.Contains("/"))
                    {
                        using Stream str = entry.Open();
                        for (; ; )
                        {
                            if (token.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }

                            int read = str.Read(buf, 0, buf.Length);
                            if (read == 0)
                            {
                                break;
                            }

                            ms.Write(buf, 0, read);
                        }

                        files.Add(entry.Name, ms.ToArray());
                    }
                    else
                    {
                        // As this wont extract anthing further down we need to increase the process for it in advance
                        progress++;
                    }

                    progressCb((float)++progress / steps);
                    ms.SetLength(0);
                }
            }

            // Failsafe so we dont break songcore. Info.dat, a diff and the song itself - not sure if the cover is needed
            if (files.Count < 3 || !files.Keys.Any(x => x.ToLowerInvariant() == "info.dat"))
            {
                throw new InvalidDataException();
            }

            if (token.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (KeyValuePair<string, byte[]> e in files)
            {
                string entryPath = Path.Combine(path, e.Key);
                if (overwrite || !File.Exists(entryPath))
                {
                    File.WriteAllBytes(entryPath, e.Value);
                }

                progressCb((float)++progress / steps);

                // Dont think cancelling here is smart, might as well finish writing this song to not have a corrupted download
                // if(token.IsCancellationRequested)
                //     throw new TaskCanceledException();
            }
        }
    }
}
