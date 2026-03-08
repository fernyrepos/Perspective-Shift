using HarmonyLib;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.TryRecoverFromUnwalkablePosition))]
    public static class Pawn_PathFollower_TryRecoverFromUnwalkablePosition_Patch
    {
        public static bool Prefix(Pawn_PathFollower __instance, ref bool __result)
        {
            if (__instance.pawn.IsAvatar())
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
