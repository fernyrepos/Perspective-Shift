using HarmonyLib;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.PatherDraw))]
    public static class Pawn_PathFollower_DrawPath_Patch
    {
        public static bool Prefix(Pawn_PathFollower __instance)
        {
            if (!__instance.pawn.IsAvatar()) return true;
            return !State.Avatar.IsMoving;
        }
    }
}
