using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider_CleanRoom), "AppliesInt")]
    public static class FloatMenuOptionProvider_CleanRoom_Patch
    {
        public static bool Prefix(ref bool __result)
        {
            if (Avatar.IsAvatarLeftClick)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
