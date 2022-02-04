using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System.Reflection;
using TMPro;
using UnityEngine;
using static BetterSongSearch.UI.DownloadHistoryView;
using static BetterSongSearch.UI.DownloadHistoryView.DownloadHistoryEntry;

namespace BetterSongSearch.UI.CustomLists
{
    internal static class DownloadListTableData
    {
        private const string ReuseIdentifier = "REUSECustomDownloadListTableCell";

        public static CustomDownloadListTableCell GetCell(TableView tableView)
        {
            TableCell tableCell = tableView.DequeueReusableCellForIdentifier(ReuseIdentifier);

            if (tableCell == null)
            {
                tableCell = new GameObject("CustomDownloadListTableCell", typeof(Touchable)).AddComponent<CustomDownloadListTableCell>();
                tableCell.interactable = true;

                tableCell.reuseIdentifier = ReuseIdentifier;
                BSMLParser.instance.Parse(
                    Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BetterSongSearch.UI.CustomLists.DownloadListCell.bsml"),
                    tableCell.gameObject, tableCell
                );
            }

            return (CustomDownloadListTableCell)tableCell;
        }
    }

    internal class CustomDownloadListTableCell : TableCell
    {
        [UIComponent("songName")] private readonly TextMeshProUGUI songName = null;
        [UIComponent("levelAuthorName")] private readonly TextMeshProUGUI levelAuthorName = null;
        [UIComponent("statusLabel")] private readonly TextMeshProUGUI statusLabel = null;
        private DownloadHistoryEntry entry = null;

        public CustomDownloadListTableCell PopulateWithSongData(DownloadHistoryEntry entry)
        {
            songName.text = entry.songName;
            levelAuthorName.text = entry.levelAuthorName;
            statusLabel.text = entry.statusMessage;
            this.entry = entry;
            entry.UpdateProgressHandler = UpdateProgress;

            UpdateProgress();

            return this;
        }

        protected override void SelectionDidChange(TransitionType transitionType)
        {
            RefreshBgState();
        }

        protected override void HighlightDidChange(TransitionType transitionType)
        {
            RefreshBgState();
        }

        protected override void WasPreparedForReuse()
        {
            entry.UpdateProgressHandler = null;
        }

        [UIComponent("bgContainer")] private readonly ImageView bg = null;
        [UIComponent("bgProgress")] private readonly ImageView bgProgress = null;
        [UIAction("refresh-visuals")]
        public void RefreshBgState()
        {
            bg.color = new Color(0, 0, 0, highlighted ? 0.8f : 0.45f);

            RefreshBar();
        }

        private void RefreshBar()
        {
            if (entry == null)
            {
                return;
            }

            Color clr = entry.status == DownloadStatus.Failed ? Color.red : entry.status != DownloadStatus.Queued ? Color.green : Color.gray;

            clr.a = 0.5f + (entry.downloadProgress * 0.4f);
            bgProgress.color = clr;

            RectTransform x = (bgProgress.gameObject.transform as RectTransform);
            if (x == null)
            {
                return;
            }

            x.anchorMax = new Vector2(entry.downloadProgress, 1);
            x.ForceUpdateRectTransforms();
        }

        public void UpdateProgress()
        {
            statusLabel.text = entry.statusMessage;

            RefreshBar();
        }
    }
}
