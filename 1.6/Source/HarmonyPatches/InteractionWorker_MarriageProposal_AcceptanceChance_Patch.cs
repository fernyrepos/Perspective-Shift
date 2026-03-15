using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.AcceptanceChance))]
    public static class InteractionWorker_MarriageProposal_AcceptanceChance_Patch
    {
        public static bool Prefix(ref float __result)
        {
            return State.TryApplyForcedInteraction(ref __result);
        }
    }
}
