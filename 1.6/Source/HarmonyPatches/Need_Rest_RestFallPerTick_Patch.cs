using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Need_Rest), "RestFallPerTick", MethodType.Getter)]
    public static class Need_Rest_RestFallPerTick_Patch
    {
        public static void Postfix(Need_Rest __instance, ref float __result)
        {
            if (State.IsActive && __instance.pawn == State.Avatar.pawn && State.Avatar.isSprinting && State.Avatar.IsMoving)
            {
                __result *= PerspectiveShiftMod.settings.sprintSleepDrain;
            }
        }
    }
}
