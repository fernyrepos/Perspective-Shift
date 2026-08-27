using HarmonyLib;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI))]
    public static class ThingOverlays_ThingOverlaysOnGUI_Patch
    {
        public static void Postfix()
        {
            if (Event.current.type != EventType.Repaint) return;
            State.DrawSleepOverlay();
        }
    }
}
