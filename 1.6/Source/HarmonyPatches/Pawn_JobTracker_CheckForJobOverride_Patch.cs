using HarmonyLib;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HotSwappable]
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
                Log.Message("[CheckForJobOverride.Prefix] Avatar: curJob is null, allowing override check");
                return true;
            }

            if (curJob.playerForced)
            {
                Log.Message($"[CheckForJobOverride.Prefix] BLOCKING override check for playerForced job: {curJob.def.defName}");
                return false;
            }

            Log.Message($"[CheckForJobOverride.Prefix] Avatar: allowing override check for non-forced job: {curJob.def.defName}");
            return true;
        }
    }
}
