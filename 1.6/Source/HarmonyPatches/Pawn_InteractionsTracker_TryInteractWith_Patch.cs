using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Pawn_InteractionsTracker_TryInteractWith_Patch
    {
        public static bool Prefix(Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef intDef, ref bool __result)
        {
            if (recipient.IsAvatar() && !State.skipDialog && (intDef == InteractionDefOf.RomanceAttempt || intDef == InteractionDefOf.MarriageProposal))
            {
                Find.WindowStack.Add(new Dialog_InteractionChoice(__instance.pawn, recipient, intDef));
                __result = true;
                return false;
            }
            return true;
        }
    }
}
