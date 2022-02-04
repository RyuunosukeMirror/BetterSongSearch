using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BetterSongSearch.UI.CustomLists;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace BetterSongSearch.UI
{
    [HotReload(RelativePathToLayout = @"Views\DownloadHistory.bsml")]
    [ViewDefinition("BetterSongSearch.UI.Views.DownloadHistory.bsml")]
    internal class DownloadHistoryView : BSMLAutomaticViewController, TableView.IDataSource
    {
        [UIComponent("scrollBarContainer")] private readonly VerticalLayoutGroup _scrollBarContainer = null;
        [UIComponent("downloadList")] private readonly CustomListTableData downloadHistoryData = null;

        private TableView downloadHistoryTable => downloadHistoryData?.tableView;
        public readonly List<DownloadHistoryEntry> downloadList = new List<DownloadHistoryEntry>();
        private DownloadHistoryEntry[] downloadListSorted = null;

        public bool hasUnloadedDownloads => downloadList.Any(x => x.status == DownloadHistoryEntry.DownloadStatus.Downloaded);

        private const int RETRY_COUNT = 3;
        private const int MAX_PARALLEL_DOWNLOADS = 2; // Might add this as a menu option

        public bool TryAddDownload(SongSearchSong song, bool isBatch = false)
        {
            DownloadHistoryEntry existingDLHistoryEntry = downloadList.FirstOrDefault(x => x.key == song.detailsSong.key);

            existingDLHistoryEntry?.ResetIfFailed();

            if (!song.CheckIsDownloadable())
            {
                return false;
            }

            if (existingDLHistoryEntry == null)
            {
                //var newPos = downloadList.FindLastIndex(x => x.status > DownloadHistoryEntry.DownloadStatus.Queued);
                downloadList.Add(new DownloadHistoryEntry(song));
                downloadHistoryTable.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
            }
            else
            {
                existingDLHistoryEntry.status = DownloadHistoryEntry.DownloadStatus.Queued;
            }

            ProcessDownloads(!isBatch);

            return true;
        }

        private void SelectSong(TableView _, int idx)
        {
            if (BSSFlowCoordinator.songDetails.songs.FindByMapId(downloadListSorted[idx].key, out SongDetailsCache.Structs.Song song))
            {
                BSSFlowCoordinator.songListView.selectedSongView.SetSelectedSong(BSSFlowCoordinator.songsList[song.index]);
                BSSFlowCoordinator.songListView.songList.ClearSelection();
            }
        }

        public async void ProcessDownloads(bool forceTableReload = false)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (downloadList.Count(x => x.IsInAnyOfStates(DownloadHistoryEntry.DownloadStatus.Preparing | DownloadHistoryEntry.DownloadStatus.Downloading)) >= MAX_PARALLEL_DOWNLOADS)
            {
                if (forceTableReload)
                {
                    RefreshTable(true);
                }

                return;
            }

            DownloadHistoryEntry firstEntry = downloadList
                .Where(x => x.retries < RETRY_COUNT && x.IsInAnyOfStates(DownloadHistoryEntry.DownloadStatus.Failed | DownloadHistoryEntry.DownloadStatus.Queued))
                .OrderBy(x => x.orderValue)
                .FirstOrDefault();

            if (firstEntry == null)
            {
                RefreshTable(forceTableReload);
                return;
            }

            if (firstEntry.status == DownloadHistoryEntry.DownloadStatus.Failed)
            {
                firstEntry.retries++;
            }

            firstEntry.downloadProgress = 0f;
            firstEntry.status = DownloadHistoryEntry.DownloadStatus.Preparing;

            RefreshTable(true);

            await Task.Run(async () =>
            {
                try
                {
                    Stopwatch updateRateLimiter = new Stopwatch();
                    updateRateLimiter.Start();

                    await RyuunosukeDownloader.BeatmapDownload(firstEntry, BSSFlowCoordinator.closeCancelSource.Token, (float progress) =>
                    {
                        if (updateRateLimiter.ElapsedMilliseconds < 50)
                        {
                            return;
                        }

                        firstEntry.statusDetails = string.Format("({0:0%}{1})", progress, firstEntry.retries == 0 ? "" : $", retry #{firstEntry.retries} / {RETRY_COUNT}");
                        firstEntry.downloadProgress = progress;

                        updateRateLimiter.Restart();

                        if (firstEntry.UpdateProgressHandler != null)
                        {
                            IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(firstEntry.UpdateProgressHandler);
                        }
                    });

                    firstEntry.downloadProgress = 1f;
                    firstEntry.status = DownloadHistoryEntry.DownloadStatus.Downloaded;
                    firstEntry.statusDetails = "";
                }
                catch (Exception ex)
                {
                    if (!(ex is TaskCanceledException))
                    {
                        Plugin.Log.Warn("Download failed:");
                        Plugin.Log.Warn(ex);
                    }

                    firstEntry.status = DownloadHistoryEntry.DownloadStatus.Failed;
                    firstEntry.statusDetails = $"{(firstEntry.retries < 3 ? "(Will retry)" : "")}: {ex.Message}";
                }
            });

            if (firstEntry.status == DownloadHistoryEntry.DownloadStatus.Downloaded)
            {
                // NESTING HELLLL
                SelectedSongView selectedSongView = BSSFlowCoordinator.songListView.selectedSongView;
                if (selectedSongView.selectedSong.detailsSong.key == firstEntry.key)
                {
                    selectedSongView.SetIsDownloaded(true);
                }

                BSSFlowCoordinator.songListView.songList.RefreshCells(false, true);
            }

            ProcessDownloads(true);
        }

        private RatelimitCoroutine limitedFullTableReload;

        public void RefreshTable(bool fullReload = true)
        {
            downloadListSorted = downloadList.OrderBy(x => x.orderValue).ToArray();
            SharedCoroutineStarter.instance.StartCoroutine(limitedFullTableReload.Call());
        }


        [UIAction("#post-parse")]
        private void Parsed()
        {
            limitedFullTableReload = new RatelimitCoroutine(downloadHistoryTable.ReloadData, 0.1f);
            downloadHistoryTable.SetDataSource(this, false);

            ReflectionUtil.SetField(downloadHistoryTable, "_canSelectSelectedCell", true);

            BSMLStuff.GetScrollbarForTable(downloadHistoryData.gameObject, _scrollBarContainer.transform);
        }

        public float CellSize()
        {
            return 8.05f;
        }

        public int NumberOfCells()
        {
            return downloadList?.Count ?? 0;
        }

        public TableCell CellForIdx(TableView tableView, int idx)
        {
            return DownloadListTableData.GetCell(tableView).PopulateWithSongData(downloadListSorted[idx]);
        }

        public class DownloadHistoryEntry
        {
            [Flags]
            public enum DownloadStatus : byte
            {
                Downloading = 1,
                Preparing = 2,
                Extracting = 4,
                Queued = 8,
                Failed = 16,
                Downloaded = 32,
                Loaded = 64
            }

            public bool isDownloading => status == DownloadStatus.Downloading || status == DownloadStatus.Preparing || status == DownloadStatus.Extracting;
            public bool isQueued => status == DownloadStatus.Queued || (status == DownloadStatus.Failed && retries < RETRY_COUNT);

            public DownloadStatus status = DownloadStatus.Queued;
            public string statusMessage => $"{status} {statusDetails}";
            public string statusDetails = "";
            public float downloadProgress = 1f;

            public int retries = 0;

            public readonly string songName;
            public readonly string levelAuthorName;
            public readonly string key;
            public readonly string hash;

            public int orderValue => ((int)status * 100) + retries;

            public bool IsInAnyOfStates(DownloadStatus states)
            {
                return (status & states) != 0;
            }

            public void ResetIfFailed()
            {
                if (status != DownloadStatus.Failed || retries < RETRY_COUNT)
                {
                    return;
                }

                status = DownloadStatus.Queued;
                retries = 0;
            }

            public DownloadHistoryEntry(SongSearchSong song)
            {
                songName = song.detailsSong.songName;
                levelAuthorName = song.detailsSong.levelAuthorName;
                key = song.detailsSong.key.ToLowerInvariant();
                hash = song.detailsSong.hash.ToLowerInvariant();
            }

            public Action UpdateProgressHandler;
        }
    }
}
