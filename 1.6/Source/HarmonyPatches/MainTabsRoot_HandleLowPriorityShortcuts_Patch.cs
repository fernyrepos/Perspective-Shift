using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MainTabsRoot), nameof(MainTabsRoot.HandleLowPriorityShortcuts))]
    public static class MainTabsRoot_HandleLowPriorityShortcuts_Patch
    {
        public static bool Prefix()
        {
            if (State.IsActive)
            {
                return false;
            }
            return true;
        }
    }
}
