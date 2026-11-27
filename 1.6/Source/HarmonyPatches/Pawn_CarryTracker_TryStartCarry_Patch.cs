using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch]
    public static class Pawn_CarryTracker_TryStartCarry_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryStartCarry), new[] { typeof(Thing) });
            yield return AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryStartCarry), new[] { typeof(Thing), typeof(int), typeof(bool) });
        }

        public static void Prefix(Pawn_CarryTracker __instance, Thing item)
        {
            if (item == null || !__instance.pawn.IsAvatar()) return;
            item.TrySetForbidden(false);
        }
    }
}
