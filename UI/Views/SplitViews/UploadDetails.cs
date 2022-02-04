using BeatSaberMarkupLanguage.Attributes;
using BetterSongSearch.Util;
using HMUI;
using System.Linq;
using System.Threading.Tasks;
using TMPro;

namespace BetterSongSearch.UI.SplitViews
{
    internal class UploadDetails
    {
        public static readonly UploadDetails instance = new UploadDetails();

        private UploadDetails() { }

        [UIComponent("selectedCharacteristics")] private readonly TextMeshProUGUI selectedCharacteristics = null;
        [UIComponent("selectedSongKey")] private readonly TextMeshProUGUI selectedSongKey = null;
        [UIComponent("selectedSongDescription")] private readonly CurvedTextMeshPro selectedSongDescription = null;
        [UIComponent("selectedRating")] private readonly TextMeshProUGUI selectedRating = null;
        //[UIComponent("selectedDownloadCount")] TextMeshProUGUI selectedDownloadCount = null;
        [UIComponent("songDetailsLoading")] private readonly ImageView songDetailsLoading = null;

        public void Populate(SongSearchSong selectedSong)
        {
            selectedCharacteristics.text = string.Join(", ", selectedSong.detailsSong.difficulties.GroupBy(x => x.characteristic).Select(x => $"{x.Count()}x {x.Key}"));
            selectedSongKey.text = selectedSong.detailsSong.key;
            //selectedDownloadCount.text = selectedSong.detailsSong.downloadCount.ToString("N0");
            selectedRating.text = selectedSong.detailsSong.rating.ToString("0.0%");
            selectedSongDescription.text = "";

            songDetailsLoading.gameObject.SetActive(true);

            Task.Run(async () =>
            {
                string desc = "Failed to load description";
                try
                {
                    desc = await RyuunosukeDownloader.GetSongDescription(selectedSong.detailsSong.key, BSSFlowCoordinator.closeCancelSource.Token);
                }
                catch { }

                _ = IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    songDetailsLoading.gameObject.SetActive(false);
                    // If we dont do that, the description is long and contains unicode the game crashes. Fun.
                    selectedSongDescription.text = desc;
                    selectedSongDescription.gameObject.SetActive(false);
                    selectedSongDescription.gameObject.SetActive(true);
                });
            }).ConfigureAwait(false);
        }
    }
}
