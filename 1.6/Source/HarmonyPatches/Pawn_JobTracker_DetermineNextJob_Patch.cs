using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    public static class Pawn_JobTracker_DetermineNextJob_Patch
    {
        public static bool Prefix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = __instance.pawn;
            if (!pawn.IsAvatar()) return true;
            if (pawn.InMentalState) return true;

            if (pawn.Drafted)
            {
                var queued = new ThinkNode_QueuedJob();
                var queuedResult = queued.TryIssueJobPackage(pawn, default);
                if (queuedResult.IsValid)
                {
                    __result = queuedResult;
                    return false;
                }

                var orders = new JobGiver_Orders();
                var ordersResult = orders.TryIssueJobPackage(pawn, default);
                if (ordersResult.IsValid)
                {
                    __result = ordersResult;
                    return false;
                }

                __result = WaitResult();
                return false;
            }

            if (pawn.mindState?.duty != null) return true;
            if (pawn.GetLord() != null) return true;
            if (HasVoluntarilyJoinableLord(pawn)) return true;

            __result = WaitResult();
            return false;
        }

        private static ThinkResult WaitResult()
        {
            var wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = 60;
            wait.checkOverrideOnExpire = true;
            return new ThinkResult(wait, null);
        }

        private static bool HasVoluntarilyJoinableLord(Pawn pawn)
        {
            if (pawn.Map == null) return false;
            foreach (var lord in pawn.Map.lordManager.lords)
            {
                if (lord.LordJob is LordJob_VoluntarilyJoinable joinable
                    && !lord.ownedPawns.Contains(pawn)
                    && joinable.VoluntaryJoinPriorityFor(pawn) > 0f)
                    return true;
            }
            return false;
        }
    }
}
