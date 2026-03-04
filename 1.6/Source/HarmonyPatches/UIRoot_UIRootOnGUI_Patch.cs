using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
    public static class UIRoot_UIRootOnGUI_Patch
    {
        public static void Postfix()
        {
            State.OnGUI();
        }
    }
}
