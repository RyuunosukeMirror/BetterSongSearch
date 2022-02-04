using BetterSongSearch.UI;
using HarmonyLib;
using HMUI;

namespace BetterSongSearch.HarmonyPatches
{
    [HarmonyPatch(typeof(FlowCoordinator), "DismissFlowCoordinator")]
    internal static class ReturnToBSS
    {
        public static bool returnTobss = false;

        private static void Prefix(FlowCoordinator flowCoordinator, ref bool immediately)
        {
            if (!returnTobss)
            {
                return;
            }

            if (!(flowCoordinator is SoloFreePlayFlowCoordinator))
            {
                returnTobss = false;
                return;
            }

            immediately = true;
        }

        private static void Postfix()
        {
            if (!returnTobss)
            {
                return;
            }

            returnTobss = false;

            Manager.ShowFlow(true);
        }
    }
}
