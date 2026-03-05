using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(PawnLeaner), nameof(PawnLeaner.ShouldLean))]
    public static class PawnLeaner_ShouldLean_Patch
    {
        public static bool Prefix(PawnLeaner __instance, ref bool __result)
        {
            Pawn pawn = __instance.pawn;
            if (pawn.IsAvatar() && !(pawn.stances.curStance is Stance_Busy) && __instance.shootSourceOffset != IntVec3.Zero)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
