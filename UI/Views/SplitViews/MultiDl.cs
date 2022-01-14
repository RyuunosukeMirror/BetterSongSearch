﻿using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterSongSearch.UI.SplitViews {
	class MultiDl {
		public static readonly MultiDl instance = new MultiDl();
		MultiDl() { }

		[UIComponent("multiDlCountSlider")] SliderSetting multiDlCountSlider = null;
		[UIAction("StartMultiDownload")]
		void StartMultiDownload() {
			for(int i = BSSFlowCoordinator.songListView.songList.GetVisibleCellsIdRange().Item1, downloaded = 0; i < SongListController.searchedSongsList.Count; i++) {
				if(SongListController.searchedSongsList[i].CheckIsDownloaded() || !SongListController.searchedSongsList[i].CheckIsDownloadable())
					continue;

				BSSFlowCoordinator.downloadHistoryView.TryAddDownload(SongListController.searchedSongsList[i], true);

				if(++downloaded >= multiDlCountSlider.Value)
					break;
			}

			BSSFlowCoordinator.downloadHistoryView.RefreshTable(true);
		}
	}
}