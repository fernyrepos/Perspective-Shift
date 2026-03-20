using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class JobStateSnapshot
    {
        public JobDef def;
        public bool playerForced;
        public LocalTargetInfo targetA;
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    public static class Pawn_JobTracker_EndCurrentJob_Patch
    {
        public static void Prefix(Pawn_JobTracker __instance, out JobStateSnapshot __state)
        {
            if (__instance.curJob != null)
            {
                __state = new JobStateSnapshot
                {
                    def = __instance.curJob.def,
                    playerForced = __instance.curJob.playerForced,
                    targetA = __instance.curJob.targetA
                };
            }
            else
            {
                __state = null;
            }
        }

        public static void Postfix(Pawn_JobTracker __instance, JobCondition condition, JobStateSnapshot __state)
        {
            if (!__instance.pawn.IsAvatar()) return;
            if (State.Avatar?.IsMoving == true)
            {
                return;
            }
            if (condition != JobCondition.Succeeded)
            {
                return;
            }
            if (__state == null || __state.targetA.Thing == null)
            {
                return;
            }
            if (!__state.playerForced)
            {
                return;
            }
            if (__state.def == JobDefOf.Ingest)
            {
                return;
            }
            if (__state.targetA.HasThing && __state.targetA.ThingDestroyed is false && __state.targetA.ThingDiscarded)
            {
                State.Avatar.InteractWith(__state.targetA.Thing);
            }
        }
    }
}
