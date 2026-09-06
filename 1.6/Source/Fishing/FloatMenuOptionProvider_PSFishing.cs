using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_PSFishing : FloatMenuOptionProvider
    {
        public override bool Drafted => false;
        public override bool Undrafted => true;
        public override bool Multiselect => false;
        public override bool RequiresManipulation => true;

        public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
        {
            if (!ModsConfig.OdysseyActive || !PerspectiveShiftMod.settings.fishingMinigame) yield break;
            if (!State.IsActive || State.Avatar == null) yield break;

            var pawn = context.FirstSelectedPawn;
            if (pawn == null || pawn != State.Avatar.pawn) yield break;

            var cell = context.ClickedCell;
            if (!PSFishingUtility.IsFishableCell(cell, pawn.Map)) yield break;

            if (!PSFishingUtility.ResearchDone)
            {
                yield return new FloatMenuOption(
                    "PS_StartFishing".Translate() + " (" + "PS_FishingNeedsResearch".Translate(PSFishingUtility.FishingResearch.LabelCap) + ")",
                    null);
                yield break;
            }

            if (PSFishingUtility.FishingWorkDisabledFor(pawn))
            {
                yield return new FloatMenuOption(
                    "PS_StartFishing".Translate() + " (" + "PS_FishingIncapable".Translate() + ")", null);
                yield break;
            }

            if (!PSFishingUtility.TryGetFishingSpot(pawn, cell, out IntVec3 standCell))
            {
                yield return new FloatMenuOption(
                    "PS_StartFishing".Translate() + " (" + "NoPath".Translate() + ")", null);
                yield break;
            }

            yield return new FloatMenuOption("PS_StartFishing".Translate(), delegate
            {
                var job = JobMaker.MakeJob(DefsOf.PS_FishMinigame, cell, standCell);
                job.playerForced = true;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
        }
    }
}
