using HarmonyLib;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_PathFollower), "MovingNow", MethodType.Getter)]
    public static class Pawn_PathFollower_MovingNow_Patch
    {
        public static void Postfix(Pawn_PathFollower __instance, ref bool __result)
        {
            if (!__result && __instance.pawn.IsAvatar() && State.Avatar?.IsMoving == true)
                __result = true;
        }
    }
}
