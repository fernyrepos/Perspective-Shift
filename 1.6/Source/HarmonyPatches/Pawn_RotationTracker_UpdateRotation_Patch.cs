using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_RotationTracker), nameof(Pawn_RotationTracker.UpdateRotation))]
    public static class Pawn_RotationTracker_UpdateRotation_Patch
    {
        public static bool Prefix(Pawn_RotationTracker __instance)
        {
            if (!__instance.pawn.IsAvatar()) return true;

            if (__instance.pawn.stances.curStance is Stance_Busy)
            {
                return true;
            }

            bool doingJob = __instance.pawn.jobs?.curJob != null && __instance.pawn.jobs.curJob.def != JobDefOf.Wait && __instance.pawn.jobs.curJob.def != JobDefOf.Wait_Combat;

            if (State.Avatar?.IsMoving == true && !doingJob)
            {
                return false;
            }

            if (__instance.pawn.Drafted && !__instance.pawn.InMentalState && !State.ControlsFrozen)
            {
                return false;
            }

            return true;
        }
    }
}
