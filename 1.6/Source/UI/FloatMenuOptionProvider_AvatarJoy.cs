using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_AvatarJoy : FloatMenuOptionProvider
    {
        public override bool Drafted => false;
        public override bool Undrafted => true;
        public override bool Multiselect => false;

        public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
        {
            if (!State.IsActive || context.FirstSelectedPawn != State.Avatar.pawn) yield break;
            var pawn = context.FirstSelectedPawn;
            if (pawn.needs?.joy == null) yield break;

            foreach (var jgDef in DefDatabase<JoyGiverDef>.AllDefsListForReading)
            {
                if (!jgDef.Worker.CanBeGivenTo(pawn)) continue;

                Job job = null;
                try { job = jgDef.Worker.TryGiveJob(pawn); } catch { }
                if (job == null) continue;

                if (!JobMatchesClick(job, context)) continue;

                var capturedJob = job;
                string label = (!jgDef.LabelCap.NullOrEmpty()) ? jgDef.LabelCap.ToString() : (!capturedJob.def.LabelCap.NullOrEmpty()) ? capturedJob.def.LabelCap.ToString() : "Unknown";
                yield return new FloatMenuOption(
                    "PS_DoJoy".Translate(label),
                    () => {
                        capturedJob.playerForced = true;
                        pawn.jobs.TryTakeOrderedJob(capturedJob, JobTag.Misc);
                    });
            }
        }

        private static bool JobMatchesClick(Job job, FloatMenuContext context)
        {
            if (job.targetA.Thing != null && context.ClickedThings.Contains(job.targetA.Thing)) return true;
            if (job.targetB.Thing != null && context.ClickedThings.Contains(job.targetB.Thing)) return true;
            if (job.targetA.IsValid && !job.targetA.HasThing && job.targetA.Cell == context.ClickedCell) return true;

            if (!context.ClickedCell.IsValid) return false;
            var clickTerrain = context.ClickedCell.GetTerrain(context.map);
            if (clickTerrain == null) return false;

            var cellsToCheck = new List<IntVec3>();
            if (job.targetA.IsValid && !job.targetA.HasThing) cellsToCheck.Add(job.targetA.Cell);
            if (job.targetQueueA != null)
                foreach (var t in job.targetQueueA)
                    if (t.IsValid && !t.HasThing) cellsToCheck.Add(t.Cell);

            foreach (var c in cellsToCheck)
                if (c.IsValid && c.GetTerrain(context.map) == clickTerrain)
                    return true;

            return false;
        }
    }
}
