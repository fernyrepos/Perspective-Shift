using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Selector), nameof(Selector.SelectorOnGUI))]
    public static class Selector_SelectorOnGUI_Patch
    {
        public static bool Prefix()
        {
            if (!State.IsActive || State.Current == null) return true;

            return !State.Current.HandleSelectorClick();
        }
    }
}
