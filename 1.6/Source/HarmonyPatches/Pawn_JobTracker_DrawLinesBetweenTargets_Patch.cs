using HarmonyLib;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.DrawLinesBetweenTargets))]
    public static class DrawLinesBetweenTargets_Patch
    {
        public static bool Prefix(Pawn_JobTracker __instance)
        {
            if (__instance.pawn.IsAvatar() && State.Avatar?.IsMoving == true)
                return false;
            return true;
        }
    }
}
