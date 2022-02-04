using BetterSongSearch.UI;
using HarmonyLib;
using HMUI;
using IPA.Utilities;
using System.Linq;
using UnityEngine;

namespace BetterSongSearch.HarmonyPatches
{
    [HarmonyPatch(typeof(GameplaySetupViewController), nameof(GameplaySetupViewController.RefreshContent))]
    internal static class MPMenuButton
    {
        private static GameObject button = null;

        private static void Postfix(GameplaySetupViewController __instance, bool ____showMultiplayer)
        {
            // I dont want the plugin to ever break because of changes to this
            try
            {
                if (button == null)
                {
                    Transform x = __instance.transform.Find("BSMLBackground/BSMLTabSelector") ?? __instance.transform.Find("TextSegmentedControl");

                    if (x == null)
                    {
                        return;
                    }

                    button = GameObject.Instantiate(x.Cast<Transform>().Last().gameObject, x);

                    TextSegmentedControlCell t = button.GetComponent<TextSegmentedControlCell>();

                    t.text = "ryuunosuke.moe | BSS";
                    ReflectionUtil.SetField<SelectableCell, Signal>(t, "_wasPressedSignal", null);
                    t.selectionDidChangeEvent += (A, B, CBADQ) =>
                    {
                        if (!t.selected)
                        {
                            return;
                        }

                        t.SetSelected(false, SelectableCell.TransitionType.Instant, false, true);
                        Manager.ShowFlow();
                    };
                }

                button.SetActive(____showMultiplayer);
            }
            catch { }
        }
    }
}
