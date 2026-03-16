using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;
using System;

namespace PerspectiveShift
{
    [StaticConstructorOnStartup]
    [HotSwappable]
    public class Avatar : IExposable
    {
        public static bool IsAvatarLeftClick = false;
        public Pawn pawn;
        public Lord savedLord;
        public Vector3 moveInput;
        public bool isSprinting;
        public bool isWalking;
        public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
        public static bool DrawingAvatarNeeds = false;
        public Vector3? physicsPosition;
        public Vector3 LeanTarget = Vector3.zero;
        public Vector3 LeanSmoothed = Vector3.zero;
        private float moveInputDuration = 0f;
        private bool wasSprinting = false;
        private Vector3 lastVehicleMoveInput = Vector3.zero;
        private int vehicleStopGraceTicks = 0;
        private int vehiclePathFailCooldown = 0;
        public float aimAngle = -1f;
        private Vector3 _leanVelocity = Vector3.zero;

        public Thing CarriedThing => pawn.carryTracker?.CarriedThing;
        public Thing LastManualTarget;

        private Building pendingMinifiedPickup;
        private Building_Door interactingDoor;
        private bool wasMovingLastFrame;
        private Rect gizmoBounds;
        private List<object> prevSelected;
        private static Texture2D _reticleTex;
        public static Texture2D ReticleTex => _reticleTex ??= ContentFinder<Texture2D>.Get("UI/Reticle");
        private static Texture2D _reticleCooldownTex;
        public static Texture2D ReticleCooldownTex => _reticleCooldownTex ??= ContentFinder<Texture2D>.Get("UI/ReticleCooldown");
        private static Texture2D _reticleNoLOSTex;
        public static Texture2D ReticleNoLOSTex => _reticleNoLOSTex ??= ContentFinder<Texture2D>.Get("UI/ReticleNoLOS");

        public Avatar() { }
        public Avatar(Pawn pawn)
        {
            this.pawn = pawn;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn", saveDestroyedThings: true);
            Scribe_References.Look(ref interactingDoor, "interactingDoor");
            Scribe_References.Look(ref savedLord, "savedLord");
            Scribe_References.Look(ref pendingMinifiedPickup, "pendingMinifiedPickup");
        }

        public void UpdatePhysics()
        {
            if (pawn.Downed)
            {
                physicsPosition = null;
                wasMovingLastFrame = false;
                return;
            }

            var inVehicle = ModCompatibility.IsPawnInVehicle(pawn, out Pawn vehicle, out bool isDriver, out bool isGunner);

            if (inVehicle)
            {
                if (isDriver)
                {
                    if (State.ControlsFrozen || State.CameraLockPosition.HasValue) moveInput = Vector3.zero;
                    else UpdateInput();

                    if (vehiclePathFailCooldown > 0) vehiclePathFailCooldown--;

                    if (IsMoving)
                    {
                        vehicleStopGraceTicks = 10;
                        if (vehiclePathFailCooldown <= 0)
                        {
                            var success = ModCompatibility.ProcessVehicleMovement(vehicle, moveInput);
                            if (!success)
                            {
                                vehiclePathFailCooldown = 5;
                            }
                            else
                            {
                                lastVehicleMoveInput = moveInput;
                            }
                        }
                    }
                    else if (lastVehicleMoveInput != Vector3.zero)
                    {
                        if (vehicleStopGraceTicks > 0)
                        {
                            vehicleStopGraceTicks--;
                        }
                        else
                        {
                            ModCompatibility.StopVehicle(vehicle);
                            lastVehicleMoveInput = Vector3.zero;
                            vehiclePathFailCooldown = 0;
                        }
                    }
                }
                physicsPosition = null;
                wasMovingLastFrame = false;
                return;
            }

            if (!pawn.Spawned || pawn.Map == null)
            {
                physicsPosition = null;
                wasMovingLastFrame = false;
                return;
            }

            if (pawn.InMentalState)
            {
                moveInput = Vector3.zero;
                if (wasMovingLastFrame)
                {
                    if (pawn.pather?.curPath != null) pawn.pather.StopDead();
                    wasMovingLastFrame = false;
                }
                physicsPosition = null;
                return;
            }

            if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def.HasModExtension<JobRequiresHoldExtension>())
            {
                if (!Input.GetMouseButton(0))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }

            bool inCombatStance = pawn.stances.curStance is Stance_Warmup || pawn.stances.curStance is Stance_Cooldown;
            var canRunAndGun = ModCompatibility.IsRunAndGunActiveFor(pawn);
            bool isShootingStationary = inCombatStance && !canRunAndGun;

            if (State.ControlsFrozen || isShootingStationary || State.CameraLockPosition.HasValue)
            {
                moveInput = Vector3.zero;
            }
            else
            {
                UpdateInput();
            }

            if (!pawn.Awake() && (IsMoving || moveInputDuration > 0f))
            {
                RestUtility.WakeUp(pawn);
            }

            if (moveInput == Vector3.zero && !pawn.Position.Walkable(pawn.Map))
            {
                IntVec3 best = IntVec3.Invalid;
                foreach (var adj in GenAdj.AdjacentCells)
                {
                    IntVec3 c = pawn.Position + adj;
                    if (c.InBounds(pawn.Map) && c.Walkable(pawn.Map))
                    {
                        best = c;
                        break;
                    }
                }

                if (best.IsValid)
                {
                    Vector3 dir = (best.ToVector3Shifted() - pawn.Position.ToVector3Shifted()).normalized;
                    moveInput = dir;
                }
            }

            ProcessMovement();
        }

        public void UpdateCamera()
        {
            if (pawn == null) return;

            var driver = Find.CameraDriver;
            if (driver == null) return;

            var sizeRange = new FloatRange(PerspectiveShiftMod.settings.minZoom, PerspectiveShiftMod.settings.maxZoom);
            driver.config.sizeRange = sizeRange;
            driver.config.zoomSpeed = PerspectiveShiftMod.settings.zoomSpeed * 10f;

            Vector3 targetCamPos = Vector3.zero;
            if (pawn.Map == Find.CurrentMap && pawn.Spawned)
            {
                targetCamPos = State.CameraLockPosition
                    ?? physicsPosition
                    ?? pawn.Position.ToVector3ShiftedWithAltitude(pawn.def.Altitude);
            }
            else
            {
                var container = State.TryGetSpawnedContainer(pawn);
                if (container != null && container.Map == Find.CurrentMap)
                    targetCamPos = container.DrawPos;
                else
                    return;
            }

            var newPos = Vector3.Lerp(driver.rootPos, targetCamPos, 0.1f);

            driver.rootPos = newPos;

            var cam = driver.GetComponent<Camera>();
            if (cam != null)
            {
                Vector3 finalPos = newPos;
                float rangeSpan = sizeRange.max - sizeRange.min;
                if (rangeSpan <= 0.01f) rangeSpan = 0.01f;
                finalPos.y = 15f + (driver.RootSize - sizeRange.min) / rangeSpan * 50f;
                cam.transform.position = finalPos + driver.shaker.ShakeOffset;
                cam.orthographicSize = driver.RootSize;
            }
        }

        public bool HandleSelectorClick()
        {
            if (Find.Targeter.IsTargeting) return false;
            if (pawn.Downed) return false;
            if (pawn.InMentalState) return false;

            if (ModCompatibility.IsPawnInVehicle(pawn, out Pawn veh, out bool isDriver, out bool isGunner))
            {
                if (isGunner && Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0)
                    {
                        ModCompatibility.FireVehicleWeapons(veh, pawn, UI.MouseMapPosition());
                        Event.current.Use();
                        return true;
                    }
                    else if (Event.current.button == 1)
                    {
                        ModCompatibility.ClearVehicleWeapons(veh, pawn);
                        Event.current.Use();
                        return true;
                    }
                }
                return false;
            }

            if (Find.TickManager.Paused) return false;
            if (State.CameraLockPosition.HasValue) return false;

            if (IsMouseOverUI() || IsMouseOverColonistBar()) return false;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (pawn.Drafted)
                {
                    if (MouseIsOverPawn()) return false;

                    HandleFiring();
                    return true;
                }
                else
                {
                    return HandleLeftClick();
                }
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                if (pawn.carryTracker?.CarriedThing != null && pawn.inventory != null)
                {
                    var carried = pawn.carryTracker.CarriedThing;
                    int count = carried.stackCount;

                    if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, carried, count))
                    {
                        var maxCount = MassUtility.CountToPickUpUntilOverEncumbered(pawn, carried);
                        if (maxCount <= 0)
                        {
                            Messages.Message("PS_CannotCarryMoreWeight".Translate(), MessageTypeDefOf.RejectInput, false);
                            Event.current.Use();
                            return true;
                        }
                        count = maxCount;
                    }

                    var transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(carried, pawn.inventory.innerContainer, count);
                    if (transferred > 0)
                    {
                        DefsOf.PS_PackInventory.PlayOneShotOnCamera();
                        Event.current.Use();
                        return true;
                    }
                }

                if (pawn.Drafted)
                {
                    var otherPawnsSelected = Find.Selector.SelectedObjects
                        .Any(o => o is Pawn p && p != pawn);
                    if (otherPawnsSelected)
                        return false;

                    if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def.playerInterruptible)
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);

                    Event.current.Use();
                    return true;
                }

                if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def.playerInterruptible)
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            return false;
        }

        private void UpdateInput()
        {
            moveInput = Vector3.zero;

            if (DefsOf.PS_MoveForward.IsDown) moveInput += Vector3.forward;
            if (DefsOf.PS_MoveBack.IsDown) moveInput += Vector3.back;
            if (DefsOf.PS_MoveLeft.IsDown) moveInput += Vector3.left;
            if (DefsOf.PS_MoveRight.IsDown) moveInput += Vector3.right;

            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

            isSprinting = PerspectiveShiftMod.settings.enableSprinting && DefsOf.PS_Sprint.IsDown;
            isWalking = PerspectiveShiftMod.settings.enableSneaking && DefsOf.PS_Walk.IsDown;

            if (isSprinting && !wasSprinting)
            {
                DefsOf.PS_SprintSound.PlayOneShotOnCamera();
            }
            wasSprinting = isSprinting;
        }

        public void RenderPawn()
        {
            if (pawn.Map == null || !pawn.Spawned) return;

            LeanSmoothed = Vector3.SmoothDamp(LeanSmoothed, LeanTarget, ref _leanVelocity, 0.07f, 10f, Time.deltaTime);

            if (pawn.Drawer?.leaner != null)
            {
                IntVec3 snapped = IntVec3.Zero;
                if (LeanTarget.sqrMagnitude > 0.01f)
                {
                    snapped = Mathf.Abs(LeanTarget.x) >= Mathf.Abs(LeanTarget.z)
                        ? (LeanTarget.x > 0 ? IntVec3.East : new IntVec3(-1, 0, 0))
                        : (LeanTarget.z > 0 ? IntVec3.North : IntVec3.South);
                }
                pawn.Drawer.leaner.shootSourceOffset = snapped;
            }

            if (!physicsPosition.HasValue) return;
            var tweener = pawn.Drawer.tweener;
            tweener.tweenedPos = physicsPosition.Value;
            tweener.lastDrawFrame = RealTime.frameCount;
            tweener.lastDrawTick = GenTicks.TicksGame;
        }

        private float GetMovementSpeedMultiplier(IntVec3 cell)
        {
            if (!cell.InBounds(pawn.Map)) return 1f;

            float num = pawn.TicksPerMoveCardinal;

            var pawnCellBaseCostOverride = Pawn_PathFollower.GetPawnCellBaseCostOverride(pawn, cell);
            num += pawn.Map.pathing.For(pawn).pathGrid.CalculatedCostAt(cell, false, pawn.Position, pawnCellBaseCostOverride);

            var edifice = cell.GetEdifice(pawn.Map);
            if (edifice != null)
            {
                num += (float)(int)edifice.PathWalkCostFor(pawn);
            }

            var terrain = cell.GetTerrain(pawn.Map);
            if (terrain != null && terrain.tags != null && pawn.kindDef.moveSpeedFactorByTerrainTag != null)
            {
                foreach (var tag in terrain.tags)
                {
                    if (pawn.kindDef.moveSpeedFactorByTerrainTag.TryGetValue(tag, out var value))
                    {
                        num /= value;
                    }
                }
            }

            if (num > 450f) num = 450f;

            return pawn.TicksPerMoveCardinal / Mathf.Max(num, 1f);
        }

        private void RotateTowardsMouse()
        {
            var pawnCenter = pawn.Position.ToVector3Shifted();
            Vector3 toMouse = UI.MouseMapPosition() - pawnCenter;
            if (toMouse.sqrMagnitude > 0.1f)
                pawn.Rotation = Rot4.FromAngleFlat(NormAngle(Mathf.Atan2(toMouse.x, toMouse.z) * Mathf.Rad2Deg));
        }

        private void ProcessMovement()
        {
            if (interactingDoor != null) return;

            if (!pawn.Drafted && (pawn.GetLord() != null || pawn.mindState?.duty != null))
            {
                physicsPosition = null;
                return;
            }

            if (!IsMoving)
            {
                moveInputDuration = 0f;
                if (wasMovingLastFrame)
                {
                    if (pawn.pather.curPath != null) pawn.pather.StopDead();
                    wasMovingLastFrame = false;
                }

                if (pawn.pather.curPath != null)
                {
                    physicsPosition = null;
                }

                if (physicsPosition.HasValue && physicsPosition.Value.ToIntVec3() != pawn.Position)
                {
                    physicsPosition = null;
                }

                if (pawn.Drafted && !pawn.InMentalState && !State.ControlsFrozen && !(pawn.stances.curStance is Stance_Busy))
                {
                    RotateTowardsMouse();
                }

                return;
            }

            moveInputDuration += Time.deltaTime;

            if (!wasMovingLastFrame && physicsPosition.HasValue)
            {
                if (physicsPosition.Value.ToIntVec3() != pawn.Position)
                {
                    State.Warning($"Physics desync detected: physicsPosition={physicsPosition.Value.ToIntVec3()}, pawn.Position={pawn.Position} | Resetting physicsPosition");
                    physicsPosition = null;
                }
            }

            if (!physicsPosition.HasValue)
            {
                physicsPosition = pawn.Position.ToVector3ShiftedWithAltitude(pawn.def.Altitude);
            }

            float distSq = (physicsPosition.Value.ToIntVec3() - pawn.Position).LengthHorizontalSquared;
            if (distSq > 2.25f)
            {
                physicsPosition = pawn.Position.ToVector3ShiftedWithAltitude(pawn.def.Altitude);
            }

            if (!wasMovingLastFrame)
            {
                wasMovingLastFrame = true;
            }

            if (pawn.pather != null && pawn.pather.curPath != null) pawn.pather.StopDead();

            bool isShooting = pawn.stances.curStance is Stance_Warmup || pawn.stances.curStance is Stance_Cooldown;
            var canRunAndGun = ModCompatibility.IsRunAndGunActiveFor(pawn);

            if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def.playerInterruptible)
            {
                bool isOurWaitJob = (pawn.jobs.curJob.def == JobDefOf.Wait && pawn.jobs.curJob.expiryInterval == 60)
                                    || pawn.jobs.curJob.def == JobDefOf.Wait_Combat;
                if (!isOurWaitJob && moveInputDuration > 0.35f)
                {
                    if (!(canRunAndGun && isShooting && pawn.Drafted))
                    {
                        HandleAbilityCancellation(pawn.jobs.curJob);
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }

            Vector3 deltaRaw = moveInput.normalized;
            Vector3 testNewPos = physicsPosition.Value + deltaRaw * 0.1f;
            var currentMultiplier = GetMovementSpeedMultiplier(pawn.Position);
            var futureMultiplier = GetMovementSpeedMultiplier(testNewPos.ToIntVec3());
            var terrainMultiplier = Mathf.Min(currentMultiplier, futureMultiplier);
            var baseSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed);
            float speed = baseSpeed * PerspectiveShiftMod.settings.moveSpeedMultiplier * terrainMultiplier * (isSprinting ? PerspectiveShiftMod.settings.sprintSpeedMultiplier : isWalking ? PerspectiveShiftMod.settings.sneakSpeedMultiplier : 1.0f) * Time.deltaTime * Find.TickManager.TickRateMultiplier;
            speed = Mathf.Min(speed, 0.8f);
            Vector3 newPos = physicsPosition.Value;

            float distanceRemaining = speed;
            while (distanceRemaining > 0)
            {
                var step = Mathf.Min(distanceRemaining, 0.05f);
                distanceRemaining -= step;

                Vector3 stepDelta = deltaRaw * step;
                var currentlyWalkable = IsWalkableWithMargin(newPos);
                var safePos = pawn.Position.ToVector3Shifted();

                if (Mathf.Abs(stepDelta.x) > 0.0001f)
                {
                    Vector3 testX = newPos + new Vector3(stepDelta.x, 0, 0);
                    if (IsWalkableWithMargin(testX)) newPos = testX;
                    else if (!currentlyWalkable)
                    {
                        if ((testX - safePos).sqrMagnitude < (newPos - safePos).sqrMagnitude) newPos = testX;
                    }
                }

                if (Mathf.Abs(stepDelta.z) > 0.0001f)
                {
                    Vector3 testZ = newPos + new Vector3(0, 0, stepDelta.z);
                    if (IsWalkableWithMargin(testZ)) newPos = testZ;
                    else if (!currentlyWalkable)
                    {
                        if ((testZ - safePos).sqrMagnitude < (newPos - safePos).sqrMagnitude) newPos = testZ;
                    }
                }
            }

            var nextCell = newPos.ToIntVec3();
            if (nextCell.InBounds(pawn.Map) && nextCell.OnEdge(pawn.Map) && pawn.Map.exitMapGrid.IsExitCell(nextCell) && !pawn.Position.OnEdge(pawn.Map))
            {
                pawn.ExitMap(true, pawn.Rotation);
                return;
            }

            var door = nextCell.GetDoor(pawn.Map);
            if (door != null && !door.Open && door.PawnCanOpen(pawn))
            {
                door.StartManualOpenBy(pawn);
                pawn.Map.fogGrid.Notify_PawnEnteringDoor(door, pawn);
                interactingDoor = door;
                return;
            }

            physicsPosition = newPos;
            if (pawn.Position != nextCell)
            {
                pawn.Position = nextCell;
                pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
                pawn.pather.nextCell = nextCell;
            }

            if (pawn.Drawer?.leaner != null && !(pawn.stances.curStance is Stance_Busy))
            {
                LeanTarget = Vector3.zero;
            }

            if (!(pawn.stances.curStance is Stance_Busy))
            {
                if (pawn.Drafted)
                {
                    RotateTowardsMouse();
                }
                else
                {
                    bool doingJob = pawn.jobs?.curJob != null && pawn.jobs.curJob.def != JobDefOf.Wait && pawn.jobs.curJob.def != JobDefOf.Wait_Combat;
                    if (!doingJob)
                    {
                        UpdateRotation(moveInput.normalized);
                    }
                }
            }
        }

        private void HandleAbilityCancellation(Job curJob)
        {
            string abilityName = null;
            if (curJob?.verbToUse is IAbilityVerb av && av.Ability != null)
            {
                abilityName = av.Ability.def.LabelCap;
            }
            else
            {
                abilityName = ModCompatibility.GetVEFAbilityName(pawn);
            }
            if (!string.IsNullOrEmpty(abilityName))
            {
                Messages.Message("PS_AbilityCancelled".Translate(abilityName), MessageTypeDefOf.RejectInput, false);
            }
        }

        private bool IsWalkableWithMargin(Vector3 pos)
        {
            float margin = 0.15f;
            var cCenter = pos.ToIntVec3();

            if (!IsWalkableCell(cCenter)) return false;

            var c1 = new Vector3(pos.x + margin, pos.y, pos.z + margin).ToIntVec3();
            if (c1 != cCenter && !IsWalkableCell(c1)) return false;

            var c2 = new Vector3(pos.x - margin, pos.y, pos.z + margin).ToIntVec3();
            if (c2 != cCenter && !IsWalkableCell(c2)) return false;

            var c3 = new Vector3(pos.x + margin, pos.y, pos.z - margin).ToIntVec3();
            if (c3 != cCenter && !IsWalkableCell(c3)) return false;

            var c4 = new Vector3(pos.x - margin, pos.y, pos.z - margin).ToIntVec3();
            if (c4 != cCenter && !IsWalkableCell(c4)) return false;

            return true;
        }

        private bool IsWalkableCell(IntVec3 cell)
        {
            if (!cell.InBounds(pawn.Map)) return false;

            var door = cell.GetDoor(pawn.Map);
            if (door != null && !door.Open && !door.PawnCanOpen(pawn)) return false;

            var edifice = cell.GetEdifice(pawn.Map);
            if (edifice != null && edifice.def.passability == Traversability.Impassable) return false;

            if (!cell.Walkable(pawn.Map))
            {
                if (edifice != null && edifice.def.building != null && edifice.def.building.isFence) return true;
                return false;
            }

            return true;
        }

        private void UpdateRotation(Vector3 dir)
        {
            if (dir.x < -0.1f && dir.z > 0.1f) pawn.Rotation = Rot4.West;
            else if (dir.x > 0.1f && dir.z < -0.1f) pawn.Rotation = Rot4.East;
            else if (dir.x > 0.1f && dir.z > 0.1f) pawn.Rotation = Rot4.East;
            else if (dir.x < -0.1f && dir.z < -0.1f) pawn.Rotation = Rot4.West;
            else
            {
                var angle = NormAngle(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
                pawn.Rotation = Rot4.FromAngleFlat(angle);
            }
        }

        private static float NormAngle(float a)
        {
            while (a < 0f) a += 360f;
            while (a >= 360f) a -= 360f;
            return a;
        }

        private void HandleDoorInteraction()
        {
            if (interactingDoor == null) return;
            if (interactingDoor.Open || interactingDoor.Destroyed)
                interactingDoor = null;
        }

        private void HandleCombatStance()
        {
            if (!pawn.Drafted || pawn.stances.curStance == null) return;

            bool isMoving = IsMoving;

            if (isMoving)
            {
                var rgActive = ModCompatibility.IsRunAndGunActiveFor(pawn, out string rgReason);
                if (rgActive)
                {
                    var stance = pawn.stances.curStance;
                    if (stance is Stance_Warmup warmup && stance.GetType() != ModCompatibility.stanceRunAndGunType)
                    {
                        ModCompatibility.ConvertToRunAndGunStance(pawn, warmup);
                    }
                    else if (stance is Stance_Cooldown cooldown && stance.GetType() != ModCompatibility.stanceRunAndGunCooldownType)
                    {
                        ModCompatibility.ConvertToRunAndGunCooldownStance(pawn, cooldown);
                    }
                    return;
                }

            }
            else
            {
                if (ModCompatibility.IsRunAndGunActiveFor(pawn))
                {
                    var stance = pawn.stances.curStance;
                    if (ModCompatibility.stanceRunAndGunType != null && stance.GetType() == ModCompatibility.stanceRunAndGunType && stance is Stance_Warmup warmup)
                    {
                        ModCompatibility.ConvertToVanillaWarmupStance(pawn, warmup);
                    }
                    else if (ModCompatibility.stanceRunAndGunCooldownType != null && stance.GetType() == ModCompatibility.stanceRunAndGunCooldownType && stance is Stance_Cooldown cooldown)
                    {
                        ModCompatibility.ConvertToVanillaCooldownStance(pawn, cooldown);
                    }
                }
            }
        }

        public void OnGUI()
        {
            DebugLog();
            if (State.CameraLockPosition.HasValue)
            {
                float btnW = 220f;
                float panelH = 70f;
                float panelY = UI.screenHeight - 150f - panelH;
                var panelRect = new Rect(UI.screenWidth / 2f - btnW / 2f - 10f, panelY, btnW + 20f, panelH);
                Widgets.DrawWindowBackground(panelRect);
                var btnRect = new Rect(panelRect.xMin + 10f, panelRect.yMin + 10f, btnW, 50f);
                if (Widgets.ButtonText(btnRect, "PS_ReturnToCharacter".Translate()))
                {
                    State.CameraLockPosition = null;
                }
            }
            if (!pawn.InMentalState)
            {
                DrawPlayerGizmos();
                DrawNeeds();
            }
            bool mouseOverGizmo = MapGizmoUtility.LastMouseOverGizmo != null || gizmoBounds.Contains(Event.current.mousePosition);
            bool mouseOverUI = IsMouseOverUI() || IsMouseOverColonistBar();

            TryToggleInspectTab(DefsOf.PS_OpenGearTab, typeof(ITab_Pawn_Gear));
            TryToggleInspectTab(DefsOf.PS_HealthTab, typeof(ITab_Pawn_Health));
            TryToggleInspectTab(DefsOf.PS_NeedsTab, typeof(ITab_Pawn_Needs));

            if (DefsOf.PS_EatFood.KeyDownEvent)
            {
                bool onlyAvatarSelected = Find.Selector.NumSelected == 0 || (Find.Selector.NumSelected == 1 && Find.Selector.IsSelected(pawn));

                if (onlyAvatarSelected && !pawn.Downed && !pawn.InMentalState && pawn.needs?.food != null)
                {
                    FoodPreferability foodPreferability = FoodPreferability.Undefined;
                    bool allowCorpse = false;

                    if (pawn.AnimalOrWildMan())
                    {
                        allowCorpse = true;
                    }
                    else
                    {
                        Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
                        if (firstHediffOfDef != null && firstHediffOfDef.Severity > 0.4f)
                        {
                            allowCorpse = true;
                        }
                    }

                    if (pawn.IsMutant && pawn.mutant.Def.allowEatingCorpses)
                    {
                        foodPreferability = FoodPreferability.DesperateOnly;
                        allowCorpse = true;
                    }

                    bool desperate = pawn.needs.food.CurCategory == HungerCategory.Starving;

                    Thing foodSource = null;
                    ThingDef foodDef = null;
                    FoodUtility.TryFindBestFoodSourceFor(pawn, pawn, desperate, out foodSource, out foodDef, canRefillDispenser: true, canUseInventory: true, canUsePackAnimalInventory: true, allowForbidden: false, allowCorpse, allowSociallyImproper: false, pawn.IsWildMan(), forceScanWholeMap: true, ignoreReservations: false, calculateWantedStackCount: false, allowVenerated: false, minPrefOverride: foodPreferability);

                    if (foodSource != null && Toils_Ingest.TryFindChairOrSpot(pawn, foodSource, out var _))
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Ingest, foodSource);
                        job.count = FoodUtility.WillIngestStackCountOf(pawn, foodDef, FoodUtility.NutritionForEater(pawn, foodSource));
                        job.playerForced = true;
                        pawn.jobs.TryTakeOrderedJob(job);
                        Messages.Message("PS_EatingFood".Translate(foodSource.LabelCap), pawn, MessageTypeDefOf.TaskCompletion, false);
                        Event.current.Use();
                    }
                }
            }

            if (pawn.Drafted && !pawn.InMentalState)
            {
                if (!Find.TickManager.Paused && Find.Selector.IsSelected(pawn) && !Find.Targeter.IsTargeting)
                {
                    Find.Selector.Deselect(pawn);
                }

                if (!Find.TickManager.Paused && !State.ControlsFrozen && !(pawn.stances.curStance is Stance_Busy))
                {
                    Vector3 toMouse = UI.MouseMapPosition() - pawn.DrawPos;
                    toMouse.y = 0f;
                    if (toMouse.sqrMagnitude >= 0.01f)
                    {
                        aimAngle = Mathf.Atan2(toMouse.x, toMouse.z) * Mathf.Rad2Deg;
                        if (aimAngle < 0f) aimAngle += 360f;
                    }
                }

                if (mouseOverUI || mouseOverGizmo || Find.TickManager.Paused || State.ControlsFrozen || Find.Targeter.IsTargeting)
                {
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.visible = false;
                    DrawReticle(Event.current.mousePosition);
                }
            }
            else
            {
                Cursor.visible = true;
            }
        }

        private void TryToggleInspectTab(KeyBindingDef keyDef, Type tabType)
        {
            if (!keyDef.KeyDownEvent) return;
            if (Find.DesignatorManager.SelectedDesignator != null) return;

            if (Find.Selector.SingleSelectedThing != pawn)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(pawn);
            }
            if (Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect)
            {
                Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect);
            }

            var inspectPane = (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
            if (inspectPane != null)
            {
                var tab = inspectPane.CurTabs.FirstOrDefault(t => t.GetType() == tabType);
                if (tab != null)
                {
                    if (inspectPane.OpenTabType == tabType)
                    {
                        inspectPane.CloseOpenTab();
                        Find.Selector.Deselect(pawn);
                    }
                    else
                        inspectPane.OpenTabType = tabType;
                }
            }
            Event.current.Use();
        }

        private void DebugLog()
        {
            bool shouldLogMovement = false;
            if (shouldLogMovement)
            {
                bool desyncDetected = physicsPosition.HasValue && physicsPosition.Value.ToIntVec3() != pawn.Position;
                string desyncMsg = desyncDetected ? " [DESYNC]" : "";
                State.Message($"Frame={Time.frameCount} " +
                    $"physPos={physicsPosition} " +
                    $"pawn.Pos={pawn.Position} " +
                    $"DrawPos={pawn.DrawPos} " +
                    $"LeanSmoothed={LeanSmoothed} " +
                    $"LeanTarget={LeanTarget} " +
                    $"camPos={Find.CameraDriver?.rootPos} " +
                    $"mouseUI={UI.MousePositionOnUI} " +
                    $"mouseCell={UI.MouseCell()} " +
                    $"IsMoving={IsMoving} " +
                    $"paused={Find.TickManager.Paused}" +
                    desyncMsg);
            }
            bool shouldLogJobs = true;
            if (shouldLogJobs)
            {
                pawn.jobs.debugLog = true;
            }
            bool shouldLogUI = false;
            if (shouldLogUI)
            {
                State.Message($"Windows: {string.Join(", ", Find.WindowStack.windows.Select(x => x.GetType().Name))} | OpenTab: {Find.MainTabsRoot.OpenTab?.defName ?? "null"}");
            }
        }

        private bool IsMouseOverUI()
        {
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 mouseInverted = UI.MousePositionOnUIInverted;

            if (gizmoBounds.Contains(mousePos))
                return true;

            if (Find.WindowStack.GetWindowAt(mouseInverted) != null)
                return true;

            if (GizmoGridDrawer.HeightDrawnRecently > 0 && mouseInverted.y <= GizmoGridDrawer.HeightDrawnRecently)
                return true;

            if (mouseInverted.y <= 35f)
                return true;

            if (Find.Selector.NumSelected > 0 && mouseInverted.y <= 165f && mousePos.x <= 432f)
                return true;

            if (Find.MainTabsRoot.OpenTab != null)
            {
                var window = Find.MainTabsRoot.OpenTab.TabWindow;
                if (window != null && window.windowRect.Contains(mousePos))
                    return true;
            }

            if (Find.MainTabsRoot.OpenTab == MainButtonDefOf.Architect)
            {
                if (ArchitectCategoryTab.InfoRect.Contains(mouseInverted))
                    return true;
            }

            return false;
        }

        private bool IsMouseOverColonistBar()
        {
            if (Find.ColonistBar == null) return false;
            var entries = Find.ColonistBar.Entries;
            var drawLocs = Find.ColonistBar.DrawLocs;
            var size = Find.ColonistBar.Size;
            Vector2 mousePos = Event.current.mousePosition;
            for (int i = 0; i < entries.Count && i < drawLocs.Count; i++)
            {
                var rect = new Rect(drawLocs[i].x, drawLocs[i].y, size.x, size.y);
                if (rect.Contains(mousePos))
                    return true;
            }
            return false;
        }

        private bool MouseIsOverPawn()
        {
            if (pawn == null || !pawn.Spawned) return false;
            return UI.MouseCell() == pawn.Position;
        }

        private void DrawReticle(Vector2 center)
        {
            Color color = Color.white;
            Texture2D tex = ReticleTex;

            if (pawn.stances.curStance is Stance_Cooldown)
            {
                color = new Color(1f, 0.65f, 0f);
                tex = ReticleCooldownTex;
            }
            if (pawn.stances.curStance is Stance_Busy)
            {
                Color prevColor = GUI.color;
                GUI.color = pawn.stances.curStance is Stance_Cooldown
                    ? new Color(1f, 0.65f, 0f) : Color.white;
                float reticleSize = 32f;
                var reticleRect = new Rect(center.x - reticleSize / 2f, center.y - reticleSize / 2f, reticleSize, reticleSize);
                Texture2D reticleTex = pawn.stances.curStance is Stance_Cooldown ? ReticleCooldownTex : ReticleTex;
                if (reticleTex != null) GUI.DrawTexture(reticleRect, reticleTex);
                GUI.color = prevColor;
                return;
            }
            else
            {
                Verb verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
                if (verb == null || verb.verbProps.IsMeleeAttack)
                    verb = pawn.VerbTracker?.AllVerbs?.FirstOrDefault(v => v is Verb_MeleeAttack && v.Available());

                if (verb != null)
                {
                    var targetCell = UI.MouseCell();
                    if (!targetCell.InBounds(pawn.Map))
                    {
                        LeanTarget = Vector3.zero;
                        return;
                    }
                    Thing bestTarget = targetCell.GetThingList(pawn.Map)
                        ?.FirstOrDefault(t => t is Pawn && t != pawn)
                        ?? targetCell.GetThingList(pawn.Map)
                            ?.FirstOrDefault(t => t.def.category == ThingCategory.Building);
                    LocalTargetInfo target = bestTarget != null
                        ? new LocalTargetInfo(bestTarget)
                        : new LocalTargetInfo(targetCell);

                    var canHit = verb.CanHitTarget(target);

                    if (!canHit)
                    {
                        color = Color.red;
                        tex = ReticleNoLOSTex;
                        if (!IsMoving)
                            LeanTarget = Vector3.zero;
                    }
                    else
                    {
                        color = Color.green;
                        tex = ReticleTex;

                        if (!IsMoving)
                        {
                            var leanSources = new List<IntVec3>();
                            ShootLeanUtility.LeanShootingSourcesFromTo(pawn.Position, targetCell, pawn.Map, leanSources);
                            var bestLeanSource = leanSources
                                .Where(s => s != pawn.Position && s.IsValid && s != IntVec3.Zero && GenSight.LineOfSight(s, targetCell, pawn.Map, skipFirstCell: true))
                                .OrderBy(s => s.DistanceToSquared(targetCell))
                                .FirstOrDefault();
                            LeanTarget = (bestLeanSource != IntVec3.Zero && bestLeanSource != pawn.Position)
                                ? (bestLeanSource - pawn.Position).ToVector3()
                                : Vector3.zero;
                        }
                    }
                }
                else
                {
                    color = Color.green;
                }
            }

            Color prev = GUI.color;
            GUI.color = color;
            float size = 32f;
            var rect = new Rect(center.x - size / 2f, center.y - size / 2f, size, size);
            if (tex != null) GUI.DrawTexture(rect, tex);
            GUI.color = prev;
        }

        private void HandleFiring()
        {
            if (pawn.stances.curStance is Stance_Busy) return;

            Verb verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
            if (verb == null || verb.verbProps.IsMeleeAttack)
                verb = pawn.VerbTracker?.AllVerbs?.FirstOrDefault(v => v is Verb_MeleeAttack && v.Available());
            if (verb == null) return;

            var targetCell = UI.MouseCell();
            if (!targetCell.InBounds(pawn.Map)) return;
            var things = targetCell.GetThingList(pawn.Map);
            Thing bestTarget = things?.FirstOrDefault(t => t is Pawn && t != pawn)
                ?? things?.FirstOrDefault(t => t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item);
            LocalTargetInfo target = bestTarget != null
                ? new LocalTargetInfo(bestTarget)
                : new LocalTargetInfo(targetCell);

            Vector3 targetPos = target.Thing != null ? target.Thing.DrawPos : targetCell.ToVector3Shifted();
            Vector3 toTarget = targetPos - pawn.DrawPos;
            if (toTarget.sqrMagnitude > 0.01f)
                pawn.Rotation = Rot4.FromAngleFlat(NormAngle(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg));

            if (verb.verbProps.IsMeleeAttack)
            {
                if (pawn.Position.AdjacentTo8WayOrInside(targetCell) && target.Thing != null)
                    pawn.meleeVerbs.TryMeleeAttack(target.Thing);
            }
            else
            {
                if (verb.CanHitTarget(target))
                {
                    verb.TryStartCastOn(target, false, true);
                }
            }
        }

        public void DrawPlayerGizmos()
        {
            if (Event.current.type == EventType.Layout) return;

            State.DrawingTopRightGizmos = true;
            var gizmoSource = ModCompatibility.IsPawnInVehicle(pawn, out Pawn vehicle, out bool isDriver, out _)
                ? vehicle
                : (Thing)pawn;
            var wasSelected = Find.Selector.IsSelected(gizmoSource);
            if (!wasSelected)
            {
                prevSelected = new List<object>(Find.Selector.SelectedObjects);
                Find.Selector.selected.Clear();
                Find.Selector.selected.Add(gizmoSource);
            }
            var gizmos = gizmoSource.GetGizmos()
                .Distinct()
                .OrderBy(g => g.Order)
                .Where(g => g.Visible)
                .ToList();
            if (!wasSelected)
            {
                Find.Selector.selected.Clear();
                Find.Selector.selected.AddRange(prevSelected);
            }
            State.DrawingTopRightGizmos = false;

            float scale = 0.85f * Prefs.UIScale;
            float actualSize = 75f;
            float spacing = 8f;

            float startX = 0f;
            float startY = 0f;

            switch (PerspectiveShiftMod.settings.gizmoCorner)
            {
                case GizmoCorner.TopRight:
                    startX = (UI.screenWidth - 10f) / 0.85f - actualSize;
                    startY = 10f / 0.85f;
                    break;
                case GizmoCorner.BottomRight:
                    startX = (UI.screenWidth - 10f) / 0.85f - actualSize;
                    startY = (UI.screenHeight - 10f) / 0.85f - actualSize;
                    break;
                case GizmoCorner.BottomLeft:
                    startX = 10f / 0.85f;
                    startY = (UI.screenHeight - 10f) / 0.85f - actualSize;
                    break;
                case GizmoCorner.TopLeft:
                    startX = 10f / 0.85f;
                    startY = 10f / 0.85f;
                    break;
            }

            float y = startY;

            float yStep = (PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.TopLeft || PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.TopRight) ? (actualSize + spacing) : -(actualSize + spacing);

            float maxRowWidth = (actualSize + spacing) * 4f;
            float rowWidth = 0f;

            bool rightAnchored = PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.TopRight || PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.BottomRight;
            float cursor = rightAnchored ? startX + actualSize : startX;

            GizmoGridDrawer.drawnHotKeys.Clear();

            Gizmo interactedGizmo = null;
            Event interactedEvent = null;
            Gizmo floatMenuGizmo = null;
            bool isFirst = true;

            float boundsMinX = UI.screenWidth;
            float boundsMaxX = 0f;
            float boundsMinY = UI.screenHeight;
            float boundsMaxY = 0f;

            bool suppressHotkeys = Find.Selector.NumSelected > 0 && !Find.Selector.IsSelected(pawn);

            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            foreach (var cmd in gizmos)
            {
                float gizmoWidth = cmd.GetWidth(actualSize);

                if (rowWidth > 0 && rowWidth + gizmoWidth > maxRowWidth)
                {
                    cursor = rightAnchored ? startX + actualSize : startX;
                    y += yStep;
                    rowWidth = 0f;
                }

                float drawX = rightAnchored ? cursor - gizmoWidth : cursor;

                KeyBindingDef tempHotkey = null;
                if (cmd is Command command)
                {
                    tempHotkey = command.hotKey;
                    if (suppressHotkeys) command.hotKey = null;
                }
                float screenX = drawX * 0.85f;
                float screenY = y * 0.85f;
                float screenW = gizmoWidth * 0.85f;
                float screenH = actualSize * 0.85f;

                boundsMinX = Mathf.Min(boundsMinX, screenX);
                boundsMaxX = Mathf.Max(boundsMaxX, screenX + screenW);
                boundsMinY = Mathf.Min(boundsMinY, screenY);
                boundsMaxY = Mathf.Max(boundsMaxY, screenY + screenH);

                GizmoRenderParms parms = default;
                parms.isFirst = isFirst;
                GizmoResult result = cmd.GizmoOnGUI(new Vector2(drawX, y), actualSize, parms);

                if (cmd is Command command2)
                {
                    if (suppressHotkeys) command2.hotKey = tempHotkey;
                }

                GenUI.AbsorbClicksInRect(new Rect(drawX, y, gizmoWidth, actualSize));

                if (result.State == GizmoState.Interacted ||
                    (result.State == GizmoState.OpenedFloatMenu
                     && !cmd.RightClickFloatMenuOptions.Any()))
                {
                    interactedGizmo = cmd;
                    interactedEvent = result.InteractEvent;
                }
                else if (result.State == GizmoState.OpenedFloatMenu)
                {
                    floatMenuGizmo = cmd;
                }

                isFirst = false;
                rowWidth += gizmoWidth + spacing;
                if (rightAnchored) cursor -= gizmoWidth + spacing;
                else cursor += gizmoWidth + spacing;
            }

            GUI.matrix = prevMatrix;

            if (gizmos.Count > 0)
            {
                gizmoBounds = new Rect(boundsMinX, boundsMinY, boundsMaxX - boundsMinX, boundsMaxY - boundsMinY);
            }
            else
            {
                gizmoBounds = Rect.zero;
            }

            if (interactedGizmo != null)
            {
                interactedGizmo.ProcessInput(interactedEvent);
                Event.current.Use();
            }
            else if (floatMenuGizmo != null)
            {
                var options = floatMenuGizmo.RightClickFloatMenuOptions.ToList();
                if (options.Any()) Find.WindowStack?.Add(new FloatMenu(options));
                Event.current.Use();
            }
        }

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
            bool itemInRange = pawn.Position.DistanceTo(clickCell) <= PerspectiveShiftMod.settings.grabRange;
            bool inRange = itemInRange;

            if (!inRange)
            {
                var things = clickCell.GetThingList(pawn.Map);
                if (things != null)
                {
                    foreach (var t in things)
                    {
                        if (t.def.hasInteractionCell && t.InteractionCell == pawn.Position)
                        {
                            inRange = true;
                            break;
                        }
                        if (t.def.building?.watchBuildingStandDistanceRange != null)
                        {
                            var watchCells = WatchBuildingUtility.CalculateWatchCells(t.def, t.Position, t.Rotation, pawn.Map);
                            if (watchCells.Contains(pawn.Position))
                            {
                                inRange = true;
                                break;
                            }
                        }
                        if (t.def.size.x > 1 || t.def.size.z > 1)
                        {
                            foreach (var c in t.OccupiedRect())
                            {
                                if (pawn.Position.DistanceTo(c) <= PerspectiveShiftMod.settings.grabRange)
                                {
                                    inRange = true;
                                    break;
                                }
                            }
                        }
                        if (inRange) break;
                    }
                }
            }

            if (!inRange)
            {
                return false;
            }

            if (CarriedThing == null)
            {
                var storageBuilding = clickCell.GetThingList(pawn.Map)?.OfType<Building_Storage>().FirstOrDefault();
                if (storageBuilding != null)
                {
                    Find.WindowStack.Add(new Dialog_StorageMenu(storageBuilding));
                    return true;
                }
            }

            if (CarriedThing != null)
            {
                return HandleDropOrInteract(clickCell, itemInRange);
            }
            else
            {
                var item = clickCell.GetFirstItem(pawn.Map);
                if (item != null && item.def.category == ThingCategory.Item && itemInRange)
                {
                    if (!pawn.Awake())
                    {
                        RestUtility.WakeUp(pawn);
                    }
                    ExecutePickup(item);
                    return true;
                }

                var downedPawn = clickCell.GetFirstPawn(pawn.Map);
                if (downedPawn != null && downedPawn != pawn && downedPawn.Downed && itemInRange)
                {
                    if (!pawn.Awake()) RestUtility.WakeUp(pawn);
                    ExecutePickup(downedPawn);
                    return true;
                }
            }

            List<FloatMenuOption> opts = null;
            try
            {
                var building = clickCell.GetFirstBuilding(pawn.Map);
                if (building != null && !building.Destroyed && building.Spawned)
                {
                    if (building.def.Minifiable && (building.Faction == pawn.Faction || building.def.building.alwaysUninstallable))
                    {
                        var reinstallBp = InstallBlueprintUtility.ExistingBlueprintFor(building);
                        if (reinstallBp != null)
                        {
                            var unJob = JobMaker.MakeJob(JobDefOf.Uninstall, building);
                            unJob.ignoreDesignations = true;
                            if (TryStartForcedJob(unJob))
                            {
                                pendingMinifiedPickup = building;
                                return true;
                            }
                        }
                        if (pawn.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall) != null)
                        {
                            var unJob = JobMaker.MakeJob(JobDefOf.Uninstall, building);
                            unJob.ignoreDesignations = true;
                            if (TryStartForcedJob(unJob)) return true;
                        }
                    }

                    if (InteractWith(building))
                    {
                        LastManualTarget = building;
                        return true;
                    }
                }

                opts = FloatMenuMakerMap.GetOptions(new List<Pawn> { pawn }, clickCell.ToVector3Shifted(), out _);
            }
            finally
            {
            }
            if (opts != null)
            {
                if (pawn.equipment?.Primary != null)
                {
                    var dropString = "Drop".Translate(pawn.equipment.Primary.Label, pawn.equipment.Primary);
                    opts.RemoveAll(opt => opt.Label == dropString);
                }
                opts.RemoveAll(opt => opt.revalidateClickTarget is Pawn otherPawn && otherPawn != pawn);
            }

            var bestOption = opts.Where(opt => !opt.Disabled)
                                 .OrderByDescending(opt => opt.Priority)
                                 .ThenByDescending(opt => opt.Priority != MenuOptionPriority.GoHere)
                                 .FirstOrDefault();
            if (bestOption != null)
            {
                bestOption.action.Invoke();

                var clickedPawn = clickCell.GetFirstPawn(pawn.Map);
                if (clickedPawn != null && clickedPawn != pawn)
                {
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(clickedPawn);
                }
                return true;
            }

            return TryExecuteDesignatorlessFallback(clickCell, itemInRange);
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
            if (mineable != null
                && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Mining)
                && pawn.CanReserve(mineable))
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

            if (target is Building b)
            {
                var blueprint = b.Position.GetThingList(pawn.Map).OfType<Blueprint>().FirstOrDefault();
                if (blueprint != null && GenConstruct.BlocksConstruction(blueprint, b) && b.DeconstructibleBy(pawn.Faction).Accepted)
                {
                    if (TryStartForcedJob(JobMaker.MakeJob(JobDefOf.Deconstruct, b)))
                        return true;
                }
            }

            if (pawn.workSettings != null)
            {
                List<WorkGiver> workGivers = pawn.workSettings.WorkGiversInOrderNormal;
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
                                Messages.Message("PS_IncapableOfWorkType".Translate(scanner.def.workType.labelShort), MessageTypeDefOf.RejectInput, false);
                                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                                return true;
                            }
                        }
                    }
                }
            }

            if (target is Building_ResearchBench)
            {
                if (Find.ResearchManager.GetProject() == null)
                {
                    Messages.Message("PS_NoResearchProject".Translate(), MessageTypeDefOf.RejectInput, false);
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    return true;
                }
            }

            if (target is Building_Bed bed && !bed.ForPrisoners && !bed.Medical && pawn.needs?.rest != null)
            {
                if (RestUtility.CanUseBedEver(pawn, bed.def) && pawn.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Deadly))
                {
                    if (TryStartForcedJob(JobMaker.MakeJob(JobDefOf.LayDown, bed)))
                    {
                        return true;
                    }
                }
            }

            if (pawn.needs?.joy != null)
            {
                var joyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading
                    .Where(jg => jg.Worker is JoyGiver_InteractBuilding && jg.thingDefs != null && jg.thingDefs.Contains(target.def)).ToList();

                foreach (var jgDef in joyGivers)
                {
                    if (jgDef.Worker is JoyGiver_InteractBuilding worker)
                    {
                        if (TryStartForcedJob(worker.TryGivePlayJob(pawn, target)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsTargetInRange(LocalTargetInfo target)
        {
            if (!target.IsValid) return true;

            IntVec3 cell = target.Cell;

            if (pawn.Position.DistanceTo(cell) <= PerspectiveShiftMod.settings.grabRange ||
                cell.AdjacentTo8WayOrInside(pawn.Position))
                return true;

            Thing thing = target.Thing;
            if (thing != null)
            {
                if (thing.def.hasInteractionCell && thing.InteractionCell == pawn.Position)
                    return true;

                if (thing.def.building?.watchBuildingStandDistanceRange != null)
                {
                    var watchCells = WatchBuildingUtility.CalculateWatchCells(
                        thing.def, thing.Position, thing.Rotation, pawn.Map);
                    if (watchCells.Contains(pawn.Position))
                        return true;
                }

                if (thing.def.size.x > 1 || thing.def.size.z > 1)
                {
                    foreach (var c in thing.OccupiedRect())
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

        private void ExecutePickup(Thing target)
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
            }
        }

        private bool HandleDropOrInteract(IntVec3 cell, bool itemInRange)
        {
            if (!cell.InBounds(pawn.Map))
            {
                return false;
            }

            var frame = cell.GetFirstThing<Frame>(pawn.Map);
            if (frame != null && frame.IsCompleted())
            {
                Messages.Message("PS_BlueprintFull".Translate(), MessageTypeDefOf.RejectInput, false);
                return true;
            }

            var cellThings = cell.GetThingList(pawn.Map);
            if (cellThings != null)
            {
                if (CarriedThing?.def.IsMedicine == true)
                {
                    var patient = cell.GetFirstPawn(pawn.Map);
                    if (patient != null && HealthAIUtility.ShouldBeTendedNowByPlayer(patient))
                    {
                        var job = JobMaker.MakeJob(JobDefOf.TendPatient, patient, CarriedThing);
                        if (TryStartForcedJob(job)) return true;
                    }
                }

                var mortar = cellThings.OfType<Building_TurretGun>().FirstOrDefault(t => t.gun != null && t.gun.TryGetComp<CompChangeableProjectile>() != null);
                if (mortar != null && CarriedThing != null)
                {
                    var comp = mortar.gun.TryGetComp<CompChangeableProjectile>();
                    if (!comp.Loaded && comp.allowedShellsSettings.AllowedToAccept(CarriedThing))
                    {
                        var shell = CarriedThing;
                        comp.LoadShell(shell.def, 1);
                        shell.SplitOff(1).Destroy();
                        SoundDefOf.Artillery_ShellLoaded.PlayOneShot(new TargetInfo(mortar.Position, pawn.Map));
                        Messages.Message("PS_ShellLoaded".Translate(shell.def.label), MessageTypeDefOf.TaskCompletion);
                        return true;
                    }
                }

                var installBp = cellThings.OfType<Blueprint_Install>().FirstOrDefault();
                if (installBp != null && installBp.MiniToInstallOrBuildingToReinstall == CarriedThing)
                {
                    installBp.TryReplaceWithSolidThing(pawn, out _, out _);
                    return true;
                }

                var refuelableThing = cellThings.FirstOrDefault(t => t.TryGetComp<CompRefuelable>() != null);
                if (refuelableThing != null)
                {
                    var comp = refuelableThing.TryGetComp<CompRefuelable>();
                    if (comp.Props.fuelFilter.Allows(CarriedThing) && comp.GetFuelCountToFullyRefuel() > 0)
                    {
                        var amount = Mathf.Min(CarriedThing.stackCount, comp.GetFuelCountToFullyRefuel());
                        comp.Refuel(amount);
                        CarriedThing.SplitOff(amount).Destroy();
                        if (refuelableThing.def.soundInteract != null) refuelableThing.def.soundInteract.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                        return true;
                    }
                }

                var blueprint = cellThings.OfType<Blueprint_Build>().FirstOrDefault();
                if (blueprint != null && blueprint.ThingCountNeeded(CarriedThing.def) > 0 && blueprint.TryReplaceWithSolidThing(pawn, out Thing frameThing, out _) && frameThing is Frame frame1)
                {
                    var needed = frame1.ThingCountNeeded(CarriedThing.def);
                    if (needed > 0)
                    {
                        pawn.carryTracker.innerContainer.TryTransferToContainer(CarriedThing, frame1.resourceContainer, Mathf.Min(CarriedThing.stackCount, needed));
                    }
                    return true;
                }

                var frame2 = cellThings.OfType<Frame>().FirstOrDefault();
                if (frame2 != null)
                {
                    var needed = frame2.ThingCountNeeded(CarriedThing.def);
                    if (needed > 0)
                    {
                        pawn.carryTracker.innerContainer.TryTransferToContainer(CarriedThing, frame2.resourceContainer, Mathf.Min(CarriedThing.stackCount, needed));
                        return true;
                    }
                }
            }

            var building = cell.GetFirstBuilding(pawn.Map);

            if (building is IBillGiver billGiver)
            {
                if (TryDepositIntoBill(billGiver, building))
                {
                    return true;
                }
            }

            var bed = building as Building_Bed;
            if (bed != null && CarriedThing is Pawn carriedPawn && carriedPawn.Downed)
            {
                if (bed.ForPrisoners && !carriedPawn.IsPrisonerOfColony)
                {
                    Messages.Message("PS_BedForPrisonersOnly".Translate(), MessageTypeDefOf.RejectInput, false);
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    return true;
                }
                if (bed.ForSlaves && !carriedPawn.IsSlaveOfColony)
                {
                    Messages.Message("PS_BedForSlavesOnly".Translate(), MessageTypeDefOf.RejectInput, false);
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    return true;
                }
                if (bed.ForColonists && (carriedPawn.IsPrisonerOfColony || carriedPawn.IsSlaveOfColony))
                {
                    Messages.Message("PS_BedForColonistsOnly".Translate(), MessageTypeDefOf.RejectInput, false);
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    return true;
                }
                pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out _);
                carriedPawn.jobs.Notify_TuckedIntoBed(bed);
                if (!bed.ForPrisoners && carriedPawn.Faction != Faction.OfPlayer && !carriedPawn.IsPrisoner)
                {
                    carriedPawn.guest.SetGuestStatus(Faction.OfPlayer);
                }
                return true;
            }

            bool wouldWipe = false;
            if (cellThings != null)
            {
                foreach (var t in cellThings)
                {
                    if (GenSpawn.SpawningWipes(CarriedThing.def, t.def))
                    {
                        wouldWipe = true;
                        break;
                    }
                }
            }
            ThingPlaceMode placeMode = wouldWipe ? ThingPlaceMode.Near : ThingPlaceMode.Direct;

            if (!itemInRange) return false;

            IHaulDestination haulDest = building as IHaulDestination ?? pawn.Map.haulDestinationManager.SlotGroupAt(cell)?.parent;

            if (haulDest != null)
            {
                Thing itemToDrop = CarriedThing;
                var parentSettings = haulDest.GetParentStoreSettings();
                bool isPossible = parentSettings == null || parentSettings.AllowedToAccept(itemToDrop);

                if (!isPossible || (!haulDest.GetStoreSettings().AllowedToAccept(itemToDrop) && !haulDest.GetStoreSettings().filter.Allows(itemToDrop.def)))
                {
                    if (!isPossible)
                    {
                        Messages.Message("PS_StorageImpossible".Translate(itemToDrop.LabelCap), MessageTypeDefOf.RejectInput, false);
                        DropAdjacent(itemToDrop, cell);
                        return true;
                    }
                    else
                    {
                        string label = (haulDest as ISlotGroupParent)?.SlotYielderLabel() ?? (haulDest as Thing)?.Label ?? "Storage";
                        var text = "PS_StorageNotPermitted".Translate(itemToDrop.Label, label);
                        Find.WindowStack.Add(new Dialog_MessageBox(text, "Yes".Translate(), () =>
                        {
                            if (itemToDrop != null && !itemToDrop.Destroyed)
                            {
                                haulDest.GetStoreSettings().filter.SetAllow(itemToDrop.def, true);
                                if (!TryDepositInDestination(haulDest, itemToDrop, cell, placeMode))
                                {
                                    Messages.Message("PS_StorageImpossible".Translate(itemToDrop.LabelCap), MessageTypeDefOf.RejectInput, false);
                                    DropAdjacent(itemToDrop, cell);
                                }
                            }
                        }, "No".Translate(), () =>
                        {
                            if (itemToDrop != null && !itemToDrop.Destroyed)
                            {
                                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                            }
                        }));
                        return true;
                    }
                }
                else
                {
                    IntVec3 depositCell = cell;
                    if (haulDest is Building_Storage bStorage)
                    {
                        foreach (var c in bStorage.AllSlotCellsList())
                        {
                            if (StoreUtility.IsGoodStoreCell(c, pawn.Map, itemToDrop, pawn, pawn.Faction))
                            {
                                depositCell = c;
                                break;
                            }
                        }
                    }
                    if (TryDepositInDestination(haulDest, itemToDrop, depositCell, placeMode))
                    {
                        return true;
                    }
                    bool filterAllows = haulDest.GetStoreSettings()?.AllowedToAccept(itemToDrop) == true;
                    Messages.Message(filterAllows
                        ? "PS_StorageFull".Translate(itemToDrop.LabelCap)
                        : "PS_StorageImpossible".Translate(itemToDrop.LabelCap),
                        MessageTypeDefOf.RejectInput, false);
                    DropAdjacent(itemToDrop, cell);
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

        private bool TryDepositInDestination(IHaulDestination dest, Thing item, IntVec3 cell, ThingPlaceMode placeMode)
        {
            if (dest is Building b && !(b is ISlotGroupParent))
            {
                var thingOwner = b.TryGetInnerInteractableThingOwner();
                if (thingOwner != null)
                {
                    if (dest.Accepts(item))
                    {
                        var transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(item, thingOwner, item.stackCount);
                        if (transferred > 0) return true;
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

        private void DropAdjacent(Thing item, IntVec3 cell)
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

        private bool TryDepositIntoBill(IBillGiver billGiver, Building building)
        {
            Thing heldItem = CarriedThing;
            if (heldItem == null) return false;

            foreach (var bill in billGiver.BillStack)
            {
                if (bill.ShouldDoNow() && bill.IsFixedOrAllowedIngredient(heldItem))
                {
                    IntVec3 placeLoc = building.InteractionCell;

                    var container = building.TryGetInnerInteractableThingOwner();

                    if (container != null && !(building is ISlotGroupParent))
                    {
                        int transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(heldItem, container, heldItem.stackCount);

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

        private void DrawNeeds()
        {
            if (pawn.needs == null || gizmoBounds == Rect.zero) return;

            var needs = pawn.needs.AllNeeds
                .Where(n => PerspectiveShiftMod.settings.pinnedNeeds.Contains(n.def.defName))
                .ToList();
            if (!needs.Any()) return;

            float width = 200f;
            float height = 40f;
            float totalHeight = needs.Count * height;

            var startX = Mathf.Min(gizmoBounds.xMax - width - 10f, UI.screenWidth - width - 10f);
            float startY = gizmoBounds.yMax + 35f;

            if (PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.BottomRight)
            {
                startY = gizmoBounds.yMin - totalHeight - 10f;
            }
            else if (PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.BottomLeft)
            {
                startX = gizmoBounds.xMin + 10f;
                startY = gizmoBounds.yMin - totalHeight - 10f;
            }
            else if (PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.TopLeft)
            {
                startX = gizmoBounds.xMin + 10f;
            }

            var unifiedBg = new Rect(startX - 20f, startY - 5f, width + 30f, totalHeight + 10f);
            Widgets.DrawBoxSolid(unifiedBg, new ColorInt(32, 32, 32).ToColor.WithAlpha(0.7f));

            DrawingAvatarNeeds = true;
            float currentY = startY;
            foreach (var need in needs)
            {
                Rect needRect = new Rect(startX, currentY, width, height);
                need.DrawOnGUI(needRect, maxThresholdMarkers: int.MaxValue, customMargin: 4f, drawArrows: true, doTooltip: true, rectForTooltip: null, drawLabel: true);
                currentY += height;
            }
            DrawingAvatarNeeds = false;
        }

        public void Tick()
        {
            if (pawn.Downed || !pawn.Spawned || pawn.Map == null || pawn.GetLord() != null || pawn.mindState.duty != null) return;
            if (pawn.InMentalState) return;

            if (pawn.carryTracker?.CarriedThing != null && pawn.CurJob != null)
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

            HandleDoorInteraction();
            HandleCombatStance();

            if (pendingMinifiedPickup != null)
            {
                if (pawn.CurJob == null || pawn.CurJob.def != JobDefOf.Uninstall)
                {
                    var minified = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.MinifiedThing), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Some));
                    if (minified != null && minified is MinifiedThing mt && mt.InnerThing == pendingMinifiedPickup)
                    {
                        if (pawn.Position.DistanceTo(minified.Position) <= PerspectiveShiftMod.settings.grabRange)
                        {
                            ExecutePickup(minified);
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

            if (pawn.Drafted && pawn.carryTracker?.CarriedThing != null)
            {
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            }

            if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def != JobDefOf.Wait && pawn.jobs.curJob.def != JobDefOf.Wait_Combat)
            {
                pawn.jobs.curJob.expiryInterval = -1;
            }

            if (IsMoving && pawn.pather != null)
            {
                pawn.pather.lastMovedTick = Find.TickManager.TicksGame;
                var baseSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed);
                float speedMult = isSprinting ? PerspectiveShiftMod.settings.sprintSpeedMultiplier
                                : isWalking ? PerspectiveShiftMod.settings.sneakSpeedMultiplier
                                : 1f;
                pawn.pather.nextCellCostTotal = Mathf.Max(60f / Mathf.Max(baseSpeed, 0.1f) / speedMult, 1f);
            }
        }
    }
}
