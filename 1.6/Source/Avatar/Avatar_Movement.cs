using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace PerspectiveShift
{
    public partial class Avatar
    {
        private float moveInputDuration = 0f;
        private bool wasSprinting = false;
        private Vector3 lastVehicleMoveInput = Vector3.zero;
        private IntVec3 prevCell = IntVec3.Invalid;
        private int vehicleStopGraceTicks = 0;
        private int vehiclePathFailCooldown = 0;
        private Vector3 _leanVelocity = Vector3.zero;
        private const float jobInterruptDelay = 0.35f;
        private const float maxPhysicsDesyncDistSq = 2.25f;

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
                UpdateVehiclePhysics(vehicle, isDriver);
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

            if (pawn.InMentalState || passedOut)
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

            if (PerspectiveShiftMod.settings.requireHeldClickForJobs
                && pawn.jobs?.curJob != null
                && pawn.jobs.curJob.def.HasModExtension<JobRequiresHoldExtension>())
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
                if (!passedOut) RestUtility.WakeUp(pawn);
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

            var newPos = PerspectiveShiftMod.settings.cameraEasing
                ? Vector3.Lerp(driver.rootPos, targetCamPos, 0.1f)
                : targetCamPos;

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

        private void UpdateInput()
        {
            moveInput = Vector3.zero;

            if (DefsOf.PS_MoveForward.IsDown) moveInput += Vector3.forward;
            if (DefsOf.PS_MoveBack.IsDown) moveInput += Vector3.back;
            if (DefsOf.PS_MoveLeft.IsDown) moveInput += Vector3.left;
            if (DefsOf.PS_MoveRight.IsDown) moveInput += Vector3.right;

            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

            bool sprintKeyDown = DefsOf.PS_Sprint.KeyDownEvent;
            isSprinting = PerspectiveShiftMod.settings.enableSprinting && DefsOf.PS_Sprint.IsDown;
            if (isSprinting && pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage <= 0.01f)
            {
                isSprinting = false;
                if (sprintKeyDown)
                {
                    Messages.Message("PS_CannotSprintExhausted".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }
            isWalking = PerspectiveShiftMod.settings.enableSneaking && DefsOf.PS_Walk.IsDown;

            if (isSprinting && !wasSprinting)
            {
                DefsOf.PS_SprintSound.PlayOneShotOnCamera();
            }
            wasSprinting = isSprinting;
        }

        private void ProcessMovement()
        {
            if (interactingDoor != null) return;

            if (!pawn.Drafted && pawn.IsUnderAIControl())
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

                if (pawn.Drafted && !pawn.InMentalState && !State.ControlsFrozen && pawn.stances.curStance is not Stance_Busy)
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
            if (distSq > maxPhysicsDesyncDistSq)
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
                if (!isOurWaitJob && moveInputDuration > jobInterruptDelay)
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
            var baseSpeed = 60f / Mathf.Max(pawn.TicksPerMoveCardinal, 1f);
            float gaitMultiplier = isSprinting ? PerspectiveShiftMod.settings.sprintSpeedMultiplier
                                 : isWalking ? PerspectiveShiftMod.settings.sneakSpeedMultiplier
                                 : 1f;
            float tickRate = Mathf.Min(State.ActualTickRateMultiplier, Find.TickManager.TickRateMultiplier);
            float speed = baseSpeed * PerspectiveShiftMod.settings.moveSpeedMultiplier * terrainMultiplier * gaitMultiplier * Time.deltaTime * tickRate;
            speed = Mathf.Min(speed, PerspectiveShiftMod.settings.playerMoveSpeedCap);

            IntVec3 forwardCell = (physicsPosition.Value + deltaRaw * 0.8f).ToIntVec3();
            if (forwardCell != pawn.Position && forwardCell.InBounds(pawn.Map))
            {
                var forwardDoor = forwardCell.GetDoor(pawn.Map);
                if (forwardDoor != null && forwardDoor.PawnCanOpen(pawn))
                {
                    forwardDoor.Notify_PawnApproaching(pawn, 1f);
                }
            }

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

            if (nextCell != pawn.Position)
            {
                var door = nextCell.GetDoor(pawn.Map);
                if (door != null && door.PawnCanOpen(pawn))
                {
                    if (door.SlowsPawns && (!door.Open || door.TicksTillFullyOpened > 0))
                    {
                        if (!door.Open) door.StartManualOpenBy(pawn);
                        pawn.Map.fogGrid.Notify_PawnEnteringDoor(door, pawn);
                        interactingDoor = door;
                        return;
                    }
                }
            }

            physicsPosition = newPos;
            if (pawn.Position != nextCell)
            {
                prevCell = pawn.Position;
                pawn.Position = nextCell;
                pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
                pawn.pather.nextCell = nextCell;
            }

            if (pawn.Drawer?.leaner != null && pawn.stances.curStance is not Stance_Busy)
            {
                LeanTarget = Vector3.zero;
            }

            if (pawn.stances.curStance is not Stance_Busy)
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

        private void RotateTowardsMouse()
        {
            var pawnCenter = pawn.Position.ToVector3Shifted();
            Vector3 toMouse = UI.MouseMapPosition() - pawnCenter;
            if (toMouse.sqrMagnitude > 0.1f)
                pawn.Rotation = Rot4.FromAngleFlat(NormAngle(Mathf.Atan2(toMouse.x, toMouse.z) * Mathf.Rad2Deg));
        }

        private static float NormAngle(float a)
        {
            while (a < 0f) a += 360f;
            while (a >= 360f) a -= 360f;
            return a;
        }

        private float GetMovementSpeedMultiplier(IntVec3 cell)
        {
            if (!cell.InBounds(pawn.Map)) return 1f;

            float num = Pawn_PathFollower.CostToMoveIntoCell(pawn, cell);

            if (prevCell.IsValid && prevCell != pawn.Position)
            {
                int wrongPrevCost = pawn.Map.pathing.For(pawn).pathGrid.CalculatedCostAt(cell, perceivedStatic: false, pawn.Position);
                int rightPrevCost = pawn.Map.pathing.For(pawn).pathGrid.CalculatedCostAt(cell, perceivedStatic: false, prevCell);
                num += rightPrevCost - wrongPrevCost;
            }

            if (cell.x != pawn.Position.x && cell.z != pawn.Position.z)
                num = num - pawn.TicksPerMoveDiagonal + pawn.TicksPerMoveCardinal;

            num = Mathf.Clamp(num, 1f, 450f);
            return pawn.TicksPerMoveCardinal / Mathf.Max(num, 1f);
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

            if (!cell.WalkableBy(pawn.Map, pawn)) return false;

            var door = cell.GetDoor(pawn.Map);
            if (door != null && !door.Open && !door.PawnCanOpen(pawn)) return false;

            return true;
        }

        private void UpdateVehiclePhysics(Pawn vehicle, bool isDriver)
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

        private void HandleDoorInteraction()
        {
            if (interactingDoor == null) return;
            if (interactingDoor.TicksTillFullyOpened <= 0 || interactingDoor.Destroyed)
                interactingDoor = null;
        }
    }
}
