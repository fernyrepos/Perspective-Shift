using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(PawnTweener), nameof(PawnTweener.PreDrawPosCalculation))]
    public static class PawnTweener_PreDrawPosCalculation_Patch
    {
        public static bool Prefix(PawnTweener __instance)
        {
            if (!__instance.pawn.IsAvatar()) return true;

            if (State.Avatar?.physicsPosition != null) return false;

            return true;
        }
    }
}
