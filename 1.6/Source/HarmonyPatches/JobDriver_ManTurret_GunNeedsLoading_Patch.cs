using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(JobDriver_ManTurret), nameof(JobDriver_ManTurret.GunNeedsLoading))]
    public static class JobDriver_ManTurret_GunNeedsLoading_Patch
    {
        public static bool Prefix(Building b, ref bool __result)
        {
            if (State.IsActive && State.Avatar?.pawn?.CurJob != null && 
                State.Avatar.pawn.CurJob.def == JobDefOf.ManTurret && 
                State.Avatar.pawn.CurJob.targetA.Thing == b)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
