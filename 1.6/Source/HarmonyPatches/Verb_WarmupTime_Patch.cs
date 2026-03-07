using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Verb), "WarmupTime", MethodType.Getter)]
    public static class Verb_WarmupTime_Patch
    {
        public static void Postfix(Verb __instance, ref float __result)
        {
            if (__result > 0f && __instance.CasterIsPawn && __instance.CasterPawn.IsAvatar() && PerspectiveShiftMod.settings.disableAimingDelay)
            {
                __result = 0f;
            }
        }
    }
}
