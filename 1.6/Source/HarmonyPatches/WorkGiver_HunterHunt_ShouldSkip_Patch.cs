using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(WorkGiver_HunterHunt), nameof(WorkGiver_HunterHunt.ShouldSkip))]
    public static class WorkGiver_HunterHunt_ShouldSkip_Patch
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result && pawn.IsAvatar())
            {
                __result = true;
            }
        }
    }
}
