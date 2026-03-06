using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HotSwappable]
    [HarmonyPatch(typeof(Selector), nameof(Selector.HandleMapClicks))]
    public static class Selector_HandleMapClicks_Patch
    {
        public static void Prefix(Selector __instance, ref List<object> __state)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 1) return;
            if (!State.IsActive || State.Avatar == null) return;

            if (__instance.SelectedPawns.Any()) return;

            if (__instance.SelectedPawns.Contains(State.Avatar.pawn)) return;

            FloatMenuMap_AvatarTracker.currentMenuSubject = State.Avatar.pawn;
            __state = [.. __instance.selected];
            __instance.selected.Add(State.Avatar.pawn);
        }

        public static void Postfix(Selector __instance, List<object> __state)
        {
            FloatMenuMap_AvatarTracker.currentMenuSubject = null;
            if (__state == null) return;
            __instance.selected.Clear();
            foreach (var obj in __state)
            {
                __instance.selected.Add(obj);
            }
        }
    }
}
