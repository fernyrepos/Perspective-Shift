using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PerspectiveShift
{
    public static class PSFishingUtility
    {
        private static ResearchProjectDef _fishingResearch;
        private static bool _fishingResearchResolved;

        public static ResearchProjectDef FishingResearch
        {
            get
            {
                if (!_fishingResearchResolved)
                {
                    _fishingResearchResolved = true;
                    _fishingResearch = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Fishing");
                }
                return _fishingResearch;
            }
        }

        public static bool ResearchDone => FishingResearch == null || FishingResearch.IsFinished;

        private static WorkTypeDef _fishingWork;
        private static bool _fishingWorkResolved;

        public static WorkTypeDef FishingWork
        {
            get
            {
                if (!_fishingWorkResolved)
                {
                    _fishingWorkResolved = true;
                    _fishingWork = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Fishing");
                }
                return _fishingWork;
            }
        }

        public static bool FishingWorkDisabledFor(Pawn pawn)
        {
            return FishingWork != null && pawn.WorkTypeIsDisabled(FishingWork);
        }

        public static bool IsFishableCell(IntVec3 cell, Map map)
        {
            if (!ModsConfig.OdysseyActive || map == null) return false;
            if (!cell.InBounds(map)) return false;
            if (!cell.GetTerrain(map).IsWater) return false;
            if (cell.GetWaterBodyType(map) == WaterBodyType.None) return false;
            return map.waterBodyTracker.AnyFishPopulationAt(cell);
        }

        public static bool TryGetFishingSpot(Pawn pawn, IntVec3 cell, out IntVec3 standCell)
        {
            standCell = IntVec3.Invalid;
            if (pawn?.Map == null || !IsFishableCell(cell, pawn.Map)) return false;

            standCell = WorkGiver_Fish.BestStandSpotFor(pawn, cell);
            if (!standCell.IsValid) return false;
            return pawn.CanReach(standCell, PathEndMode.OnCell, Danger.Deadly);
        }

        public static float FishingSkill(Pawn pawn)
        {
            return pawn?.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0f;
        }

        public static ThingDef PreviewFishDef(IntVec3 cell, Map map)
        {
            var body = cell.GetWaterBody(map);
            if (body == null) return null;
            return body.CommonFishIncludingExtras?.FirstOrDefault() ?? body.UncommonFish?.FirstOrDefault();
        }

        public static void ResolveCatch(Pawn pawn, IntVec3 cell)
        {
            if (pawn?.Map == null) return;

            var negativeOutcomes = FishingUtility.GetNegativeFishingOutcomes(pawn, cell);
            if (negativeOutcomes != null && negativeOutcomes.Any())
            {
                ApplyNegativeOutcome(pawn, negativeOutcomes.RandomElement());
                return;
            }

            var catches = new List<Thing>(FishingUtility.GetCatchesFor(pawn, cell, false, out bool rare));
            if (!catches.Any())
            {
                Messages.Message("PS_FishingNothingBiting".Translate(), pawn, MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            bool placedAny = false;
            int total = catches.Sum(x => x.stackCount);
            foreach (var item in catches)
            {
                placedAny |= GenPlace.TryPlaceThing(item, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
            if (!placedAny) return;

            if (rare)
            {
                pawn.Map.waterBodyTracker.lastRareCatchTick = Find.TickManager.TicksGame;
                Find.LetterStack.ReceiveLetter("LetterLabelRareCatch".Translate(),
                    "LetterTextRareCatch".Translate(pawn.Named("PAWN")) + ":\n" + catches.Select(x => x.LabelCap).ToLineList("  - "),
                    LetterDefOf.PositiveEvent, catches);
            }
            else
            {
                pawn.Map.waterBodyTracker.Notify_Fished(cell, total);
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.SlaughteredFish, pawn.Named(HistoryEventArgsNames.Doer)));
            }

            PSFishingCompat.Notify_Caught(pawn, rare);

            SoundDefOf.Interact_CatchFish.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            Messages.Message("PS_FishingCaught".Translate(catches.Select(x => x.LabelCap).ToCommaList()), pawn, MessageTypeDefOf.PositiveEvent, false);
        }

        private static void ApplyNegativeOutcome(Pawn pawn, NegativeFishingOutcomeDef outcome)
        {
            pawn.Map.waterBodyTracker.lastNegativeCatchTick = Find.TickManager.TicksGame;

            if (outcome.damageDef != null)
            {
                var dinfo = new DamageInfo(outcome.damageDef, outcome.damageAmountRange.RandomInRange);
                dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
                pawn.TakeDamage(dinfo);
            }
            if (outcome.addsHediff != null)
            {
                var hediff = pawn.health.AddHediff(outcome.addsHediff);
                if (outcome.hediffSeverity > 0f) hediff.Severity = outcome.hediffSeverity;
            }
            Find.LetterStack.ReceiveLetter(outcome.letterLabel, outcome.letterText.Formatted(pawn.Named("PAWN")), outcome.letterDef, pawn);
        }
    }
}
