using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_PSChunkWorkout : FloatMenuOptionProvider
    {
        public override bool Drafted => false;
        public override bool Undrafted => true;
        public override bool Multiselect => false;
        public override bool RequiresManipulation => true;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!ModCompatibility.RimbodyChunkWorkoutsAvailable) yield break;
            if (!State.IsActive || State.Avatar == null) yield break;

            var pawn = context.FirstSelectedPawn;
            if (pawn == null || pawn != State.Avatar.pawn) yield break;
            if (!ThingRequestGroup.Chunk.Includes(clickedThing.def)) yield break;
            if (!ModCompatibility.RimbodyTracksPhysique(pawn)) yield break;

            bool blocked = ModCompatibility.RimbodyWorkoutBlocked(pawn, out string reason);
            if (blocked && reason == null) yield break;

            if (!blocked && !pawn.CanReserveAndReach(clickedThing, PathEndMode.ClosestTouch, Danger.Deadly, 1, -1))
            {
                blocked = true;
                reason = "NoPath".Translate();
            }

            foreach (var workout in ModCompatibility.RimbodyChunkWorkouts)
            {
                string label = workout.labelKey.Translate(clickedThing.LabelShort);
                if (blocked)
                {
                    yield return new FloatMenuOption(label + " (" + reason + ")", null);
                    continue;
                }

                var chunk = clickedThing;
                var jobDef = workout.job;
                yield return new FloatMenuOption(label, delegate
                {
                    var job = JobMaker.MakeJob(jobDef, chunk);
                    job.count = 1;
                    job.playerForced = true;
                    job.ignoreForbidden = true;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }
    }
}
