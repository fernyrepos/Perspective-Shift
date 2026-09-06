using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PerspectiveShift
{
    public class JobDriver_PSFishMinigame : JobDriver
    {
        public enum Stage
        {
            Waiting,
            Biting,
            Hooked,
            Reeling,
        }

        private const int MinBiteTicks = 60;
        private const int MaxBiteTicks = 1200;
        private const float ReactionWindow = 1.1f;
        private const float HitHold = 0.75f;

        private Stage stage = Stage.Waiting;
        private int biteTicksLeft;
        private float stageRealTime;
        private Dialog_FishingMinigame dialog;

        public Stage CurrentStage => stage;

        public string AlertText
        {
            get
            {
                if (stage == Stage.Biting) return "!";
                if (stage == Stage.Hooked) return "PS_FishingHit".Translate();
                return null;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stage, "psFishStage", Stage.Waiting);
            Scribe_Values.Look(ref biteTicksLeft, "psFishBiteTicks", 0);
        }

        public bool TryHook()
        {
            if (stage != Stage.Biting) return false;
            if (Time.realtimeSinceStartup - stageRealTime > ReactionWindow) return false;

            stage = Stage.Hooked;
            stageRealTime = Time.realtimeSinceStartup;
            DefsOf.PS_FishHit.PlayOneShotOnCamera();
            return true;
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !PSFishingUtility.IsFishableCell(job.GetTarget(TargetIndex.A).Cell, pawn.Map));

            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            var fish = ToilMaker.MakeToil("PSFish");
            fish.defaultCompleteMode = ToilCompleteMode.Never;
            fish.handlingFacing = true;
            fish.initAction = delegate
            {
                BeginWaiting();
            };
            fish.WithEffect(PSFishingCompat.FishingEffecterFor(pawn), () => job.GetTarget(TargetIndex.A));
            fish.tickAction = delegate
            {
                pawn.rotationTracker.FaceCell(job.GetTarget(TargetIndex.A).Cell);
                switch (stage)
                {
                    case Stage.Waiting:
                        if (--biteTicksLeft > 0) return;
                        stage = Stage.Biting;
                        stageRealTime = Time.realtimeSinceStartup;
                        DefsOf.PS_FishBite.PlayOneShotOnCamera();
                        return;

                    case Stage.Biting:
                        if (Time.realtimeSinceStartup - stageRealTime > ReactionWindow) BeginWaiting();
                        return;

                    case Stage.Hooked:
                        if (Time.realtimeSinceStartup - stageRealTime < HitHold) return;
                        stage = Stage.Reeling;
                        dialog = new Dialog_FishingMinigame(pawn, job.GetTarget(TargetIndex.A).Cell);
                        Find.WindowStack.Add(dialog);
                        return;

                    default:
                        if (dialog == null || !Find.WindowStack.IsOpen(dialog)) ReadyForNextToil();
                        return;
                }
            };
            yield return fish;
        }

        private void BeginWaiting()
        {
            stage = Stage.Waiting;
            biteTicksLeft = Rand.RangeInclusive(MinBiteTicks, MaxBiteTicks);
            stageRealTime = Time.realtimeSinceStartup;
        }
    }
}
