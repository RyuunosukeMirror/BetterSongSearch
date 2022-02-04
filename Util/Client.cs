using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BetterSongSearch.Util
{
    internal class Client
    {
#if DEBUG
        private readonly bool MAKE_SLOW = true;
#endif

        private const int BATCHSIZE = 1048576;
        private const int MAX_CONNECTIONS = 4;
        private readonly HttpClient client;
        private readonly string url;
        private readonly Action<float> progressCb;

        public Client(HttpClient client, string url, Action<float> progressCb)
        {
            this.client = client;
            this.url = url;
            this.progressCb = progressCb;
        }

        private const bool doMultiDl = true; // Ryuunosuke supports multiple connections!!! 🎉
        private int downloadSize = 0;
        private float downloadSizeInverse = 0;
        private int downloadedBytes = 0;
        private float progress = 0f;
        private byte[] fileOut;
        private CancellationToken token;

        private void Reset()
        {
            progress = 0f;
            downloadedBytes = 0;
        }

        private void AddDownloadedBytes(int bytes)
        {
            downloadedBytes += bytes;

            float newProgress = downloadedBytes * downloadSizeInverse;

            if (newProgress - progress > 0.01f)
            {
                progress = newProgress;

                progressCb(progress);
            }
        }

        private async Task<Task> DownloadRange(int start, int length)
        {
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
            if (length != 0)
            {
                req.Headers.Add("range", $"bytes={start}-{start + length - 1}");
            }

            Stream stream = null;
            HttpResponseMessage resp = null;

            void cleanup(Exception ex = null)
            {
                Plugin.Log.Debug(string.Format("[{0}-{1}] Cleanup: {2}", start, start + length, ex));

                stream?.Dispose();
                resp?.Dispose();
                req.Dispose();

                if (ex != null)
                {
                    throw ex;
                }
            }

            try
            {
                Plugin.Log.Debug(string.Format("Opening connection for {0}-{1}", start, start + length));
                resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
                Plugin.Log.Debug(string.Format("[{0}-{1}] Opened connection: {2}", start, start + length, resp.StatusCode));

                if ((int)resp.StatusCode == 429 || resp.ReasonPhrase == "Too Many Requests")
                {
                    throw new Exception("Ratelimited, retry later");
                }

                if (resp.StatusCode == HttpStatusCode.NotFound) {

                    throw new Exception("The requested beatmap was not found.");
                }

                if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.PartialContent)
                {
                    throw new Exception($"Unexpected HTTP response: {resp.StatusCode} {resp.ReasonPhrase}");
                }

                int len = (int)(resp.Content.Headers.ContentLength ?? 0);

                if (len == 0)
                {
                    throw new Exception("Response has no length");
                }

                int end = len + start;

                stream = await resp.Content.ReadAsStreamAsync();
                if (fileOut == null && start == 0)
                {
                    downloadSize = len;

                    // If we got a partial response (Requested bytes are less than the total fizesize) get the total fizesize from resp header
                    if (resp.StatusCode == HttpStatusCode.PartialContent)
                    {
                        downloadSize = (int)resp.Content.Headers.ContentRange.Length;
                    }

                    downloadSizeInverse = 1f / downloadSize;

                    fileOut = new byte[downloadSize];

                    //foreach(var x in resp.Headers) {
                    //	if(x.Key.ToLower() == "cf-cache-status") {
                    //		doMultiDl = x.Value.FirstOrDefault() == "HIT";
                    //		break;
                    //	}
                    //}

                    Plugin.Log.Debug(string.Format("downloadSize: {0}, isDownloadingFromCache: {1}", downloadSize, doMultiDl));
                }

                return Task.Run(() =>
                {
                    try
                    {
                        stream.ReadTimeout = 7000;

                        int pos = start;

                        while (pos != end)
                        {
                            if (token.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }

                            int read = stream.Read(fileOut, pos, Math.Min(8192, fileOut.Length - pos));
                            if (read == 0)
                            {
                                break;
                            }

                            pos += read;

                            AddDownloadedBytes(read);

                            if (pos == fileOut.Length)
                            {
                                break;
                            }

#if DEBUG
                            if (!MAKE_SLOW)
                            {
                                continue;
                            }

                            SpinWait x = new SpinWait();
                            for (int i = 0; i < 8; i++)
                            {
                                x.SpinOnce();
                            }
#endif
                        }

                        Plugin.Log.Debug(string.Format("[{0}-{1}] Downloaded {2} bytes ({3} left)", start, start + length, pos, end - pos));

                        if (pos != end)
                        {
                            throw new Exception("Response was incomplete");
                        }
                    }
                    catch (Exception ex)
                    {
                        cleanup(ex);
                    }
                });
            }
            catch
            {
                // Gotta do this manually here, else C# will yell at us because it has no idea cleanup will throw
                cleanup();
                throw;
            }
        }


        public async Task<byte[]> Load(CancellationToken token)
        {
            this.token = token;

            try
            {
                IEnumerable<Task> initialDl = new[] { await DownloadRange(0, doMultiDl ? BATCHSIZE : 0) }.AsEnumerable();

                int leftover = downloadSize - BATCHSIZE;

                if (doMultiDl && leftover > 0)
                {
                    // Chunks should be at least 3M in size
                    int connections = !doMultiDl ? 1 : (int)Math.Floor(Mathf.Clamp(leftover / (BATCHSIZE * 2f), 1, MAX_CONNECTIONS));
                    int bytesPerConnection = (int)Math.Floor((float)leftover / connections);
                    int offs = downloadedBytes;

                    Plugin.Log.Debug(string.Format("Downloading song with {0} connection(s)", connections));

                    // Open connections in parallel
                    List<Task<Task>> connectingRequests = new List<Task<Task>>() { };

                    while (connections > 0)
                    {
                        connections--;

                        int chunkSize = connections == 0 ? downloadSize - offs : bytesPerConnection;

                        Plugin.Log.Debug(string.Format("Chunk {0}, size {1}, start {2}", connections, chunkSize, offs));

                        connectingRequests.Add(DownloadRange(offs, chunkSize));

                        offs += bytesPerConnection;
                    }

                    Plugin.Log.Debug("Waiting for all connections to open...");
                    // Wait for all connections to open
                    await Task.WhenAll(connectingRequests);
                    initialDl = initialDl.Concat(connectingRequests.Select(x => x.Result));
                }

                Plugin.Log.Debug("Waiting for all chunks to download...");
                // Now that all connections exist wait for them to finish downloading their chunk
                await Task.WhenAll(initialDl);

                return fileOut;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
