using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Verb), "WarmupTime", MethodType.Getter)]
    public static class Verb_WarmupTime_Patch
    {
        public static void Postfix(Verb __instance, ref float __result)
        {
            if (__result > 0f && __instance.EquipmentCompSource != null && __instance.CasterIsPawn && __instance.CasterPawn.IsAvatar() && PerspectiveShiftMod.settings.disableAimingDelay)
            {
                if (__instance is IAbilityVerb)
                {
                    return;
                }
                __result = 0f;
            }
        }
    }
}
