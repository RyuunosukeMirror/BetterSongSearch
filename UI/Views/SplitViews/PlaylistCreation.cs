using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberPlaylistsLib;
using BeatSaberPlaylistsLib.Types;
using IPA.Utilities;
using System;
using System.IO;
using TMPro;

namespace BetterSongSearch.UI.SplitViews
{
    internal class PlaylistCreation
    {
        public static readonly PlaylistCreation instance = new PlaylistCreation();

        private PlaylistCreation() { }

        [UIComponent("playlistSongsCountSlider")] private readonly SliderSetting playlistSongsCountSlider = null;
        [UIComponent("playlistName")] private readonly StringSetting playlistName = null;
        [UIComponent("resultText")] private readonly TextMeshProUGUI resultText = null;

        [UIParams] private readonly BSMLParserParams parserParams = null;

        [UIAction("#post-parse")]
        private void Parsed()
        {
            ReflectionUtil.SetField(playlistName.modalKeyboard.modalView, "_animateParentCanvas", false);
        }

        internal static string nameToUseOnNextOpen = "h";
        private static readonly bool clearExisting = true;

        public void Open()
        {
            if (IPA.Loader.PluginManager.GetPluginFromId("BeatSaberPlaylistsLib") == null)
            {
                resultText.text = "You dont have 'BeatSaberPlaylistsLib' installed which is required to create Playlists. You can get it in ModAssistant.";
                parserParams.EmitEvent("ShowResultModal");
                return;
            }

            if (nameToUseOnNextOpen != null)
            {
                playlistName.Text = nameToUseOnNextOpen;
                nameToUseOnNextOpen = null;
            }

            parserParams.EmitEvent("ShowModal");
        }

        private void ShowResult(string text)
        {
            resultText.text = text;
            parserParams.EmitEvent("CloseModal");
            parserParams.EmitEvent("ShowResultModal");
        }

        private void CreatePlaylist()
        {
            string fName = string.Concat(playlistName.Text.Split(Path.GetInvalidFileNameChars())).Trim();

            if (fName.Length == 0)
            {
                ShowResult($"Your Playlist name is invalid");
                return;
            }

            try
            {
                PlaylistManager manager = PlaylistManager.DefaultManager.CreateChildManager("BetterSongSearch");

                if (!manager.TryGetPlaylist(fName, out BeatSaberPlaylistsLib.Types.IPlaylist plist))
                {
                    plist = manager.CreatePlaylist(
                        fName,
                        playlistName.Text,
                        "BetterSongSearch",
                        ""
                    );
                }

                if (clearExisting)
                {
                    plist.Clear();
                }

                plist.SetCustomData("BetterSongSearchFilter", FilterView.currentFilter.Serialize(Newtonsoft.Json.Formatting.None));
                plist.SetCustomData("BetterSongSearchSearchTerm", BSSFlowCoordinator.songListView.songSearchInput.text);
                plist.SetCustomData("BetterSongSearchSort", SongListController.selectedSortMode);
                plist.AllowDuplicates = false;

                int addedSongs = 0;

                for (int i = 0; i < SongListController.searchedSongsList.Count; i++)
                {
                    if (addedSongs >= playlistSongsCountSlider.Value)
                    {
                        break;
                    }

                    SongSearchSong s = SongListController.searchedSongsList[i];

                    PlaylistSong pls = (PlaylistSong)plist.Add(s.hash, s.detailsSong.songName, s.detailsSong.key, s.detailsSong.levelAuthorName);

                    if (pls == null)
                    {
                        continue;
                    }

                    foreach (SongSearchSong.SongSearchDiff x in s.diffs)
                    {
                        if (!x.passesFilter)
                        {
                            continue;
                        }

                        string dchar = x.detailsDiff.characteristic.ToString();

                        if (dchar == "ThreeSixtyDegree")
                        {
                            dchar = "360Degree";
                        }
                        else if (dchar == "NinetyDegree")
                        {
                            dchar = "90Degree";
                        }

                        pls.AddDifficulty(dchar, x.detailsDiff.difficulty.ToString());
                    }

                    addedSongs++;
                }

                manager.StorePlaylist(plist);
                manager.RequestRefresh("BetterSongSearch");

                ShowResult($"Added <b><color=#CCC>{addedSongs}</color></b> Songs to Playlist <b><color=#CCC>{playlistName.Text}</color></b> (Contains {plist.Count} now)");
            }
            catch (Exception ex)
            {
                ShowResult($"Playlist failed to Create: More details in log, {ex.GetType().Name}");
                Plugin.Log.Warn("Failed to create Playlist:");
                Plugin.Log.Warn(ex);
            }
        }
    }
}
