using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;

namespace BetterSongSearch.UI.SplitViews
{
    internal class MultiDl
    {
        public static readonly MultiDl instance = new MultiDl();

        private MultiDl() { }

        [UIComponent("multiDlCountSlider")] private readonly SliderSetting multiDlCountSlider = null;
        [UIAction("StartMultiDownload")]
        private void StartMultiDownload()
        {
            for (int i = BSSFlowCoordinator.songListView.songList.GetVisibleCellsIdRange().Item1, downloaded = 0; i < SongListController.searchedSongsList.Count; i++)
            {
                if (SongListController.searchedSongsList[i].CheckIsDownloaded() || !SongListController.searchedSongsList[i].CheckIsDownloadable())
                {
                    continue;
                }

                BSSFlowCoordinator.downloadHistoryView.TryAddDownload(SongListController.searchedSongsList[i], true);

                if (++downloaded >= multiDlCountSlider.Value)
                {
                    break;
                }
            }

            BSSFlowCoordinator.downloadHistoryView.RefreshTable(true);
        }
    }
}
