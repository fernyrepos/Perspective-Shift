using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.CanMeditateNow))]
    public static class MeditationUtility_CanMeditateNow_Patch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn.IsAvatar())
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
