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
        public static bool DrawingAvatarNeeds = false;
        private Rect gizmoBounds;
        private List<object> prevSelected;
        private List<Gizmo> _cachedGizmos = [];
        private Thing _lastGizmoSource;
        private int _lastGizmoCacheFrame = -999;
        private Rect needsBounds;
        private Rect rotateButtonRect;
        private Rect scaleGripRect;
        private bool resizingUI;
        private Vector2 resizeStartMouse;
        private float resizeStartScale;
        private Thing equipHintThing;
        private string equipHintLabel;
        private Texture2D equipHintIcon;
        private IntVec3 equipHintCell = IntVec3.Invalid;
        private IntVec3 equipHintPawnCell = IntVec3.Invalid;
        private string equipHintTitle;
        private string equipHintQuality;
        private readonly HintStat[] equipHintStats = new HintStat[4];
        private int equipHintStatCount;
        private Vector2 equipHintLabelSize;
        private Vector2 equipHintTitleSize;
        private Vector2 equipHintQualitySize;
        private const float HintScale = 0.858f;
        private const int HintStatMaxCols = 2;
        private const float HintStatColGap = 14f;
        private readonly float[] equipHintColLabelW = new float[HintStatMaxCols];
        private readonly float[] equipHintColValueW = new float[HintStatMaxCols];
        private readonly float[] equipHintColDeltaW = new float[HintStatMaxCols];
        private int equipHintStatCols;
        private int equipHintStatRows;
        private float equipHintStatRowH;

        private const float MinAvatarUIScale = 0.5f;
        private const float MaxAvatarUIScale = 1.5f;

        private static readonly Color NeedsPanelColor = new ColorInt(32, 32, 32).ToColor.WithAlpha(0.7f);
        private static readonly Color EquipHintColor = new ColorInt(14, 14, 14).ToColor.WithAlpha(0.45f);
        private static readonly Color EquipHintLineColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color HintTitleColor = new Color(0.94f, 0.94f, 0.92f);
        private static readonly Color HintBetterColor = new Color(0.56f, 0.80f, 0.56f);
        private static readonly Color HintWorseColor = new Color(0.85f, 0.53f, 0.53f);
        private static readonly Color HintDividerColor = new Color(1f, 1f, 1f, 0.13f);
        private const float HintArrowSize = 8f;
        private static readonly Color GripIdleColor = new Color(1f, 1f, 1f, 0.45f);

        private static Texture2D _wearIcon;
        private static Texture2D WearIcon => _wearIcon ??= ContentFinder<Texture2D>.Get("Storage/Wear");

        private static Texture2D _equipIcon;
        private static Texture2D EquipIcon => _equipIcon ??= ContentFinder<Texture2D>.Get("Storage/Equip");

        private static Texture2D _harvestIcon;
        private static Texture2D HarvestIcon => _harvestIcon ??= ContentFinder<Texture2D>.Get("Storage/Hold");

        private static Texture2D _eatIcon;
        private static Texture2D EatIcon => _eatIcon ??= ContentFinder<Texture2D>.Get("Storage/Eat");

        private static Texture2D _arrowUpIcon;
        private static Texture2D ArrowUpIcon => _arrowUpIcon ??= ContentFinder<Texture2D>.Get("UI/Buttons/ReorderUp");

        private static Texture2D _arrowDownIcon;
        private static Texture2D ArrowDownIcon => _arrowDownIcon ??= ContentFinder<Texture2D>.Get("UI/Buttons/ReorderDown");

        private static Texture2D _rotateIcon;
        private static Texture2D RotateIcon => _rotateIcon ??= ContentFinder<Texture2D>.Get("UI/Widgets/RotRight");
        private static float AvatarUIScale => PerspectiveShiftMod.settings.avatarUIScale;

        public void OnGUI()
        {
            DebugLog();
            DrawCameraLockReturnButton();
            DrawGizmosAndNeeds();
            if (!State.ControlsFrozen)
            {
                HandleTabKeyBindings();
                HandleEatFoodBinding();
                HandleRecreationBinding();
            }
            bool mouseOverGizmo = MapGizmoUtility.LastMouseOverGizmo != null || gizmoBounds.Contains(UI.MousePositionOnUIInverted);
            bool mouseOverUI = IsMouseOverUI() || IsMouseOverColonistBar();
            DrawEquipHint(mouseOverUI || mouseOverGizmo);
            HandleHoldToFire(mouseOverGizmo, mouseOverUI);
            UpdateCursorAndReticle(mouseOverGizmo, mouseOverUI);
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
            tweener.lastTickSpringPos = tweener.tweenedPos;
            tweener.tweenedPos = physicsPosition.Value;
            tweener.lastDrawFrame = RealTime.frameCount;
            tweener.lastDrawTick = GenTicks.TicksGame;
        }

        private void DrawCameraLockReturnButton()
        {
            if (!State.CameraLockPosition.HasValue) return;

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

        private void DrawGizmosAndNeeds()
        {
            if (pawn.InMentalState) return;

            bool hideForTab = PerspectiveShiftMod.settings.gizmoCorner == GizmoCorner.BottomLeft && Find.MainTabsRoot?.OpenTab != null;
            if (hideForTab) return;

            if (!PerspectiveShiftMod.settings.disableCustomGizmos)
            {
                DrawPlayerGizmos();
                DrawNeeds();
                DrawCornerRotateButton();
                DrawScaleGrip();
            }
        }

        private void HandleTabKeyBindings()
        {
            TryToggleInspectTab(DefsOf.PS_OpenGearTab, typeof(ITab_Pawn_Gear));
            TryToggleInspectTab(DefsOf.PS_HealthTab, typeof(ITab_Pawn_Health));
            TryToggleInspectTab(DefsOf.PS_NeedsTab, typeof(ITab_Pawn_Needs));
        }

        private void HandleRecreationBinding()
        {
            if (!DefsOf.PS_DoRecreation.KeyDownEvent) return;

            bool onlyAvatarSelected = Find.Selector.NumSelected == 0 || (Find.Selector.NumSelected == 1 && Find.Selector.IsSelected(pawn));
            if (!onlyAvatarSelected || pawn.Downed || pawn.InMentalState || passedOut || pawn.needs?.joy == null) return;

            var jobGiver = new JobGiver_GetJoy();
            jobGiver.ResolveReferences();
            var job = jobGiver.TryGiveJob(pawn);
            if (job != null)
            {
                job.playerForced = true;
                pawn.jobs.TryTakeOrderedJob(job);
                Messages.Message("PS_DoingRecreation".Translate(job.def.reportString), pawn, MessageTypeDefOf.TaskCompletion, false);
                Event.current.Use();
            }
            else
            {
                Messages.Message("PS_NoRecreationAvailable".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                Event.current.Use();
            }
        }

        private void HandleEatFoodBinding()
        {
            if (!DefsOf.PS_EatFood.KeyDownEvent) return;

            bool onlyAvatarSelected = Find.Selector.NumSelected == 0 || (Find.Selector.NumSelected == 1 && Find.Selector.IsSelected(pawn));

            if (!onlyAvatarSelected || pawn.Downed || pawn.InMentalState || passedOut || pawn.needs?.food == null) return;

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
            if (CarriedThing != null && CarriedThing.def.IsNutritionGivingIngestible && pawn.WillEat(CarriedThing, pawn, true))
            {
                foodSource = CarriedThing;
                foodDef = FoodUtility.GetFinalIngestibleDef(foodSource);
            }
            else
            {
                FoodUtility.TryFindBestFoodSourceFor(pawn, pawn, desperate, out foodSource, out foodDef, canRefillDispenser: false, canUseInventory: true, canUsePackAnimalInventory: true, allowForbidden: false, allowCorpse, allowSociallyImproper: false, pawn.IsWildMan(), forceScanWholeMap: true, ignoreReservations: false, calculateWantedStackCount: false, allowVenerated: false, minPrefOverride: foodPreferability);
            }

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

        private void TryToggleInspectTab(KeyBindingDef keyDef, Type tabType)
        {
            if (!keyDef.KeyDownEvent) return;
            if (Find.DesignatorManager.SelectedDesignator != null || Find.Targeter.targetingSource != null) return;
            if (Find.Selector.SelectedPawns.Contains(pawn))
            {
                if (Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect)
                {
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect);
                }
                var inspectPane = (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
                var tab = inspectPane.CurTabs.FirstOrDefault(t => tabType.IsAssignableFrom(t.GetType()));
                if (tab != null)
                {
                    var actualType = tab.GetType();
                    if (InspectPaneUtility.IsOpen(tab, inspectPane))
                    {
                        inspectPane.CloseOpenTab();
                        Find.Selector.Deselect(pawn);
                    }
                    else
                    {
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(pawn);
                        InspectPaneUtility.OpenTab(actualType);
                    }
                }
            }
            else
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(pawn);
                if (Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect)
                {
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect);
                }
                var inspectPane = (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
                var tab = inspectPane.CurTabs.FirstOrDefault(t => tabType.IsAssignableFrom(t.GetType()));
                var actualType = tab.GetType();
                InspectPaneUtility.OpenTab(actualType);
            }
            Event.current.Use();
            if (pawn.Drafted)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            }
        }

        private void DebugLog()
        {
            bool shouldShowOverlay = false;
            if (shouldShowOverlay)
            {
                string text = $"UI.MousePositionOnUIInverted={UI.MousePositionOnUIInverted}\n" +
              $"Input.mousePosition={new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)}";
                Rect rect = new Rect(10, 10, 400, 60);
                Widgets.DrawWindowBackground(rect);
                Widgets.Label(rect, text);
            }
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
            bool shouldLogJobs = false;
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
            Vector2 mousePos = UI.MousePositionOnUIInverted;
            Vector2 mouseInverted = UI.MousePositionOnUIInverted;

            if (gizmoBounds.Contains(mousePos))
                return true;

            if (resizingUI || rotateButtonRect.Contains(mousePos) || scaleGripRect.Contains(mousePos))
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
            Vector2 mousePos = UI.MousePositionOnUIInverted;
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
                prevSelected = [.. Find.Selector.SelectedObjects];
                Find.Selector.selected.Clear();
                Find.Selector.selected.Add(gizmoSource);
            }

            if (gizmoSource != _lastGizmoSource || Time.frameCount - _lastGizmoCacheFrame >= 30)
            {
                _cachedGizmos = gizmoSource.GetGizmos()
                    .Distinct()
                    .OrderBy(g => g.Order)
                    .ToList();
                _lastGizmoSource = gizmoSource;
                _lastGizmoCacheFrame = Time.frameCount;
            }

            if (!wasSelected)
            {
                Find.Selector.selected.Clear();
                Find.Selector.selected.AddRange(prevSelected);
            }
            State.DrawingTopRightGizmos = false;

            var gizmos = _cachedGizmos.Where(g => g.Visible).ToList();

            float s = 0.85f * AvatarUIScale;
            float scale = s * Prefs.UIScale;
            float actualSize = 75f;
            float spacing = 8f;

            float startX = 0f;
            float startY = 0f;

            float mainButtonHeight = 45f;

            switch (PerspectiveShiftMod.settings.gizmoCorner)
            {
                case GizmoCorner.TopRight:
                    startX = (UI.screenWidth - 10f) / s - actualSize;
                    startY = 10f / s;
                    break;
                case GizmoCorner.BottomRight:
                    startX = (UI.screenWidth - 10f) / s - actualSize;
                    startY = (UI.screenHeight - 10f - mainButtonHeight) / s - actualSize;
                    break;
                case GizmoCorner.BottomLeft:
                    startX = 10f / s;
                    startY = (UI.screenHeight - 10f - mainButtonHeight) / s - actualSize;
                    break;
                case GizmoCorner.TopLeft:
                    startX = 10f / s;
                    startY = 10f / s;
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

            bool suppressHotkeys = (Find.Selector.NumSelected > 0 && !Find.Selector.IsSelected(pawn)) || State.ControlsFrozen;

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
                float screenX = drawX * s;
                float screenY = y * s;
                float screenW = gizmoWidth * s;
                float screenH = actualSize * s;

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

        private void DrawNeeds()
        {
            if (Event.current.type == EventType.Layout) return;

            needsBounds = Rect.zero;
            if (pawn.needs == null || gizmoBounds == Rect.zero) return;

            var needs = pawn.needs.AllNeeds
                .Where(n => PerspectiveShiftMod.settings.pinnedNeeds.Contains(n.def.defName))
                .ToList();
            if (!needs.Any()) return;

            float uiScale = AvatarUIScale;
            float width = 200f;
            float height = 40f;
            float totalHeight = needs.Count * height;
            float drawnWidth = width * uiScale;
            float drawnHeight = totalHeight * uiScale;

            var corner = PerspectiveShiftMod.settings.gizmoCorner;
            var startX = Mathf.Min(gizmoBounds.xMax - drawnWidth - 10f, UI.screenWidth - drawnWidth - 10f);
            float startY = gizmoBounds.yMax + 35f;

            if (corner == GizmoCorner.BottomRight)
            {
                startY = gizmoBounds.yMin - drawnHeight - 10f;
            }
            else if (corner == GizmoCorner.BottomLeft)
            {
                startX = gizmoBounds.xMin + 10f;
                startY = gizmoBounds.yMin - drawnHeight - 10f;
            }
            else if (corner == GizmoCorner.TopLeft)
            {
                startX = gizmoBounds.xMin + 10f;
            }

            var unifiedBg = new Rect(startX - 20f * uiScale, startY - 5f * uiScale, drawnWidth + 30f * uiScale, drawnHeight + 10f * uiScale);
            Widgets.DrawBoxSolid(unifiedBg, NeedsPanelColor);
            needsBounds = unifiedBg;

            bool scaled = !Mathf.Approximately(uiScale, 1f);
            Matrix4x4 prevMatrix = GUI.matrix;
            if (scaled)
            {
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale * Prefs.UIScale, uiScale * Prefs.UIScale, 1f));
            }

            DrawingAvatarNeeds = true;
            float localX = scaled ? startX / uiScale : startX;
            float currentY = scaled ? startY / uiScale : startY;
            foreach (var need in needs)
            {
                Rect needRect = new Rect(localX, currentY, width, height);
                need.DrawOnGUI(needRect, maxThresholdMarkers: int.MaxValue, customMargin: 4f, drawArrows: true, doTooltip: true, rectForTooltip: null, drawLabel: true);
                currentY += height;
            }
            DrawingAvatarNeeds = false;

            if (scaled) GUI.matrix = prevMatrix;
        }

        private void DrawEquipHint(bool mouseOverUI)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (!ShowWeaponHints && !ShowApparelHints && !ShowEatHints && !PerspectiveShiftMod.settings.harvestTooltips) return;
            if (mouseOverUI || CarriedThing != null || pawn.Map == null) return;

            var cell = UI.MouseCell();
            if (cell != equipHintCell || pawn.Position != equipHintPawnCell)
            {
                equipHintCell = cell;
                equipHintPawnCell = pawn.Position;
                equipHintThing = FindHintTargetAt(cell, out equipHintLabel, out equipHintIcon);
                MeasureHintLayout();
            }

            var thing = equipHintThing;
            if (thing == null || !thing.Spawned) return;
            if (IsHandlingHintTarget(thing)) return;

            const float pad = 10f;
            bool detailed = equipHintTitle != null;
            bool hasIcon = equipHintIcon != null;
            float iconSize = Mathf.Min(equipHintLabelSize.y - 1f, 16f);
            float iconAdvance = hasIcon ? iconSize + 5f : 0f;
            float actionRowH = equipHintLabelSize.y + 2f;

            float contentW = equipHintLabelSize.x + iconAdvance;
            float boxHeight = actionRowH;

            float titleRowH = 0f;
            float statsBlockH = 0f;
            if (detailed)
            {
                titleRowH = equipHintTitleSize.y + 2f;
                float titleW = equipHintTitleSize.x + (equipHintQuality != null ? equipHintQualitySize.x + 6f : 0f);
                float statsW = 0f;
                for (int c = 0; c < equipHintStatCols; c++)
                {
                    if (c > 0) statsW += HintStatColGap;
                    statsW += HintColWidth(c);
                }
                contentW = Mathf.Max(contentW, Mathf.Max(titleW, statsW));
                statsBlockH = (equipHintStatRowH + 2f) * equipHintStatRows;
                boxHeight = titleRowH + 4f + statsBlockH + 4f + actionRowH + 8f;
            }

            float boxWidth = contentW + pad * 2f;
            var anchor = (thing.DrawPos + new Vector3(0f, 0f, 0.35f)).MapToUIPosition();
            const float leaderLength = 10f;
            var boxRect = new Rect(anchor.x - boxWidth / 2f, anchor.y - leaderLength - boxHeight, boxWidth, boxHeight);
            boxRect.x = Mathf.Clamp(boxRect.x, 4f, Mathf.Max(4f, UI.screenWidth - boxWidth - 4f));
            boxRect.y = Mathf.Max(boxRect.y, 4f);

            Matrix4x4 prevMatrix = GUI.matrix;
            var pivot = new Vector3(anchor.x, anchor.y, 0f);
            GUI.matrix = prevMatrix
                * Matrix4x4.TRS(pivot, Quaternion.identity, Vector3.one)
                * Matrix4x4.Scale(new Vector3(HintScale, HintScale, 1f))
                * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);

            Widgets.DrawBoxSolid(boxRect, EquipHintColor);
            Widgets.DrawLine(new Vector2(anchor.x, boxRect.yMax), new Vector2(anchor.x, anchor.y), EquipHintLineColor, 1f);

            float x = boxRect.x + pad;
            float y = boxRect.y + (detailed ? 4f : 0f);
            bool prevWrap = Text.WordWrap;
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (detailed)
            {
                Text.Font = GameFont.Small;
                GUI.color = HintTitleColor;
                Widgets.Label(new Rect(x, y, equipHintTitleSize.x, titleRowH), equipHintTitle);
                if (equipHintQuality != null)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = ColoredText.SubtleGrayColor;
                    Widgets.Label(new Rect(x + equipHintTitleSize.x + 6f, y, equipHintQualitySize.x, titleRowH), equipHintQuality);
                }
                GUI.color = Color.white;
                y += titleRowH + 2f;

                Widgets.DrawLineHorizontal(boxRect.x + pad, y, contentW, HintDividerColor);
                y += 2f;

                Text.Font = GameFont.Tiny;
                float colX = x;
                for (int c = 0; c < equipHintStatCols; c++)
                {
                    if (c > 0) colX += HintStatColGap;
                    for (int i = c * equipHintStatRows; i < equipHintStatCount && i < (c + 1) * equipHintStatRows; i++)
                    {
                        float rowY = y + (i - c * equipHintStatRows) * (equipHintStatRowH + 2f);
                        DrawHintStatRow(equipHintStats[i], colX, rowY, equipHintStatRowH, c);
                    }
                    colX += HintColWidth(c);
                }
                y += statsBlockH;
                y += 4f;
            }

            Text.Font = GameFont.Tiny;
            if (hasIcon)
            {
                GUI.DrawTexture(new Rect(x, y + (actionRowH - iconSize) / 2f, iconSize, iconSize), equipHintIcon);
            }
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(x + iconAdvance, y, equipHintLabelSize.x, actionRowH), equipHintLabel);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            Text.WordWrap = prevWrap;
            GUI.matrix = prevMatrix;
        }

        private float HintColWidth(int col)
        {
            float w = equipHintColLabelW[col] + 6f + equipHintColValueW[col];
            if (equipHintColDeltaW[col] > 0f) w += 8f + equipHintColDeltaW[col];
            return w;
        }

        private void DrawHintStatRow(HintStat stat, float x, float y, float rowHeight, int col)
        {
            float labelW = equipHintColLabelW[col];
            float valueW = equipHintColValueW[col];

            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(x, y, labelW, rowHeight), stat.label);

            GUI.color = Color.white;
            Widgets.Label(new Rect(x + labelW + 6f, y, valueW, rowHeight), stat.value);

            if (stat.sign != 0)
            {
                float deltaX = x + labelW + 6f + valueW + 8f;
                GUI.color = stat.sign > 0 ? HintBetterColor : HintWorseColor;
                GUI.DrawTexture(new Rect(deltaX, y + (rowHeight - HintArrowSize) / 2f, HintArrowSize, HintArrowSize),
                    stat.sign > 0 ? ArrowUpIcon : ArrowDownIcon);
                Widgets.Label(new Rect(deltaX + HintArrowSize + 3f, y, stat.deltaWidth, rowHeight), stat.delta);
            }

            GUI.color = Color.white;
        }

        private void MeasureHintLayout()
        {
            equipHintLabelSize = Vector2.zero;
            equipHintTitleSize = Vector2.zero;
            equipHintQualitySize = Vector2.zero;
            for (int c = 0; c < HintStatMaxCols; c++)
            {
                equipHintColLabelW[c] = 0f;
                equipHintColValueW[c] = 0f;
                equipHintColDeltaW[c] = 0f;
            }
            equipHintStatRowH = 0f;
            equipHintStatCols = equipHintStatCount > 2 ? 2 : 1;
            equipHintStatRows = equipHintStatCols > 0 ? Mathf.CeilToInt(equipHintStatCount / (float)equipHintStatCols) : 0;
            if (equipHintThing == null) return;

            var prevFont = Text.Font;
            bool prevWrap = Text.WordWrap;
            Text.WordWrap = false;

            if (equipHintTitle != null)
            {
                Text.Font = GameFont.Small;
                equipHintTitleSize = MeasureHintText(equipHintTitle);
            }

            Text.Font = GameFont.Tiny;
            equipHintLabelSize = MeasureHintText(equipHintLabel);
            if (equipHintQuality != null) equipHintQualitySize = MeasureHintText(equipHintQuality);
            for (int i = 0; i < equipHintStatCount; i++)
            {
                int col = i / equipHintStatRows;
                var labelSize = MeasureHintText(equipHintStats[i].label);
                var valueSize = MeasureHintText(equipHintStats[i].value);
                equipHintColLabelW[col] = Mathf.Max(equipHintColLabelW[col], labelSize.x);
                equipHintColValueW[col] = Mathf.Max(equipHintColValueW[col], valueSize.x);
                equipHintStatRowH = Mathf.Max(equipHintStatRowH, Mathf.Max(labelSize.y, valueSize.y));
                if (equipHintStats[i].sign != 0)
                {
                    equipHintStats[i].deltaWidth = MeasureHintText(equipHintStats[i].delta).x;
                    equipHintColDeltaW[col] = Mathf.Max(equipHintColDeltaW[col], equipHintStats[i].deltaWidth + HintArrowSize + 3f);
                }
            }

            Text.WordWrap = prevWrap;
            Text.Font = prevFont;
        }

        private static Vector2 MeasureHintText(string text)
        {
            if (text.NullOrEmpty()) return Vector2.zero;
            var size = Text.CalcSize(text);
            size.x += 2f;
            return size;
        }

        private Thing FindHintTargetAt(IntVec3 cell, out string label, out Texture2D icon)
        {
            label = null;
            icon = null;
            if (!cell.InBounds(pawn.Map)) return null;

            bool weaponHints = ShowWeaponHints;
            bool apparelHints = ShowApparelHints;
            bool eatHints = ShowEatHints;
            var things = cell.GetThingList(pawn.Map);
            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (thing.def.category != ThingCategory.Item) continue;
                if (!IsTargetInRange(thing)) continue;

                bool isApparel = thing is Apparel;
                if ((isApparel ? apparelHints : weaponHints) && !thing.def.IsStuff && TryMakeWearOrEquipJob(pawn, thing, out _))
                {
                    label = (isApparel ? "PS_DoubleClickToWear" : "PS_DoubleClickToEquip").Translate();
                    icon = isApparel ? WearIcon : EquipIcon;
                    BuildGearHintStats(thing);
                    return thing;
                }

                if (eatHints && TryMakeIngestJob(pawn, thing, out _))
                {
                    label = "PS_DoubleClickToEat".Translate();
                    icon = EatIcon;
                    BuildFoodHintStats(thing);
                    return thing;
                }
            }

            if (PerspectiveShiftMod.settings.harvestTooltips
                && pawn.Position.DistanceTo(cell) <= PerspectiveShiftMod.settings.grabRange)
            {
                var plant = cell.GetPlant(pawn.Map);
                if (plant != null && CanHarvestNow(plant))
                {
                    label = (PerspectiveShiftMod.settings.requireHeldClickForJobs ? "PS_ClickAndHoldToHarvest" : "PS_ClickToHarvest").Translate();
                    icon = HarvestIcon;
                    BuildHarvestHintStats(plant);
                    return plant;
                }
            }

            equipHintTitle = null;
            equipHintQuality = null;
            equipHintStatCount = 0;
            return null;
        }

        private bool IsHandlingHintTarget(Thing thing)
        {
            var job = pawn.CurJob;
            if (job == null) return false;
            if (job.def != JobDefOf.Wear && job.def != JobDefOf.Equip && job.def != JobDefOf.Ingest && job.def != JobDefOf.Harvest) return false;
            return job.targetA.Thing == thing;
        }

        private static bool ShowWeaponHints
        {
            get
            {
                var settings = PerspectiveShiftMod.settings;
                return settings.weaponTooltips && !settings.disableDoubleClickEquip;
            }
        }

        private static bool ShowApparelHints
        {
            get
            {
                var settings = PerspectiveShiftMod.settings;
                return settings.apparelTooltips && !settings.disableDoubleClickEquip;
            }
        }

        private static bool ShowEatHints
        {
            get
            {
                var settings = PerspectiveShiftMod.settings;
                return settings.eatTooltips && !settings.disableDoubleClickEat;
            }
        }

        private void DrawCornerRotateButton()
        {
            if (Event.current.type == EventType.Layout) return;

            rotateButtonRect = Rect.zero;
            if (needsBounds == Rect.zero) return;

            float uiScale = AvatarUIScale;
            rotateButtonRect = new Rect(needsBounds.xMin + uiScale, needsBounds.yMin + uiScale, 18f * uiScale, 18f * uiScale);

            if (Widgets.ButtonImage(rotateButtonRect, RotateIcon))
            {
                var corner = PerspectiveShiftMod.settings.gizmoCorner;
                PerspectiveShiftMod.settings.gizmoCorner = (GizmoCorner)(((int)corner + 1) % 4);
                SoundDefOf.Click.PlayOneShotOnCamera();
                LoadedModManager.GetMod<PerspectiveShiftMod>()?.WriteSettings();
            }
            else if (Mouse.IsOver(rotateButtonRect))
            {
                TooltipHandler.TipRegion(rotateButtonRect, "PS_RotateCornerTip".Translate());
            }

            GenUI.AbsorbClicksInRect(rotateButtonRect);
        }

        private void DrawScaleGrip()
        {
            if (Event.current.type == EventType.Layout) return;

            scaleGripRect = Rect.zero;
            if (needsBounds == Rect.zero) return;

            var settings = PerspectiveShiftMod.settings;
            var corner = settings.gizmoCorner;
            bool gripRight = corner == GizmoCorner.TopLeft || corner == GizmoCorner.BottomLeft;

            float gripSize = 18f;
            scaleGripRect = new Rect(gripRight ? needsBounds.xMax - gripSize : needsBounds.xMin, needsBounds.yMax - gripSize, gripSize, gripSize);

            bool hovered = Mouse.IsOver(scaleGripRect);
            if (Event.current.type == EventType.Repaint)
            {
                DrawGripLines(scaleGripRect, gripRight, hovered || resizingUI ? Color.white : GripIdleColor);
            }
            var ev = Event.current;

            if (ev.type == EventType.MouseDown && hovered)
            {
                if (ev.button == 0)
                {
                    resizingUI = true;
                    resizeStartMouse = ev.mousePosition;
                    resizeStartScale = settings.avatarUIScale;
                    ev.Use();
                }
                else if (ev.button == 1)
                {
                    settings.avatarUIScale = 1f;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    LoadedModManager.GetMod<PerspectiveShiftMod>()?.WriteSettings();
                    ev.Use();
                }
            }

            if (!resizingUI)
            {
                if (hovered) TooltipHandler.TipRegion(scaleGripRect, "PS_ScaleInterfaceTip".Translate());
                return;
            }

            if (ev.rawType == EventType.MouseUp)
            {
                resizingUI = false;
                LoadedModManager.GetMod<PerspectiveShiftMod>()?.WriteSettings();
                ev.Use();
                return;
            }

            var delta = ev.mousePosition - resizeStartMouse;
            settings.avatarUIScale = Mathf.Clamp(resizeStartScale + (delta.x * (gripRight ? 1f : -1f) + delta.y) / 600f, MinAvatarUIScale, MaxAvatarUIScale);
            if (ev.type == EventType.MouseDrag) ev.Use();
        }

        private static void DrawGripLines(Rect rect, bool gripRight, Color color)
        {
            float cx = gripRight ? rect.xMax : rect.xMin;
            float dx = gripRight ? -1f : 1f;

            for (int i = 1; i <= 3; i++)
            {
                float d = i * 5f + 1f;
                Widgets.DrawLine(new Vector2(cx + dx * d, rect.yMax), new Vector2(cx, rect.yMax - d), color, 1f);
            }
        }
    }
}
