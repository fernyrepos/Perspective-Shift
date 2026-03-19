using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PerspectiveShift
{
    public partial class Avatar
    {
        private static readonly HashSet<Type> FloatMenuProviderBlacklist = new HashSet<Type>
        {
            typeof(FloatMenuOptionProvider_DropEquipment),
            typeof(FloatMenuOptionProvider_CleanRoom),
        };

        private bool HandleLeftClick()
        {
            IsAvatarLeftClick = true;
            bool result = false;
            try
            {
                result = HandleLeftClickInt();
            }
            catch (Exception ex)
            {
                Log.Error("[PerspectiveShift] Error in HandleLeftClick: " + ex);
            }
            finally
            {
                IsAvatarLeftClick = false;
            }
            return result;
        }

        private bool HandleLeftClickInt()
        {
            if (pawn.Map == null || !pawn.Spawned) return false;

            var clickCell = UI.MouseCell();
            var things = clickCell.GetThingList(pawn.Map);
            bool withinGrabRange = pawn.Position.DistanceTo(clickCell) <= PerspectiveShiftMod.settings.grabRange;

            if (CarriedThing != null)
                return HandleDropOrInteract(clickCell, withinGrabRange, CarriedThing);

            bool anyInRange = false;
            foreach (var thing in things)
            {
                bool inRange = withinGrabRange || IsWithinInteractionRange(thing);
                if (!inRange) continue;
                anyInRange = true;
                if (TryHandleStorageBuilding(thing)) return true;
                if (TryHandleConstructionThing(thing)) return true;
                if (TryHandleBuildingInteractions(thing)) return true;
            }

            if (withinGrabRange && TryHandlePickup(clickCell)) return true;
            if (anyInRange && TryHandleFloatMenu(clickCell)) return true;

            return TryExecuteDesignatorlessFallback(clickCell, withinGrabRange);
        }

        private bool HandleDropOrInteract(IntVec3 cell, bool itemInRange, Thing carriedThing)
        {
            if (!cell.InBounds(pawn.Map))
            {
                return false;
            }

            var cellThings = cell.GetThingList(pawn.Map);

            bool wouldWipe = false;
            IHaulDestination haulDest = null;

            foreach (var t in cellThings)
            {
                if (TryHandleBlueprintBuild(t, carriedThing)) return true;
                if (TryHandleBlueprintInstall(t, carriedThing)) return true;
                if (TryHandleFrame(t, carriedThing)) return true;
                if (TryHandleMedicineDrop(t, carriedThing)) return true;
                if (TryHandleTurretLoad(t, carriedThing)) return true;
                if (TryHandleRefuel(t, carriedThing)) return true;
                if (TryHandleBedDrop(t, carriedThing, cell)) return true;
                if (TryHandleMechCharger(t, carriedThing)) return true;
                if (ModCompatibility.IsVehiclePawn(t))
                {
                    if (ModCompatibility.PutCargoToVehicle(t, carriedThing)) return true;
                }
                else
                {
                    if (TryHandleContainerTransfer(t, carriedThing)) return true;
                }
                if (TryDepositIntoBill(t, carriedThing)) return true;

                if (haulDest == null && t is IHaulDestination dest)
                {
                    haulDest = dest;
                }

                if (GenSpawn.SpawningWipes(carriedThing.def, t.def))
                {
                    wouldWipe = true;
                }
            }

            if (haulDest == null)
            {
                haulDest = pawn.Map.haulDestinationManager.SlotGroupAt(cell)?.parent;
            }

            ThingPlaceMode placeMode = wouldWipe ? ThingPlaceMode.Near : ThingPlaceMode.Direct;

            if (!itemInRange) return false;

            if (haulDest != null)
            {
                var parentSettings = haulDest.GetParentStoreSettings();
                bool isPossible = parentSettings == null || parentSettings.AllowedToAccept(carriedThing);

                if (!isPossible || (!haulDest.GetStoreSettings().AllowedToAccept(carriedThing) && !haulDest.GetStoreSettings().filter.Allows(carriedThing.def)))
                {
                    if (!isPossible)
                    {
                        Messages.Message("PS_StorageImpossible".Translate(carriedThing.LabelCap), MessageTypeDefOf.RejectInput, false);
                        DropAdjacent(cell);
                        return true;
                    }
                    else
                    {
                        string label = (haulDest as ISlotGroupParent)?.SlotYielderLabel() ?? (haulDest as Thing)?.Label ?? "Storage";
                        var text = "PS_StorageNotPermitted".Translate(carriedThing.Label, label);
                        Find.WindowStack.Add(new Dialog_MessageBox(text, "Yes".Translate(), () =>
                        {
                            haulDest.GetStoreSettings().filter.SetAllow(carriedThing.def, true);
                            if (!TryDepositInDestination(haulDest, carriedThing, cell, placeMode))
                            {
                                Messages.Message("PS_StorageImpossible".Translate(carriedThing.LabelCap), MessageTypeDefOf.RejectInput, false);
                                DropAdjacent(cell);
                            }
                        }, "No".Translate(), () =>
                        {
                            pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                        }));
                        return true;
                    }
                }
                else
                {
                    IntVec3 depositCell = cell;
                    foreach (var t in cellThings)
                    {
                        if (t is Building_Storage bStorage)
                        {
                            foreach (var c in bStorage.AllSlotCellsList())
                            {
                                if (StoreUtility.IsGoodStoreCell(c, pawn.Map, carriedThing, pawn, pawn.Faction))
                                {
                                    depositCell = c;
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    if (TryDepositInDestination(haulDest, carriedThing, depositCell, placeMode))
                    {
                        return true;
                    }
                    bool filterAllows = haulDest.GetStoreSettings()?.AllowedToAccept(carriedThing) == true;
                    Messages.Message(filterAllows
                        ? "PS_StorageFull".Translate(carriedThing.LabelCap)
                        : "PS_StorageImpossible".Translate(carriedThing.LabelCap),
                        MessageTypeDefOf.RejectInput, false);
                    DropAdjacent(cell);
                    return true;
                }
            }

            if (cell.Walkable(pawn.Map))
            {
                if (pawn.carryTracker.TryDropCarriedThing(cell, placeMode, out var _))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryHandleBlueprintBuild(Thing t, Thing carriedThing)
        {
            if (t is Blueprint_Build bpCheck)
            {
                if (bpCheck.ThingCountNeeded(carriedThing.def) <= 0)
                {
                    Messages.Message("PS_CannotDropOnBlueprint".Translate(), MessageTypeDefOf.RejectInput, false);
                    return true;
                }
                if (bpCheck.TryReplaceWithSolidThing(pawn, out Thing frameThing, out _) && frameThing is Frame frame1)
                {
                    var needed = frame1.ThingCountNeeded(carriedThing.def);
                    if (needed > 0)
                    {
                        pawn.carryTracker.innerContainer.TryTransferToContainer(carriedThing, frame1.resourceContainer, Mathf.Min(carriedThing.stackCount, needed));
                    }
                    return true;
                }
            }
            return false;
        }

        private bool TryHandleBlueprintInstall(Thing t, Thing carriedThing)
        {
            if (t is Blueprint_Install installBp)
            {
                if (installBp.MiniToInstallOrBuildingToReinstall != carriedThing)
                {
                    Messages.Message("PS_CannotDropOnBlueprint".Translate(), MessageTypeDefOf.RejectInput, false);
                    return true;
                }
                if (installBp.MiniToInstallOrBuildingToReinstall == carriedThing)
                {
                    installBp.TryReplaceWithSolidThing(pawn, out _, out _);
                    return true;
                }
            }
            return false;
        }

        private bool TryHandleFrame(Thing t, Thing carriedThing)
        {
            if (t is Frame frame)
            {
                if (frame.ThingCountNeeded(carriedThing.def) <= 0)
                {
                    Messages.Message("PS_CannotDropOnBlueprint".Translate(), MessageTypeDefOf.RejectInput, false);
                    return true;
                }
                if (frame.IsCompleted())
                {
                    Messages.Message("PS_BlueprintFull".Translate(), MessageTypeDefOf.RejectInput, false);
                    return true;
                }
                var needed = frame.ThingCountNeeded(carriedThing.def);
                if (needed > 0)
                {
                    pawn.carryTracker.innerContainer.TryTransferToContainer(carriedThing, frame.resourceContainer, Mathf.Min(carriedThing.stackCount, needed));
                    return true;
                }
            }
            return false;
        }

        private bool TryHandleMedicineDrop(Thing t, Thing carriedThing)
        {
            if (carriedThing.def.IsMedicine && t is Pawn patient && patient.health.HasHediffsNeedingTend())
            {
                var job = JobMaker.MakeJob(JobDefOf.TendPatient, patient, carriedThing);
                if (TryStartForcedJob(job)) return true;
            }
            return false;
        }

        private bool TryHandleTurretLoad(Thing t, Thing carriedThing)
        {
            if (t is Building_TurretGun mortar)
            {
                var comp = mortar.gun?.TryGetComp<CompChangeableProjectile>();
                if (comp != null && !comp.Loaded && comp.allowedShellsSettings.AllowedToAccept(carriedThing))
                {
                    var shell = carriedThing;
                    comp.LoadShell(shell.def, 1);
                    shell.SplitOff(1).Destroy();
                    SoundDefOf.Artillery_ShellLoaded.PlayOneShot(new TargetInfo(mortar.Position, pawn.Map));
                    Messages.Message("PS_ShellLoaded".Translate(shell.def.label), MessageTypeDefOf.TaskCompletion);
                    return true;
                }
            }
            return false;
        }

        private bool TryHandleRefuel(Thing t, Thing carriedThing)
        {
            var refuelableComp = t.TryGetComp<CompRefuelable>();
            if (refuelableComp != null && refuelableComp.Props.fuelFilter.Allows(carriedThing) && refuelableComp.GetFuelCountToFullyRefuel() > 0)
            {
                var amount = Mathf.Min(carriedThing.stackCount, refuelableComp.GetFuelCountToFullyRefuel());
                refuelableComp.Refuel(amount);
                carriedThing.SplitOff(amount).Destroy();
                if (t.def.soundInteract != null) t.def.soundInteract.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                return true;
            }
            return false;
        }

        private bool TryHandleBedDrop(Thing t, Thing carriedThing, IntVec3 cell)
        {
            if (t is Building_Bed bed && carriedThing is Pawn carriedPawn && carriedPawn.Downed)
            {
                if (bed.ForPrisoners && !carriedPawn.IsPrisonerOfColony)
                {
                    if (carriedPawn.RaceProps.Humanlike && carriedPawn.guest != null)
                    {
                        if (carriedPawn.guest.Released)
                        {
                            carriedPawn.guest.Released = false;
                            carriedPawn.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.MaintainOnly);
                            GenGuest.RemoveHealthyPrisonerReleasedThoughts(carriedPawn);
                        }
                        carriedPawn.guest.CapturedBy(Faction.OfPlayer, pawn);
                    }
                    else
                    {
                        return RejectInteraction("PS_BedForPrisonersOnly");
                    }
                }
                if (bed.ForSlaves && !carriedPawn.IsSlaveOfColony)
                {
                    return RejectInteraction("PS_BedForSlavesOnly");
                }
                if (bed.ForColonists && !carriedPawn.IsColonist)
                {
                    if (carriedPawn.RaceProps.Humanlike && carriedPawn.guest != null && carriedPawn.Faction != Faction.OfPlayer && carriedPawn.HostFaction != Faction.OfPlayer && !carriedPawn.IsWildMan() && carriedPawn.DevelopmentalStage != DevelopmentalStage.Baby && !carriedPawn.HostileTo(Faction.OfPlayer))
                    {
                        carriedPawn.guest.SetGuestStatus(Faction.OfPlayer);
                        QuestUtility.SendQuestTargetSignals(carriedPawn.questTags, "Rescued", carriedPawn.Named("SUBJECT"));
                    }
                    else
                    {
                        return RejectInteraction("PS_BedForColonistsOnly");
                    }
                }
                pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out _);
                carriedPawn.jobs.Notify_TuckedIntoBed(bed);
                return true;
            }
            return false;
        }

        private bool TryHandleMechCharger(Thing t, Thing carriedThing)
        {
            if (t is Building_MechCharger charger
                && carriedThing is Pawn carriedMech
                && carriedMech.RaceProps.IsMechanoid && carriedMech.IsColonyMech)
            {
                if (charger.IsCompatibleWithCharger(carriedMech.kindDef)
                    && !charger.IsFullOfWaste && charger.IsPowered)
                {
                    pawn.carryTracker.TryDropCarriedThing(
                        charger.InteractionCell, ThingPlaceMode.Direct, out Thing dropped);
                    if (dropped is Pawn droppedMech)
                    {
                        var chargeJob = JobMaker.MakeJob(JobDefOf.MechCharge, charger);
                        droppedMech.jobs.TryTakeOrderedJob(chargeJob, JobTag.Misc);
                    }
                    return true;
                }
            }
            return false;
        }

        private bool TryHandleContainerTransfer(Thing t, Thing carriedThing)
        {
            if (t != pawn)
            {
                var container = t.TryGetInnerInteractableThingOwner();
                if (container != null)
                {
                    var sound = carriedThing.def.soundDrop;
                    int countBefore = carriedThing.stackCount;
                    int transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(carriedThing, container, carriedThing.stackCount);

                    if (transferred > 0 || carriedThing.Destroyed || carriedThing.stackCount < countBefore || pawn.carryTracker.CarriedThing != carriedThing)
                    {
                        sound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryDepositInDestination(IHaulDestination dest, Thing item, IntVec3 cell, ThingPlaceMode placeMode)
        {
            if (dest is Building b && b is not ISlotGroupParent)
            {
                var thingOwner = b.TryGetInnerInteractableThingOwner();
                if (thingOwner != null)
                {
                    if (dest.Accepts(item))
                    {
                        int countBefore = item.stackCount;
                        int transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(item, thingOwner, item.stackCount);

                        if (transferred > 0 || item.Destroyed || item.stackCount < countBefore || pawn.carryTracker.CarriedThing != item)
                            return true;
                    }
                    return false;
                }
            }

            if (dest.Accepts(item))
            {
                return pawn.carryTracker.TryDropCarriedThing(cell, placeMode, out _);
            }
            return false;
        }

        private void DropAdjacent(IntVec3 cell)
        {
            IntVec3 dropCell = cell;
            foreach (var adj in GenAdj.AdjacentCells)
            {
                var c = cell + adj;
                if (c.InBounds(pawn.Map) && c.Walkable(pawn.Map) && pawn.Map.haulDestinationManager.SlotGroupAt(c) == null && c.GetFirstBuilding(pawn.Map) == null)
                {
                    dropCell = c;
                    break;
                }
            }
            if (dropCell == cell) dropCell = pawn.Position;
            pawn.carryTracker.TryDropCarriedThing(dropCell, ThingPlaceMode.Near, out _);
        }

        private bool TryDepositIntoBill(Thing thing, Thing carriedThing)
        {
            if (thing is IBillGiver billGiver)
            {
                foreach (var bill in billGiver.BillStack)
                {
                    if (bill.ShouldDoNow() && bill.IsFixedOrAllowedIngredient(carriedThing))
                    {
                        var container = thing.TryGetInnerInteractableThingOwner();
                        if (container != null && thing is not ISlotGroupParent)
                        {
                            int transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(carriedThing, container, carriedThing.stackCount);

                            if (transferred > 0)
                            {
                                TryStartBillJob(billGiver);
                                return true;
                            }
                        }
                        if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing droppedItem))
                        {
                            TryStartBillJob(billGiver);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void TryStartBillJob(IBillGiver billGiver)
        {
            Thing billGiverThing = billGiver as Thing;
            if (billGiverThing == null) return;
            WorkGiver_DoBill workGiver = null;
            List<WorkGiverDef> allDefs = DefDatabase<WorkGiverDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                var def = allDefs[i];
                if (def.Worker is WorkGiver_DoBill wdb && wdb.ThingIsUsableBillGiver(billGiverThing))
                {
                    workGiver = wdb;
                    break;
                }
            }

            if (workGiver != null)
            {
                var job = workGiver.JobOnThing(pawn, billGiverThing, forced: true);
                if (job != null)
                {
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            }
        }

        private bool TryHandleStorageBuilding(Thing thing)
        {
            var storageBuilding = thing as Building_Storage;
            if (storageBuilding == null) return false;

            if (pawn.Map.designationManager.DesignationOn(storageBuilding, DesignationDefOf.Deconstruct) != null)
            {
                return TryStartForcedJobWithIgnoreDesignations(storageBuilding, JobDefOf.Deconstruct);
            }
            if (pawn.Map.designationManager.DesignationOn(storageBuilding, DesignationDefOf.Uninstall) != null)
            {
                return TryStartForcedJobWithIgnoreDesignations(storageBuilding, JobDefOf.Uninstall);
            }
            if (InstallBlueprintUtility.ExistingBlueprintFor(storageBuilding) != null)
            {
                return false;
            }
            Find.WindowStack.Add(new Dialog_StorageMenu(storageBuilding));
            return true;
        }

        private bool TryHandlePickup(IntVec3 clickCell)
        {
            var item = clickCell.GetFirstItem(pawn.Map);
            if (item != null && item.def.category == ThingCategory.Item)
            {
                return TryPickup(item);
            }
            var clickedPawn = clickCell.GetFirstPawn(pawn.Map);
            if (clickedPawn != null && clickedPawn != pawn
                && (clickedPawn.Downed || clickedPawn.IsSelfShutdown()))
            {
                return TryPickup(clickedPawn);
            }
            return false;
        }

        private bool TryPickup(Thing target)
        {
            var reservers = new HashSet<Pawn>();
            pawn.Map.reservationManager.ReserversOf(target, reservers);
            foreach (var r in reservers.ToList())
            {
                if (r != pawn && r.jobs != null)
                {
                    r.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }

            pawn.Map.reservationManager.ReleaseAllForTarget(target);
            pawn.Map.physicalInteractionReservationManager.ReleaseAllForTarget(target);

            var pickedUpCount = pawn.carryTracker.TryStartCarry(target, target.stackCount, reserve: true);

            if (pickedUpCount > 0)
            {
                if (target.def.soundPickup != null)
                    target.def.soundPickup.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                return true;
            }
            return false;
        }

        private bool TryHandleConstructionThing(Thing thing)
        {
            var blueprint = thing as Blueprint;
            if (blueprint != null && InteractWith(blueprint)) return true;
            var frame = thing as Frame;
            if (frame != null && InteractWith(frame)) return true;
            return false;
        }

        private bool TryHandleBuildingInteractions(Thing thing)
        {
            var building = thing as Building;
            if (building != null)
            {
                if (building.def.Minifiable && (building.Faction == pawn.Faction || building.def.building.alwaysUninstallable))
                {
                    var reinstallBp = InstallBlueprintUtility.ExistingBlueprintFor(building);
                    if (reinstallBp != null)
                    {
                        if (TryStartForcedJobWithIgnoreDesignations(building, JobDefOf.Uninstall))
                        {
                            pendingMinifiedPickup = building;
                            return true;
                        }
                    }
                    if (pawn.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall) != null)
                    {
                        if (TryStartForcedJobWithIgnoreDesignations(building, JobDefOf.Uninstall)) return true;
                    }
                }
                if (InteractWith(building)) return true;
            }
            return false;
        }

        private bool TryHandleFloatMenu(IntVec3 clickCell)
        {
            var acceptanceReport = FloatMenuMakerMap.ShouldGenerateFloatMenuForPawn(pawn);
            if (!acceptanceReport.Accepted)
            {
                if (!acceptanceReport.Reason.NullOrEmpty())
                    Messages.Message(acceptanceReport.Reason, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            var clickPos = clickCell.ToVector3Shifted();
            var context = new FloatMenuContext(new List<Pawn> { pawn }, clickPos, pawn.Map);
            bool withinGrabRange = pawn.Position.DistanceTo(clickCell) <= PerspectiveShiftMod.settings.grabRange;

            var opts = new List<FloatMenuOption>();
            FloatMenuMakerMap.makingFor = pawn;

            foreach (var provider in FloatMenuMakerMap.providers)
            {
                if (FloatMenuProviderBlacklist.Contains(provider.GetType())) continue;

                try
                {
                    FloatMenuMakerMap.currentProvider = provider;
                    if (!context.ValidSelectedPawns.Any() || !provider.Applies(context)) continue;

                    opts.AddRange(provider.GetOptions(context));

                    foreach (var thing in context.ClickedThings)
                    {
                        if (!withinGrabRange && !IsWithinInteractionRange(thing)) continue;
                        if (!provider.TargetThingValid(thing, context)) continue;

                        var actualThing = thing;
                        if (thing.TryGetComp(out CompSelectProxy comp) && comp.thingToSelect != null)
                            actualThing = comp.thingToSelect;

                        foreach (var opt in provider.GetOptionsFor(actualThing, context))
                        {
                            if (opt.iconThing == null) opt.iconThing = actualThing;
                            opt.targetsDespawned = !actualThing.Spawned;
                            opts.Add(opt);
                        }
                    }

                    foreach (var clickedPawn in context.ClickedPawns)
                    {
                        if (!withinGrabRange && !IsWithinInteractionRange(clickedPawn)) continue;
                        if (!provider.TargetThingValid(clickedPawn, context)) continue;
                        if (clickedPawn.RaceProps.Humanlike) continue;

                        foreach (var opt in provider.GetOptionsFor(clickedPawn, context))
                        {
                            if (opt.iconThing == null) opt.iconThing = clickedPawn;
                            opt.targetsDespawned = !clickedPawn.Spawned;
                            opts.Add(opt);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[PerspectiveShift] Error in FloatMenuWorker {provider.GetType().Name}: {ex}");
                }
            }

            FloatMenuMakerMap.currentProvider = null;
            FloatMenuMakerMap.makingFor = null;

            var bestOption = opts.Where(opt => !opt.Disabled)
                                 .OrderByDescending(opt => opt.Priority)
                                 .ThenByDescending(opt => opt.Priority != MenuOptionPriority.GoHere)
                                 .FirstOrDefault();
            if (bestOption == null) return false;
            bestOption.action.Invoke();
            return true;
        }

        private bool TryExecuteDesignatorlessFallback(IntVec3 clickCell, bool itemInRange)
        {
            if (!itemInRange) return false;

            var plant = clickCell.GetPlant(pawn.Map);
            if (plant != null
                && plant.HarvestableNow
                && plant.CanYieldNow()
                && pawn.CanReserve(plant)
                && (plant.def.plant.IsTree
                    ? (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting) && PlantUtility.PawnWillingToCutPlant_Job(plant, pawn))
                    : (plant.def.plant.harvestTag == "Standard" && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Growing))))
            {
                var job = JobMaker.MakeJob(JobDefOf.Harvest, plant);
                if (TryStartForcedJob(job)) return true;
            }

            var mineable = clickCell.GetFirstMineable(pawn.Map);
            if (mineable != null && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Mining) && pawn.CanReserve(mineable))
            {
                var job = JobMaker.MakeJob(JobDefOf.Mine, mineable, 20000, checkOverrideOnExpiry: true);
                job.ignoreDesignations = true;
                if (TryStartForcedJob(job)) return true;
            }

            return false;
        }

        public bool InteractWith(Thing target)
        {
            if (target == null || target.Destroyed)
            {
                return false;
            }
            if (TryStartBuildingDeconstructJob(target)) return true;
            if (TryStartMeditationOrReignJob(target)) return true;
            if (TryStartWorkGiverJob(target)) return true;
            if (TryHandleResearchBench(target)) return true;
            if (TryHandleBedRest(target)) return true;
            if (TryHandleJoyBuilding(target)) return true;

            return false;
        }

        private bool TryStartBuildingDeconstructJob(Thing thing)
        {
            if (pawn.Map.designationManager.DesignationOn(thing, DesignationDefOf.Deconstruct) != null)
            {
                if (TryStartForcedJobWithIgnoreDesignations(thing, JobDefOf.Deconstruct)) return true;
            }

            var blueprint = thing.Position.GetThingList(pawn.Map).OfType<Blueprint>().FirstOrDefault();
            if (blueprint != null && GenConstruct.BlocksConstruction(blueprint, thing) && thing is Building building
             && building.DeconstructibleBy(pawn.Faction).Accepted)
            {
                if (TryStartForcedJobWithIgnoreDesignations(thing, JobDefOf.Deconstruct))
                    return true;
            }

            return false;
        }

        private bool TryStartMeditationOrReignJob(Thing thing)
        {
            if (thing.def == ThingDefOf.MeditationSpot || thing is Building_Throne throne1 && throne1.AssignedPawn == pawn)
            {
                Job job;
                if (thing is Building_Throne throne2)
                {
                    job = JobMaker.MakeJob(JobDefOf.Reign, throne2, null, throne2);
                }
                else
                {
                    JobDef def = JobDefOf.Meditate;
                    if (ModsConfig.IdeologyActive && pawn.Ideo != null && pawn.Ideo.foundation is IdeoFoundation_Deity ideoFoundation_Deity && ideoFoundation_Deity.DeitiesListForReading.Any())
                    {
                        def = JobDefOf.MeditatePray;
                    }
                    job = JobMaker.MakeJob(def, thing, null, thing);
                }
                job.ignoreJoyTimeAssignment = true;
                if (TryStartForcedJob(job)) return true;
            }

            return false;
        }

        private bool TryStartWorkGiverJob(Thing target)
        {
            if (pawn.workSettings == null) return false;

            var workGivers = pawn.workSettings.WorkGiversInOrderNormal;
            for (int i = 0; i < workGivers.Count; i++)
            {
                if (workGivers[i] is WorkGiver_Scanner scanner)
                {
                    if (scanner.PotentialWorkThingRequest.Accepts(target))
                    {
                        if (scanner.HasJobOnThing(pawn, target, forced: true))
                        {
                            var job = scanner.JobOnThing(pawn, target, forced: true);
                            if (job != null)
                            {
                                if (job.def == JobDefOf.HaulToContainer && pawn.carryTracker?.CarriedThing == null) continue;
                                if (job.def == JobDefOf.Refuel && pawn.carryTracker?.CarriedThing == null) continue;
                                if (job.def == JobDefOf.RefuelAtomic && pawn.carryTracker?.CarriedThing == null) continue;
                                if (TryStartForcedJob(job)) return true;
                            }
                        }
                        else if (pawn.WorkTypeIsDisabled(scanner.def.workType))
                        {
                            return RejectInteraction("PS_IncapableOfWorkType", scanner.def.workType.labelShort);
                        }
                    }
                }
            }

            return false;
        }

        private bool TryHandleResearchBench(Thing target)
        {
            if (target is Building_ResearchBench && Find.ResearchManager.GetProject() == null)
            {
                return RejectInteraction("PS_NoResearchProject");
            }
            return false;
        }

        private bool TryHandleBedRest(Thing target)
        {
            if (target is Building_Bed bed && !bed.ForPrisoners && !bed.Medical && pawn.needs?.rest != null && RestUtility.CanUseBedEver(pawn, bed.def) && pawn.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Deadly, bed.SleepingSlotsCount, 0))
            {
                if (TryStartForcedJob(JobMaker.MakeJob(JobDefOf.LayDown, bed)))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryHandleJoyBuilding(Thing target)
        {
            if (pawn.needs?.joy == null) return false;

            var joyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading
                .Where(jg => jg.thingDefs != null && jg.thingDefs.Contains(target.def));

            foreach (var jgDef in joyGivers)
            {
                if (jgDef.Worker is JoyGiver_WatchBuilding)
                {
                    var watchCells = WatchBuildingUtility.CalculateWatchCells(
                        target.def, target.Position, target.Rotation, pawn.Map);
                    if (watchCells.Contains(pawn.Position))
                    {
                        var chair = pawn.Position.GetEdifice(pawn.Map);
                        if (chair != null && !chair.def.building.isSittable) chair = null;
                        var job = JobMaker.MakeJob(jgDef.jobDef, target, pawn.Position, chair);
                        job.ignoreJoyTimeAssignment = true;
                        if (TryStartForcedJob(job)) return true;
                    }
                    continue;
                }
                if (jgDef.Worker is JoyGiver_InteractBuilding worker)
                {
                    var job = worker.TryGivePlayJob(pawn, target);
                    if (job != null && TryStartForcedJob(job)) return true;
                }
            }
            return false;
        }

        private bool RejectInteraction(string messageKey, params NamedArgument[] args)
        {
            Messages.Message(messageKey.Translate(args), MessageTypeDefOf.RejectInput, false);
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return true;
        }

        private bool IsTargetInRange(LocalTargetInfo target)
        {
            if (!target.IsValid) return true;
            IntVec3 cell = target.Cell;
            if (pawn.Position.DistanceTo(cell) <= PerspectiveShiftMod.settings.grabRange ||
                cell.AdjacentTo8WayOrInside(pawn.Position))
                return true;
            if (target.Thing != null)
                return IsWithinInteractionRange(target.Thing);
            return false;
        }

        private bool IsWithinInteractionRange(Thing thing)
        {
            if (thing.def.hasInteractionCell && thing.InteractionCell == pawn.Position)
                return true;

            if (thing.def.building?.watchBuildingStandDistanceRange != null)
            {
                var watchCells = WatchBuildingUtility.CalculateWatchCells(thing.def, thing.Position, thing.Rotation, pawn.Map);
                if (watchCells.Contains(pawn.Position))
                    return true;
            }

            if (thing.def.size.x > 1 || thing.def.size.z > 1)
            {
                foreach (var c in thing.OccupiedRect())
                {
                    if (pawn.Position.DistanceTo(c) <= PerspectiveShiftMod.settings.grabRange)
                        return true;
                }
            }

            return false;
        }

        private bool JobTargetsInRange(Job job)
        {
            if (job == null) return true;

            if (job.targetA.IsValid && !IsTargetInRange(job.targetA)) return false;
            if (job.targetB.IsValid && !IsTargetInRange(job.targetB)) return false;
            if (job.targetC.IsValid && !IsTargetInRange(job.targetC)) return false;

            if (job.targetQueueA != null)
                foreach (var t in job.targetQueueA)
                    if (t.IsValid && !IsTargetInRange(t)) return false;

            if (job.targetQueueB != null)
                foreach (var t in job.targetQueueB)
                    if (t.IsValid && !IsTargetInRange(t)) return false;

            return true;
        }

        private bool TryStartForcedJob(Job job)
        {
            if (job == null) return false;
            if (!JobTargetsInRange(job)) return false;
            job.playerForced = true;
            return pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private bool TryStartForcedJobWithIgnoreDesignations(Thing thing, JobDef jobDef)
        {
            var job = JobMaker.MakeJob(jobDef, thing);
            job.ignoreDesignations = true;
            return TryStartForcedJob(job);
        }
    }
}
