using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BetterSongSearch.Configuration;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using System.Linq;
using TMPro;
using UnityEngine.UI;

namespace BetterSongSearch.UI.SplitViews
{
    internal class Presets
    {
        public static readonly Presets instance = new Presets();

        private Presets() { }

        private class FilterPresetRow
        {
            public readonly string name;
            [UIComponent("label")] private readonly TextMeshProUGUI label = null;

            public FilterPresetRow(string name)
            {
                this.name = name;
            }

            [UIAction("refresh-visuals")]
            public void Refresh(bool selected, bool highlighted)
            {
                label.color = new UnityEngine.Color(
                    selected ? 0 : 255,
                    selected ? 128 : 255,
                    selected ? 128 : 255,
                    highlighted ? 0.9f : 0.6f
                );
            }
        }

        [UIAction("#post-parse")]
        private void Parsed()
        {
            FilterPresets.Init();

            BSMLStuff.GetScrollbarForTable(presetList.tableView.gameObject, _presetScrollbarContainer.transform);

            // BSML / HMUI my beloved
            ReflectionUtil.SetField(newPresetName.modalKeyboard.modalView, "_animateParentCanvas", false);
        }


        [UIComponent("loadButton")] private readonly NoTransitionsButton loadButton = null;
        [UIComponent("deleteButton")] private readonly NoTransitionsButton deleteButton = null;
        [UIComponent("presetList")] private readonly CustomCellListTableData presetList = null;
        [UIComponent("newPresetName")] private readonly StringSetting newPresetName = null;
        [UIComponent("presetScrollbarContainer")] private readonly VerticalLayoutGroup _presetScrollbarContainer = null;
        internal void ReloadPresets()
        {
            presetList.data = FilterPresets.presets.Select(x => new FilterPresetRow(x.Key)).ToList<object>();
            presetList.tableView.ReloadData();
            presetList.tableView.ClearSelection();

            loadButton.interactable = false;
            deleteButton.interactable = false;

            newPresetName.Text = "";
        }

        private string curSelected;

        private void PresetSelected(object _, FilterPresetRow row)
        {
            loadButton.interactable = true;
            deleteButton.interactable = true;
            newPresetName.Text = curSelected = row.name;
        }

        private void AddPreset()
        {
            FilterPresets.Save(newPresetName.Text);
            ReloadPresets();
        }

        private void LoadPreset()
        {
            PlaylistCreation.nameToUseOnNextOpen = curSelected;

            BSSFlowCoordinator.filterView.SetFilter(FilterPresets.presets[curSelected]);
        }

        private void DeletePreset()
        {
            FilterPresets.Delete(curSelected);
            ReloadPresets();
        }
    }
}
