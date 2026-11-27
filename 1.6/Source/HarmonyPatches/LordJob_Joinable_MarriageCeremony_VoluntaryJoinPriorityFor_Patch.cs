using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(LordJob_Joinable_MarriageCeremony), nameof(LordJob_Joinable_MarriageCeremony.VoluntaryJoinPriorityFor))]
    public static class LordJob_Joinable_MarriageCeremony_VoluntaryJoinPriorityFor_Patch
    {
        public static void Postfix(LordJob_Joinable_MarriageCeremony __instance, Pawn p, ref float __result)
        {
            if (__result <= 0f) return;
            if (State.Avatar == null || State.Avatar.pawn != p) return;
            if (__instance.firstPawn == p || __instance.secondPawn == p) return;
            if (State.Avatar.IsGatheringDeclined(__instance.lord)) __result = 0f;
        }
    }
}
