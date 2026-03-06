using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace PerspectiveShift
{
    [StaticConstructorOnStartup]
    [HotSwappable]
    public class Avatar : IExposable
    {
        public static bool IsAvatarLeftClick = false;
        public Pawn pawn;
        public Vector3 moveInput;
        public bool isSprinting;
        public bool isWalking;
        public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
        public Vector3? physicsPosition;
        public Vector3 LeanTarget = Vector3.zero;
        public Vector3 LeanSmoothed = Vector3.zero;
        private float moveInputDuration = 0f;
        public float aimAngle = -1f;
        private Vector3 _leanVelocity = Vector3.zero;

        public Thing CarriedThing => pawn.carryTracker?.CarriedThing;
        public Thing LastManualTarget;

        private Building_Door interactingDoor;
        private bool wasMovingLastFrame;
        private Rect gizmoBounds;
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
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref interactingDoor, "interactingDoor");
        }

        public void UpdatePhysics()
        {
            if (pawn == null || pawn.Dead || pawn.Downed) return;
            if (WorldComponent_GravshipController.CutsceneInProgress) return;
            if (State.CameraLockPosition.HasValue) return;

            if (pawn.InMentalState)
            {
                moveInput = Vector3.zero;
                if (wasMovingLastFrame)
                {
                    if (pawn.pather.curPath != null) pawn.pather.StopDead();
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

            if (State.ControlsFrozen || isShootingStationary)
            {
                moveInput = Vector3.zero;
                if (wasMovingLastFrame)
                {
                    if (pawn.pather.curPath != null) pawn.pather.StopDead();
                    wasMovingLastFrame = false;
                }
                return;
            }

            UpdateInput();

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
            if (pawn == null || pawn.Map != Find.CurrentMap) return;

            var driver = Find.CameraDriver;
            if (driver == null) return;

            var sizeRange = new FloatRange(PerspectiveShiftMod.settings.minZoom, PerspectiveShiftMod.settings.maxZoom);
            driver.config.sizeRange = sizeRange;
            driver.config.zoomSpeed = PerspectiveShiftMod.settings.zoomSpeed * 10f;

            Vector3 targetCamPos = State.CameraLockPosition
                ?? physicsPosition
                ?? pawn.Position.ToVector3ShiftedWithAltitude(pawn.def.Altitude);
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
            if (pawn == null || pawn.Dead || pawn.Downed) return false;
            if (pawn.InMentalState) return false;

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
                    var transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(carried, pawn.inventory.innerContainer, count);
                    if (transferred > 0)
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                        Event.current.Use();
                        return true;
                    }
                }

                if (pawn.Drafted)
                {
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

            isSprinting = DefsOf.PS_Sprint.IsDown;
            isWalking = DefsOf.PS_Walk.IsDown;
        }

        public void RenderPawn()
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned) return;
            if (WorldComponent_GravshipController.CutsceneInProgress) return;

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

            if (pawn.pather != null && IsMoving)
            {
                var baseSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed);
                pawn.pather.nextCellCostTotal = 60f / Mathf.Max(baseSpeed, 0.1f);
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
            if (pawn == null || pawn.Map == null || !pawn.Spawned) return;
            if (interactingDoor != null) return;

            if (!pawn.Drafted && pawn.GetLord() != null)
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
                    physicsPosition = null;
                }
            }

            if (!physicsPosition.HasValue)
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
                bool isOurWaitJob = pawn.jobs.curJob.def == JobDefOf.Wait && pawn.jobs.curJob.expiryInterval == 60;
                if (!isOurWaitJob && moveInputDuration > 0.35f)
                {
                    if (!(canRunAndGun && isShooting && pawn.Drafted))
                    {
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
            float expectedTicks = 60f / Mathf.Max(baseSpeed, 0.1f);

            if (pawn.pather != null)
            {
                pawn.pather.nextCellCostTotal = expectedTicks;
            }

            float tickModifier = expectedTicks / Mathf.Max(pawn.TicksPerMoveCardinal, 1f);
            float speed = baseSpeed * PerspectiveShiftMod.settings.moveSpeedMultiplier * terrainMultiplier * tickModifier * (isSprinting ? 1.3f : isWalking ? 0.5f : 1.0f) * Time.deltaTime * Find.TickManager.TickRateMultiplier;
            Vector3 delta = deltaRaw * speed;
            Vector3 newPos = physicsPosition.Value;

            if (Mathf.Abs(delta.x) > 0.0001f)
            {
                Vector3 testX = newPos + new Vector3(delta.x, 0, 0);
                if (IsWalkableWithMargin(testX)) newPos = testX;
            }

            if (Mathf.Abs(delta.z) > 0.0001f)
            {
                Vector3 testZ = newPos + new Vector3(0, 0, delta.z);
                if (IsWalkableWithMargin(testZ)) newPos = testZ;
            }

            var nextCell = newPos.ToIntVec3();
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
                    UpdateRotation(moveInput.normalized);
                }
            }
        }

        private bool IsWalkableWithMargin(Vector3 pos)
        {
            float margin = 0.2f;
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
            if (!cell.Walkable(pawn.Map)) return false;

            var door = cell.GetDoor(pawn.Map);
            if (door != null && !door.Open && !door.PawnCanOpen(pawn)) return false;

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
            if (pawn != null)
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

                if (pawn.Drafted && !pawn.InMentalState)
                {
                    if (!Find.TickManager.Paused && Find.Selector.NumSelected > 0) Find.Selector.ClearSelection();

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

                    if (mouseOverUI || mouseOverGizmo || Find.TickManager.Paused)
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
        }

        private void DebugLog()
        {
            bool shouldLogMovement = false;
            if (shouldLogMovement)
            {
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
                    $"paused={Find.TickManager.Paused}");
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
            if (pawn == null) return;
            if (Event.current.type == EventType.Layout) return;

            State.DrawingTopRightGizmos = true;
            var gizmos = pawn.GetGizmos()
                .Distinct()
                .OrderByDescending(g => g.Order)
                .OfType<Command>()
                .Where(g => g.Visible)
                .ToList();
            State.DrawingTopRightGizmos = false;

            float scale = 0.85f;
            float actualSize = 75f;
            float spacing = 8f;

            float startX = 0f;
            float startY = 0f;

            switch (PerspectiveShiftMod.settings.gizmoCorner)
            {
                case GizmoCorner.TopRight:
                    startX = (UI.screenWidth - 10f) / scale - actualSize;
                    startY = 10f / scale;
                    break;
                case GizmoCorner.BottomRight:
                    startX = (UI.screenWidth - 10f) / scale - actualSize;
                    startY = (UI.screenHeight - 10f) / scale - actualSize;
                    break;
                case GizmoCorner.BottomLeft:
                    startX = 10f / scale;
                    startY = (UI.screenHeight - 10f) / scale - actualSize;
                    break;
                case GizmoCorner.TopLeft:
                    startX = 10f / scale;
                    startY = 10f / scale;
                    break;
            }

            float x = startX;
            float y = startY;

            float xStep = (PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.TopLeft || PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.BottomLeft) ? (actualSize + spacing) : -(actualSize + spacing);
            float yStep = (PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.TopLeft || PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.TopRight) ? (actualSize + spacing) : -(actualSize + spacing);

            float minX = startX - (actualSize + spacing) * 4f;
            float maxX = startX + (actualSize + spacing) * 4f;

            GizmoGridDrawer.drawnHotKeys.Clear();

            Gizmo interactedGizmo = null;
            Event interactedEvent = null;
            Gizmo floatMenuGizmo = null;
            bool isFirst = true;

            float boundsMinX = UI.screenWidth;
            float boundsMaxX = 0f;
            float boundsMinY = UI.screenHeight;
            float boundsMaxY = 0f;

            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            foreach (var cmd in gizmos)
            {
                float screenX = x * scale;
                float screenY = y * scale;
                float screenW = actualSize * scale;
                float screenH = actualSize * scale;

                boundsMinX = Mathf.Min(boundsMinX, screenX);
                boundsMaxX = Mathf.Max(boundsMaxX, screenX + screenW);
                boundsMinY = Mathf.Min(boundsMinY, screenY);
                boundsMaxY = Mathf.Max(boundsMaxY, screenY + screenH);

                GizmoRenderParms parms = default;
                parms.isFirst = isFirst;
                GizmoResult result = cmd.GizmoOnGUI(new Vector2(x, y), actualSize, parms);
                GenUI.AbsorbClicksInRect(new Rect(x, y, actualSize, actualSize));

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

                x += xStep;
                if ((xStep < 0 && x < minX) || (xStep > 0 && x > maxX))
                {
                    x = startX;
                    y += yStep;
                }
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
            var clickCell = UI.MouseCell();
            bool itemInRange = pawn.Position.DistanceTo(clickCell) <= PerspectiveShiftMod.settings.grabRange || clickCell.AdjacentTo8WayOrInside(pawn.Position);
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
            }

            var building = clickCell.GetFirstBuilding(pawn.Map);
            if (building != null && !building.Destroyed && building.Spawned)
            {
                if (InteractWith(building))
                {
                    LastManualTarget = building;
                    return true;
                }
            }

            IsAvatarLeftClick = true;
            var opts = FloatMenuMakerMap.GetOptions(new List<Pawn> { pawn }, clickCell.ToVector3Shifted(), out _);
            IsAvatarLeftClick = false;
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
                return true;
            }

            return false;
        }

        public bool InteractWith(Thing target)
        {
            if (target == null || target.Destroyed)
            {
                return false;
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
                        }
                    }
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
            var reserver = target.Map.reservationManager.FirstRespectedReserver(target, pawn);
            if (reserver != null && reserver != pawn)
            {
                target.Map.reservationManager.ReleaseAllForTarget(target);
            }

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

            var cellThings = cell.GetThingList(pawn.Map);
            if (cellThings != null)
            {
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
                if (blueprint != null && blueprint.ThingCountNeeded(CarriedThing.def) > 0)
                {
                    if (blueprint.TryReplaceWithSolidThing(pawn, out Thing frameThing, out _))
                    {
                        if (frameThing is Frame frame)
                        {
                            var needed = frame.ThingCountNeeded(CarriedThing.def);
                            if (needed > 0)
                            {
                                pawn.carryTracker.innerContainer.TryTransferToContainer(CarriedThing, frame.resourceContainer, Mathf.Min(CarriedThing.stackCount, needed));
                            }
                            return true;
                        }
                    }
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

            if (!itemInRange) return false;

            var slotGroup = pawn.Map.haulDestinationManager.SlotGroupAt(cell);
            if (slotGroup != null)
            {
                if (slotGroup.Settings.AllowedToAccept(CarriedThing))
                {
                    if (pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var _))
                    {
                        return true;
                    }
                }
            }

            if (cell.Walkable(pawn.Map))
            {
                if (pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var _))
                {
                    return true;
                }
            }

            return false;
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

                    if (building is Building_Storage || container != null)
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
            if (pawn == null || billGiver == null) return;
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
                var originalRadii = new Dictionary<Bill, float>();
                try
                {
                    foreach (var bill in billGiver.BillStack)
                    {
                        originalRadii[bill] = bill.ingredientSearchRadius;
                        bill.ingredientSearchRadius = 3f;
                    }

                    var job = workGiver.JobOnThing(pawn, billGiverThing, forced: true);
                    if (job != null)
                    {
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
                }
                finally
                {
                    foreach (var kvp in originalRadii)
                    {
                        kvp.Key.ingredientSearchRadius = kvp.Value;
                    }
                }
            }
        }

        private void DrawNeeds()
        {
            if (pawn?.needs == null || gizmoBounds == Rect.zero) return;

            var needs = pawn.needs.AllNeeds.Where(n => n.ShowOnNeedList && (n.def.major || n is Need_Mood)).ToList();
            if (!needs.Any()) return;

            float width = 200f;
            float height = 40f;
            float totalHeight = needs.Count * height;

            float startX = gizmoBounds.xMax - width - 10f;
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

            var unifiedBg = new Rect(startX - 10f, startY - 5f, width + 20f, totalHeight + 10f);
            Widgets.DrawBoxSolid(unifiedBg, new ColorInt(32, 32, 32).ToColor.WithAlpha(0.7f));

            float currentY = startY;
            foreach (var need in needs)
            {
                Rect needRect = new Rect(startX, currentY, width, height);
                need.DrawOnGUI(needRect, maxThresholdMarkers: int.MaxValue, customMargin: 4f, drawArrows: true, doTooltip: true, rectForTooltip: null, drawLabel: true);
                currentY += height;
            }
        }

        public void Tick()
        {
            if (pawn == null || pawn.Dead || pawn.Downed || pawn.GetLord() != null || pawn.mindState.duty != null) return;
            HandleDoorInteraction();
            HandleCombatStance();

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
                pawn.pather.nextCellCostTotal = 60f / Mathf.Max(baseSpeed, 0.1f);
            }
        }
    }
}
