using HarmonyLib;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MentalBreakWorker_RunWild), nameof(MentalBreakWorker_RunWild.BreakCanOccur))]
    public static class MentalBreakWorker_RunWild_BreakCanOccur_Patch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn.IsAvatar())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MentalBreakWorker_RunWild), nameof(MentalBreakWorker_RunWild.TryStart))]
    public static class MentalBreakWorker_RunWild_TryStart_Patch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn.IsAvatar())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
