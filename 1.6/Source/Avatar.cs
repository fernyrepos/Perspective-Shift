using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
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
        public Pawn pawn;
        public Vector3 moveInput;
        public bool isSprinting;
        public bool isWalking;
        public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
        public Vector3? physicsPosition;

        public Thing CarriedThing => pawn.carryTracker?.CarriedThing;

        private Building_Door interactingDoor;
        private bool wasMovingLastFrame;
        private Rect topRightGizmoBounds;
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

            if (pawn.InMentalState)
            {
                moveInput = Vector3.zero;
                if (wasMovingLastFrame)
                {
                    if (pawn.pather.Moving) pawn.pather.StopDead();
                    wasMovingLastFrame = false;
                }
                physicsPosition = null;
                return;
            }

            bool inCombatStance = pawn.stances.curStance is Stance_Warmup || pawn.stances.curStance is Stance_Cooldown;
            var canRunAndGun = ModCompatibility.IsRunAndGunActiveFor(pawn);
            bool isShootingStationary = inCombatStance && !canRunAndGun;

            if (State.ControlsFrozen || isShootingStationary)
            {
                moveInput = Vector3.zero;
                if (wasMovingLastFrame)
                {
                    if (pawn.pather.Moving) pawn.pather.StopDead();
                    wasMovingLastFrame = false;
                }
                return;
            }

            UpdateInput();
            ProcessMovement();
        }

        public void UpdateCamera()
        {
            if (pawn == null || pawn.Map != Find.CurrentMap) return;

            var driver = Find.CameraDriver;
            if (driver == null) return;

            Vector3 targetCamPos = pawn.DrawPos;
            var newPos = Vector3.Lerp(driver.rootPos, targetCamPos, 0.1f);

            var scroll = Input.GetAxis("Mouse ScrollWheel");
            float newSize = driver.desiredSize;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                newSize -= scroll * PerspectiveShiftMod.settings.zoomSpeed * 10f;
                newSize = Mathf.Clamp(newSize,
                    PerspectiveShiftMod.settings.minZoom,
                    PerspectiveShiftMod.settings.maxZoom);
            }

            driver.rootPos = newPos;
            driver.desiredSize = newSize;

            var cam = driver.GetComponent<Camera>();
            if (cam != null)
            {
                Vector3 finalPos = newPos;
                finalPos.y = 15f + (driver.RootSize - driver.config.sizeRange.min) / (driver.config.sizeRange.max - driver.config.sizeRange.min) * 50f;
                cam.transform.position = finalPos + driver.shaker.ShakeOffset;
                cam.orthographicSize = driver.RootSize;
            }
        }

        public bool HandleSelectorClick()
        {
            if (pawn == null || pawn.Dead || pawn.Downed) return false;
            if (pawn.InMentalState) return false;

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

        private void ProcessMovement()
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned) return;
            if (interactingDoor != null) return;

            if (!IsMoving)
            {
                if (wasMovingLastFrame)
                {
                    if (pawn.pather.Moving) pawn.pather.StopDead();
                    wasMovingLastFrame = false;
                }

                if (pawn.pather.Moving)
                {
                    physicsPosition = null;
                }

                if (pawn.Drafted && !pawn.InMentalState && !State.ControlsFrozen)
                {
                    Vector3 toMouse = UI.MouseMapPosition() - pawn.DrawPos;
                    if (toMouse.sqrMagnitude > 0.1f)
                        pawn.Rotation = Rot4.FromAngleFlat(NormAngle(Mathf.Atan2(toMouse.x, toMouse.z) * Mathf.Rad2Deg));
                }

                return;
            }

            if (!wasMovingLastFrame || !physicsPosition.HasValue)
            {
                physicsPosition = pawn.Drawer.tweener.TweenedPos;
                physicsPosition = new Vector3(physicsPosition.Value.x, pawn.def.Altitude, physicsPosition.Value.z);
                wasMovingLastFrame = true;
            }

            if (pawn.pather != null && pawn.pather.Moving) pawn.pather.StopDead();

            bool isShooting = pawn.stances.curStance is Stance_Warmup || pawn.stances.curStance is Stance_Cooldown;
            var canRunAndGun = ModCompatibility.IsRunAndGunActiveFor(pawn);

            if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def.playerInterruptible)
            {
                if (canRunAndGun && isShooting && pawn.Drafted)
                {

                }
                else
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }

            Vector3 deltaRaw = moveInput.normalized;

            Vector3 testNewPos = physicsPosition.Value + deltaRaw * 0.1f;

            var currentMultiplier = GetMovementSpeedMultiplier(pawn.Position);
            var futureMultiplier = GetMovementSpeedMultiplier(testNewPos.ToIntVec3());

            var terrainMultiplier = Mathf.Min(currentMultiplier, futureMultiplier);

            var baseSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed);
            float expectedTicks = 60f / Mathf.Max(baseSpeed, 0.1f);
            float tickModifier = expectedTicks / Mathf.Max(pawn.TicksPerMoveCardinal, 1f);

            float speed = baseSpeed
                          * terrainMultiplier
                          * tickModifier
                          * (isSprinting ? 1.3f : isWalking ? 0.5f : 1.0f)
                          * Time.deltaTime;

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

            if (isShooting && pawn.stances.curStance is Stance_Warmup warmup)
            {
                var targetPos = warmup.focusTarg.Thing?.DrawPos ?? warmup.focusTarg.Cell.ToVector3Shifted();
                Vector3 toTarget = targetPos - pawn.DrawPos;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    pawn.Rotation = Rot4.FromAngleFlat(NormAngle(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg));
                }
            }
            else if (isShooting && pawn.stances.curStance is Stance_Cooldown cooldown)
            {
                var targetPos = cooldown.focusTarg.Thing?.DrawPos ?? cooldown.focusTarg.Cell.ToVector3Shifted();
                Vector3 toTarget = targetPos - pawn.DrawPos;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    pawn.Rotation = Rot4.FromAngleFlat(NormAngle(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg));
                }
            }
            else
            {
                UpdateRotation(moveInput.normalized);
            }
        }

        private bool IsWalkableWithMargin(Vector3 pos)
        {
            float margin = 0.2f;
            var cCenter = pos.ToIntVec3();
            var c1 = new Vector3(pos.x + margin, pos.y, pos.z + margin).ToIntVec3();
            var c2 = new Vector3(pos.x - margin, pos.y, pos.z + margin).ToIntVec3();
            var c3 = new Vector3(pos.x + margin, pos.y, pos.z - margin).ToIntVec3();
            var c4 = new Vector3(pos.x - margin, pos.y, pos.z - margin).ToIntVec3();

            if (!IsWalkableCell(cCenter)) return false;
            if (c1 != cCenter && !IsWalkableCell(c1)) return false;
            if (c2 != cCenter && !IsWalkableCell(c2)) return false;
            if (c3 != cCenter && !IsWalkableCell(c3)) return false;
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
                if (!pawn.InMentalState)
                {
                    DrawTopRightGizmos();
                }

                bool mouseOverGizmo = MapGizmoUtility.LastMouseOverGizmo != null || topRightGizmoBounds.Contains(Event.current.mousePosition);
                bool mouseOverUI = IsMouseOverUI() || IsMouseOverColonistBar();

                if (pawn.Drafted && !pawn.InMentalState)
                {
                    if (mouseOverUI || mouseOverGizmo)
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

        private bool IsMouseOverUI()
        {
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 mouseInverted = UI.MousePositionOnUIInverted;

            if (topRightGizmoBounds.Contains(mousePos))
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
            else if (pawn.equipment?.PrimaryEq?.PrimaryVerb == null && (pawn.VerbTracker?.AllVerbs?.FirstOrDefault(v => v is Verb_MeleeAttack && v.Available()) == null))
            {
                color = Color.green;
                tex = ReticleTex;
            }
            else
            {
                Verb verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
                if (verb == null || verb.verbProps.IsMeleeAttack)
                {
                    verb = pawn.VerbTracker?.AllVerbs?.FirstOrDefault(v => v is Verb_MeleeAttack && v.Available());
                }

                if (verb != null)
                {
                    var target = UI.MouseCell();
                    float range = verb.verbProps.range;

                    if (pawn.Position.DistanceTo(target) > range || !GenSight.LineOfSight(pawn.Position, target, pawn.Map))
                    {
                        color = Color.red;
                        tex = ReticleNoLOSTex;
                    }
                    else
                    {
                        color = Color.green;
                        tex = ReticleTex;
                    }
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
            {
                verb = pawn.VerbTracker?.AllVerbs?.FirstOrDefault(v => v is Verb_MeleeAttack && v.Available());
            }
            if (verb == null) return;

            var targetCell = UI.MouseCell();
            var target = new LocalTargetInfo(targetCell);

            var things = targetCell.GetThingList(pawn.Map);
            Thing bestTarget = things?.FirstOrDefault(t => t is Pawn && t != pawn) ?? things?.FirstOrDefault(t => t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item);
            if (bestTarget != null) target = new LocalTargetInfo(bestTarget);

            Vector3 targetPos = target.Thing != null ? target.Thing.DrawPos : targetCell.ToVector3Shifted();
            Vector3 toTarget = targetPos - pawn.DrawPos;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                pawn.Rotation = Rot4.FromAngleFlat(NormAngle(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg));
            }

            if (verb.verbProps.IsMeleeAttack)
            {
                if (pawn.Position.AdjacentTo8WayOrInside(targetCell) && target.Thing != null)
                {
                    pawn.meleeVerbs.TryMeleeAttack(target.Thing);
                }
            }
            else
            {
                if (pawn.Position.DistanceTo(targetCell) <= verb.verbProps.range && GenSight.LineOfSight(pawn.Position, targetCell, pawn.Map))
                {
                    verb.TryStartCastOn(target, false, true);
                }
            }
        }

        public void DrawTopRightGizmos()
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

            float startX = (UI.screenWidth - 10f) / scale - actualSize;
            float startY = 10f / scale;
            float x = startX;
            float y = startY;
            float minX = startX - (actualSize + spacing) * 4f;

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
                x -= actualSize + spacing;
                if (x < minX)
                {
                    x = startX;
                    y += actualSize + spacing;
                }
            }

            GUI.matrix = prevMatrix;

            if (gizmos.Count > 0)
            {
                topRightGizmoBounds = new Rect(boundsMinX, boundsMinY, boundsMaxX - boundsMinX, boundsMaxY - boundsMinY);
            }
            else
            {
                topRightGizmoBounds = Rect.zero;
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

            var distance = pawn.Position.DistanceTo(clickCell);
            Log.Message($"[Avatar.HandleLeftClick] START - clickCell: {clickCell}, distance: {distance:F2}, grabRange: {PerspectiveShiftMod.settings.grabRange}, CarriedThing: {CarriedThing?.Label ?? "null"}");

            if (distance > PerspectiveShiftMod.settings.grabRange)
            {
                Log.Message("[Avatar.HandleLeftClick] RETURN: Out of grab range");
                return false;
            }

            if (CarriedThing != null)
            {
                Log.Message("[Avatar.HandleLeftClick] Hands are full, calling HandleDropOrInteract");
                return HandleDropOrInteract(clickCell);
            }
            else
            {
                var item = clickCell.GetFirstItem(pawn.Map);
                Log.Message($"[Avatar.HandleLeftClick] item found: {item?.Label ?? "null"}, category: {item?.def.category.ToString() ?? "null"}");

                if (item != null && item.def.category == ThingCategory.Item)
                {
                    Log.Message($"[Avatar.HandleLeftClick] Calling ExecutePickup for: {item.Label}");
                    ExecutePickup(item);
                    return true;
                }
            }

            var building = clickCell.GetFirstBuilding(pawn.Map);
            if (building != null && !building.Destroyed && building.Spawned)
            {
                var job = TryGetJobFromWorkGivers(pawn, building);
                if (job != null)
                {
                    Log.Message($"[Avatar.HandleLeftClick] WorkGiver found job: {job.def.defName} on {building.Label}. Starting directly.");
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    return true;
                }
            }

            if (building != null && !building.Destroyed && building.Spawned)
            {
                var sleepOrJoyJob = TryGetSleepOrJoyJob(pawn, building);
                if (sleepOrJoyJob != null)
                {
                    Log.Message($"[Avatar.HandleLeftClick] Found Sleep/Joy job: {sleepOrJoyJob.def.defName} on {building.Label}. Starting directly.");
                    pawn.jobs.TryTakeOrderedJob(sleepOrJoyJob, JobTag.Misc);
                    return true;
                }
            }

            var opts = FloatMenuMakerMap.GetOptions(new List<Pawn> { pawn }, clickCell.ToVector3Shifted(), out _);
            Log.Message($"[Avatar.HandleLeftClick] float menu options: {opts?.Count ?? 0}");
            if (opts != null)
            {
                for (int i = 0; i < opts.Count; i++)
                {
                    var opt = opts[i];
                    Log.Message($"[Avatar.HandleLeftClick]   [{i}] Label: {opt.Label}, Priority: {opt.Priority}, Disabled: {opt.Disabled}");
                }
            }

            var bestOption = opts.Where(opt => !opt.Disabled)
                                 .OrderByDescending(opt => opt.Priority)
                                 .ThenByDescending(opt => opt.Priority != MenuOptionPriority.GoHere)
                                 .FirstOrDefault();
            if (bestOption != null)
            {
                Log.Message($"[Avatar.HandleLeftClick] invoking best option: {bestOption.Label}, priority: {bestOption.Priority}");
                bestOption.action.Invoke();
                return true;
            }

            Log.Message("[Avatar.HandleLeftClick] RETURN: No action taken");
            return false;
        }

        private Job TryGetJobFromWorkGivers(Pawn pawn, Thing thing)
        {
            if (pawn.workSettings == null) return null;

            List<WorkGiver> workGivers = pawn.workSettings.WorkGiversInOrderNormal;
            for (int i = 0; i < workGivers.Count; i++)
            {
                WorkGiver workGiver = workGivers[i];

                if (!(workGiver is WorkGiver_Scanner scanner)) continue;

                if (scanner.PotentialWorkThingRequest.Accepts(thing))
                {
                    if (!scanner.HasJobOnThing(pawn, thing, forced: true)) continue;

                    var job = scanner.JobOnThing(pawn, thing, forced: true);

                    if (job != null)
                    {
                        return job;
                    }
                }
            }
            return null;
        }

        private Job TryGetSleepOrJoyJob(Pawn pawn, Thing thing)
        {
            if (thing is Building_Bed bed && !bed.ForPrisoners && !bed.Medical && pawn.needs?.rest != null)
            {
                if (RestUtility.CanUseBedEver(pawn, bed.def) && pawn.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Deadly))
                {
                    return JobMaker.MakeJob(JobDefOf.LayDown, bed);
                }
            }

            if (pawn.needs?.joy != null)
            {
                var joyGivers = DefDatabase<JoyGiverDef>.AllDefsListForReading
                    .Where(jg => jg.Worker is JoyGiver_InteractBuilding && jg.thingDefs != null && jg.thingDefs.Contains(thing.def));

                foreach (var jgDef in joyGivers)
                {
                    if (jgDef.Worker is JoyGiver_InteractBuilding worker)
                    {
                        Job joyJob = worker.TryGivePlayJob(pawn, thing);
                        if (joyJob != null)
                        {
                            joyJob.playerForced = true;
                            return joyJob;
                        }
                    }
                }
            }

            return null;
        }

        private void ExecutePickup(Thing target)
        {
            Log.Message($"[Avatar.ExecutePickup] START - target: {target?.Label}, stackCount: {target?.stackCount ?? 0}");

            var pickedUpCount = pawn.carryTracker.TryStartCarry(target, target.stackCount);
            Log.Message($"[Avatar.ExecutePickup] TryStartCarry result: {pickedUpCount}");

            if (pickedUpCount > 0)
            {
                if (target.def.soundPickup != null)
                    target.def.soundPickup.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                Log.Message("[Avatar.ExecutePickup] SUCCESS - Item picked up");
            }
            else
            {
                Log.Message("[Avatar.ExecutePickup] FAILED - TryStartCarry returned 0");
            }
        }

        private bool HandleDropOrInteract(IntVec3 cell)
        {
            Log.Message($"[Avatar.HandleDropOrInteract] START - cell: {cell}, CarriedThing: {CarriedThing?.Label ?? "null"}");

            if (!cell.InBounds(pawn.Map))
            {
                Log.Message("[Avatar.HandleDropOrInteract] RETURN: cell not in bounds");
                return false;
            }

            var building = cell.GetFirstBuilding(pawn.Map);
            Log.Message($"[Avatar.HandleDropOrInteract] building at cell: {building?.Label ?? "null"}");

            if (building is IBillGiver billGiver)
            {
                Log.Message("[Avatar.HandleDropOrInteract] Building is IBillGiver, trying TryDepositIntoBill");
                if (TryDepositIntoBill(billGiver, building))
                {
                    Log.Message("[Avatar.HandleDropOrInteract] RETURN: Success from TryDepositIntoBill");
                    return true;
                }
            }

            var slotGroup = pawn.Map.haulDestinationManager.SlotGroupAt(cell);
            if (slotGroup != null)
            {
                if (slotGroup.Settings.AllowedToAccept(CarriedThing))
                {
                    Log.Message("[Avatar.HandleDropOrInteract] Cell is valid storage for item, trying to drop");
                    if (pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var _))
                    {
                        Log.Message("[Avatar.HandleDropOrInteract] RETURN: Success - Dropped in storage");
                        return true;
                    }
                }
                else
                {
                    Log.Message("[Avatar.HandleDropOrInteract] Cell is storage but does NOT accept this item.");
                }
            }

            if (cell.Walkable(pawn.Map))
            {
                Log.Message("[Avatar.HandleDropOrInteract] Cell is walkable, trying to drop on ground");
                if (pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var _))
                {
                    Log.Message("[Avatar.HandleDropOrInteract] RETURN: Success - Dropped on ground");
                    return true;
                }
            }

            Log.Message("[Avatar.HandleDropOrInteract] RETURN: Failed to drop anywhere");
            return false;
        }

        private bool TryDepositIntoBill(IBillGiver billGiver, Building building)
        {
            Thing heldItem = CarriedThing;
            if (heldItem == null) return false;

            Log.Message($"[Avatar.TryDepositIntoBill] START - heldItem: {heldItem.Label}, building: {building?.Label ?? "null"}");

            foreach (var bill in billGiver.BillStack)
            {
                Log.Message($"[Avatar.TryDepositIntoBill] Checking bill: {bill.Label}, ShouldDoNow: {bill.ShouldDoNow()}, IsFixedOrAllowedIngredient: {bill.IsFixedOrAllowedIngredient(heldItem)}");

                if (bill.ShouldDoNow() && bill.IsFixedOrAllowedIngredient(heldItem))
                {
                    IntVec3 placeLoc = building.InteractionCell;
                    Log.Message($"[Avatar.TryDepositIntoBill] Bill matches! placeLoc: {placeLoc}");

                    var container = building.TryGetInnerInteractableThingOwner();
                    Log.Message($"[Avatar.TryDepositIntoBill] container: {container != null}, building is Building_Storage: {building is Building_Storage}");

                    if (building is Building_Storage || container != null)
                    {
                        int transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(heldItem, container, heldItem.stackCount);
                        Log.Message($"[Avatar.TryDepositIntoBill] TryTransferToContainer result: {transferred}");

                        if (transferred > 0)
                        {
                            Log.Message("[Avatar.TryDepositIntoBill] SUCCESS - Transferred to container");
                            TryStartBillJob(billGiver);
                            return true;
                        }
                    }

                    if (pawn.carryTracker.TryDropCarriedThing(placeLoc, ThingPlaceMode.Direct, out Thing droppedItem))
                    {
                        Log.Message($"[Avatar.TryDepositIntoBill] SUCCESS - Dropped on floor: {droppedItem?.Label ?? "null"}");
                        TryStartBillJob(billGiver);
                        return true;
                    }
                }
            }

            Log.Message("[Avatar.TryDepositIntoBill] RETURN: No matching bill or deposit failed");
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
                        Log.Message($"[Avatar] Auto-starting bill job: {job.def.defName} on {billGiverThing.Label}");
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
                    else
                    {
                        Log.Message($"[Avatar] JobOnThing returned null (ingredients likely missing locally)");
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
            else
            {
                Log.Message($"[Avatar] No WorkGiver_DoBill found for {billGiverThing.Label}");
            }
        }

        public void Tick()
        {
            if (pawn == null || pawn.Dead || pawn.Downed) return;
            HandleDoorInteraction();
            HandleCombatStance();
        }
    }
}
