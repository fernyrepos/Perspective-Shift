using HarmonyLib;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MentalBreaker), nameof(MentalBreaker.CanDoRandomMentalBreaks), MethodType.Getter)]
    public static class MentalBreaker_CanDoRandomMentalBreaks_Patch
    {
        public static void Postfix(MentalBreaker __instance, ref bool __result)
        {
            if (!__result) return;
            if (!PerspectiveShiftMod.settings.noPlayerMentalBreaks) return;
            if (__instance.pawn.IsAvatar()) __result = false;
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.MentalBreaksBlocked))]
    public static class MentalStateHandler_MentalBreaksBlocked_Patch
    {
        public static void Postfix(MentalStateHandler __instance, ref bool __result)
        {
            if (__result) return;
            if (!PerspectiveShiftMod.settings.noPlayerMentalBreaks) return;
            if (__instance.pawn.IsAvatar()) __result = true;
        }
    }
}
