using HarmonyLib;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.CheckForJobOverride))]
    public static class Pawn_JobTracker_CheckForJobOverride_Patch
    {
        public static bool Prefix(Pawn_JobTracker __instance)
        {
            Pawn pawn = __instance.pawn;
            if (!pawn.IsAvatar()) return true;
            Job curJob = __instance.curJob;
            if (curJob == null)
            {
                return true;
            }
            if (curJob.playerForced)
            {
                return false;
            }
            return true;
        }
    }
}
