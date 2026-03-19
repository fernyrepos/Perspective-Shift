using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
    public static class UIRoot_Play_UIRootOnGUI_Patch
    {
        public static void Prefix()
        {
            if (Event.current.type != EventType.Repaint)
            {
                State.OnGUI();
            }
        }

        public static void Postfix()
        {
            if (Event.current.type == EventType.Repaint)
            {
                State.OnGUI();
            }
        }
    }
}
