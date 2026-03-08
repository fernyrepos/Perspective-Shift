using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(JobGiver_IdleError), "TryGiveJob")]
    public static class JobGiver_IdleError_TryGiveJob_Patch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn.IsAvatar())
            {
                var wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = 60;
                wait.checkOverrideOnExpire = true;
                __result = wait;
                return false;
            }
            return true;
        }
    }
}
