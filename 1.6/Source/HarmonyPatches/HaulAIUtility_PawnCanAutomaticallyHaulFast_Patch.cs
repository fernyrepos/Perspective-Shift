using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaulFast))]
    public static class HaulAIUtility_PawnCanAutomaticallyHaulFast_Patch
    {
        public static bool Prefix(Pawn p, Thing t, bool forced, ref bool __result)
        {
            return HaulAIUtility_PawnCanAutomaticallyHaul_Patch.Prefix(p, t, forced, ref __result);
        }
    }
}
