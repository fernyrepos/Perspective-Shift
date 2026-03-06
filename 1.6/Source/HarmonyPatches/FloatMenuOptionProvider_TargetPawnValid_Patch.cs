using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider), nameof(FloatMenuOptionProvider.TargetPawnValid))]
    public static class FloatMenuOptionProvider_TargetPawnValid_Patch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (Avatar.IsAvatarLeftClick && pawn.RaceProps.Humanlike)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
