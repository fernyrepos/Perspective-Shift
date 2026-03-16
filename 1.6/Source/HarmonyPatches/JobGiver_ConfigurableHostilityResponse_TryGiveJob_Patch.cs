using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(JobGiver_ConfigurableHostilityResponse), nameof(JobGiver_ConfigurableHostilityResponse.TryGiveJob))]
    public static class JobGiver_ConfigurableHostilityResponse_TryGiveJob_Patch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn.ShouldSeekEnemy())
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}
