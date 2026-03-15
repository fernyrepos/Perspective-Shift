using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance))]
    public static class InteractionWorker_RomanceAttempt_SuccessChance_Patch
    {
        public static bool Prefix(ref float __result)
        {
            return State.TryApplyForcedInteraction(ref __result);
        }
    }
}
