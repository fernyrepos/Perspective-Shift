using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Selector), nameof(Selector.SelectorOnGUI))]
    public static class Selector_SelectorOnGUI_Patch
    {
        public static bool Prefix()
        {
            if (Find.Targeter.IsTargeting) return true;
            if (!State.IsActive || State.Current == null) return true;

            return !State.Current.HandleSelectorClick();
        }
    }
}
