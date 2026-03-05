using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MainTabsRoot), nameof(MainTabsRoot.HandleLowPriorityShortcuts))]
    public static class MainTabsRoot_HandleLowPriorityShortcuts_Patch
    {
        public static bool Prefix()
        {
            if (!State.IsActive) return true;

            if (Find.MainTabsRoot.OpenTab == MainButtonDefOf.Inspect &&
                (Find.Selector.NumSelected == 0 || WorldRendererUtility.WorldSelected))
            {
                Find.MainTabsRoot.EscapeCurrentTab(playSound: true);
            }

            var openTab = Find.MainTabsRoot.OpenTab;
            if (openTab != null && openTab != MainButtonDefOf.Inspect
                && Event.current.type == EventType.MouseDown
                && Event.current.button != 2)
            {
                Find.MainTabsRoot.EscapeCurrentTab(playSound: true);
            }

            return false;
        }
    }
}
