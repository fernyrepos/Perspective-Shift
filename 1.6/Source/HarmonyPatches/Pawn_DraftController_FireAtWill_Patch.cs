using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_DraftController), "FireAtWill", MethodType.Getter)]
    public static class Pawn_DraftController_FireAtWill_Patch
    {
        public static bool Prefix(Pawn_DraftController __instance, ref bool __result)
        {
            if (__instance.pawn.IsAvatar())
            {
                __instance.fireAtWillInt = false;
                __result = false;
                return false;
            }
            return true;
        }
    }
}
