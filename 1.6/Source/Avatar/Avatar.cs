using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using System.Collections.Generic;
using Verse.AI;

namespace PerspectiveShift
{
    [StaticConstructorOnStartup]
    [HotSwappable]
    public partial class Avatar : IExposable
    {
        public Pawn pawn;
        public Lord savedLord;
        public Vector3 moveInput;
        public bool isSprinting;
        public bool isWalking;
        public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
        public Vector3? physicsPosition;
        public Vector3 LeanTarget = Vector3.zero;
        public Vector3 LeanSmoothed = Vector3.zero;
        public float aimAngle = -1f;
        public Thing CarriedThing => pawn.carryTracker?.CarriedThing;

        private Building pendingMinifiedPickup;
        private Building_Door interactingDoor;
        private bool wasMovingLastFrame;
        private bool wasFullyRested = false;
        private bool passedOut = false;
        private Dictionary<string, bool> needsAlerted = new Dictionary<string, bool>();

        public Avatar() { }
        public Avatar(Pawn pawn)
        {
            this.pawn = pawn;
        }

        public void Tick()
        {
            if (pawn.Downed || !pawn.Spawned || pawn.Map == null || pawn.IsUnderAIControl() || pawn.InMentalState) return;

            HandleNeeds();
            HandleCarriedThing();
            HandleDoorInteraction();
            HandleCombatStance();
            TryAutoPickupMinifiedItem();
            PreventJobExpiry();
            HandlePather();
        }

        private void HandleNeeds()
        {
            if (pawn.needs == null) return;
            if (pawn.needs.rest != null) HandleRestNeed();
            HandlePinnedNeeds();
        }

        private void HandleRestNeed()
        {
            if (pawn.CurJob?.startInvoluntarySleep == true)
                passedOut = true;
            if (passedOut && pawn.needs.rest.CurLevelPercentage >= 0.25f)
                passedOut = false;

            if (!wasFullyRested && pawn.needs.rest.CurLevelPercentage >= 0.99f)
            {
                wasFullyRested = true;
                Messages.Message("PS_FullyRested".Translate(pawn.LabelShort),
                    pawn, MessageTypeDefOf.PositiveEvent, false);
            }
            else if (wasFullyRested && pawn.needs.rest.CurLevelPercentage <= 0.80f)
            {
                wasFullyRested = false;
            }

            if (passedOut && pawn.CurJobDef != JobDefOf.LayDown)
            {
                if (pawn.Drafted) pawn.drafter.Drafted = false;
                Job sleepJob = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
                sleepJob.forceSleep = true;
                pawn.jobs.StartJob(sleepJob, JobCondition.InterruptForced);
            }
        }

        private void HandlePinnedNeeds()
        {
            foreach (var needName in PerspectiveShiftMod.settings.pinnedNeeds)
            {
                var need = pawn.needs.AllNeeds.FirstOrDefault(n => n.def.defName == needName);
                if (need == null) continue;
                bool isAlerted = needsAlerted.TryGetValue(needName, out bool v) && v;
                if (need.CurLevelPercentage <= 0.01f && !isAlerted)
                {
                    needsAlerted[needName] = true;
                    string customKey = "PS_NeedRanOut_" + needName;
                    string msg = customKey.CanTranslate()
                        ? customKey.Translate(pawn.LabelShort)
                        : "PS_NeedRanOut".Translate(pawn.LabelShort, need.LabelCap);
                    Messages.Message(msg, pawn, MessageTypeDefOf.NegativeEvent, false);
                }
                else if (need.CurLevelPercentage >= 0.15f && isAlerted)
                {
                    needsAlerted[needName] = false;
                }
            }
        }

        private void HandleCarriedThing()
        {
            if (pawn.carryTracker?.CarriedThing != null)
            {
                if (pawn.Drafted)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                else if (pawn.CurJob != null)
                {
                    if (!pawn.Map.reservationManager.ReservedBy(pawn.carryTracker.CarriedThing, pawn, pawn.CurJob))
                    {
                        pawn.Map.reservationManager.Reserve(pawn, pawn.CurJob, pawn.carryTracker.CarriedThing);
                    }
                    if (!pawn.Map.physicalInteractionReservationManager.IsReservedBy(pawn, pawn.carryTracker.CarriedThing))
                    {
                        pawn.Map.physicalInteractionReservationManager.Reserve(pawn, pawn.CurJob, pawn.carryTracker.CarriedThing);
                    }
                }
            }
        }

        private void HandlePather()
        {
            if (IsMoving && pawn.pather != null)
            {
                pawn.pather.lastMovedTick = Find.TickManager.TicksGame;

                float terrainMult = GetMovementSpeedMultiplier(pawn.Position);
                float gaitMult = isSprinting ? PerspectiveShiftMod.settings.sprintSpeedMultiplier
                                : isWalking ? PerspectiveShiftMod.settings.sneakSpeedMultiplier
                                : 1f;

                float moveSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed) * PerspectiveShiftMod.settings.moveSpeedMultiplier * terrainMult * gaitMult;
                pawn.pather.nextCellCostTotal = Mathf.Max(60f / Mathf.Max(moveSpeed, 0.1f), 1f);
            }
        }

        private void PreventJobExpiry()
        {
            if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def != JobDefOf.Wait && pawn.jobs.curJob.def != JobDefOf.Wait_Combat)
            {
                pawn.jobs.curJob.expiryInterval = -1;
            }
        }

        private void TryAutoPickupMinifiedItem()
        {
            if (pendingMinifiedPickup != null)
            {
                if (pawn.CurJob == null || pawn.CurJob.def != JobDefOf.Uninstall)
                {
                    var minified = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.MinifiedThing), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Some));
                    if (minified != null && minified is MinifiedThing mt && mt.InnerThing == pendingMinifiedPickup)
                    {
                        if (pawn.Position.DistanceTo(minified.Position) <= PerspectiveShiftMod.settings.grabRange && TryPickup(minified))
                        {
                            pendingMinifiedPickup = null;
                        }
                        else
                        {
                            Log.Error("[PerspectiveShift] Minified thing is not in range, cannot pick it up");
                            pendingMinifiedPickup = null;
                        }
                    }
                    else
                    {
                        pendingMinifiedPickup = null;
                    }
                }
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn", saveDestroyedThings: true);
            Scribe_References.Look(ref interactingDoor, "interactingDoor");
            Scribe_References.Look(ref savedLord, "savedLord");
            Scribe_References.Look(ref pendingMinifiedPickup, "pendingMinifiedPickup");
            Scribe_Values.Look(ref wasFullyRested, "wasFullyRested", false);
            Scribe_Values.Look(ref passedOut, "passedOut", false);
            Scribe_Collections.Look(ref needsAlerted, "needsAlerted", LookMode.Value, LookMode.Value);
            if (needsAlerted == null) needsAlerted = new Dictionary<string, bool>();
        }
    }
}
