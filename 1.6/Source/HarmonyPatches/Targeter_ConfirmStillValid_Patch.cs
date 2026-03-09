using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Targeter), "ConfirmStillValid")]
    public static class Targeter_ConfirmStillValid_Patch
    {
        public static void Prefix(Targeter __instance)
        {
            if (State.IsActive && __instance.IsTargeting)
            {
                Thing currentCaster = __instance.targetingSource?.Caster ?? __instance.caster;

                if (currentCaster == State.Avatar.pawn)
                {
                    __instance.requiresCastedSelected = false;
                    __instance.allowNonSelectedTargetingSource = true;
                }
            }
        }
    }
}
