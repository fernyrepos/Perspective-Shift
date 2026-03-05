using HarmonyLib;
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

    [HotSwappable]
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

                if (__instance.pawn.IsAvatar())
                {
                    Log.Message($"[EndCurrentJob.Prefix] Avatar job ending: job={__state.def.defName}, captured snapshot.");
                }
            }
            else
            {
                __state = null;
            }
        }

        public static void Postfix(Pawn_JobTracker __instance, JobCondition condition, JobStateSnapshot __state)
        {
            if (!__instance.pawn.IsAvatar()) return;

            string jobName = __state?.def?.defName ?? "null";
            Log.Message($"[EndCurrentJob.Postfix] Avatar job ended: job={jobName}, condition={condition}, playerForced={__state?.playerForced ?? false}, isMoving={State.Avatar?.IsMoving ?? false}");

            if (State.Avatar?.IsMoving == true)
            {
                Log.Message($"[EndCurrentJob.Postfix] RETURN: movement cancels job continuation");
                return;
            }

            if (condition != JobCondition.Succeeded)
            {
                Log.Message($"[EndCurrentJob.Postfix] RETURN: condition is not Succeeded, skipping restart");
                return;
            }

            if (__state == null || __state.targetA.Thing == null)
            {
                Log.Message($"[EndCurrentJob.Postfix] RETURN: __state or targetA.Thing is null");
                return;
            }

            if (!__state.playerForced)
            {
                Log.Message($"[EndCurrentJob.Postfix] RETURN: job was not playerForced");
                return;
            }

            Log.Message($"[EndCurrentJob.Postfix] Attempting to restart job on target: {__state.targetA.Thing.Label}");
            State.Avatar.InteractWith(__state.targetA.Thing);
        }
    }
}
