using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public partial class Avatar
    {
        public static bool DrawingAvatarNeeds = false;
        private Rect gizmoBounds;
        private List<object> prevSelected;

        public void OnGUI()
        {
            DebugLog();
            DrawCameraLockReturnButton();
            DrawGizmosAndNeeds();
            HandleTabKeyBindings();
            HandleEatFoodBinding();
            HandleRecreationBinding();
            bool mouseOverGizmo = MapGizmoUtility.LastMouseOverGizmo != null || gizmoBounds.Contains(Event.current.mousePosition);
            bool mouseOverUI = IsMouseOverUI() || IsMouseOverColonistBar();
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

            if (!PerspectiveShiftMod.settings.disableCustomGizmos) DrawPlayerGizmos();
            DrawNeeds();
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
                var tab = inspectPane.CurTabs.FirstOrDefault(t => tabType.IsAssignableFrom(t.GetType()));
                if (tab != null)
                {
                    var actualType = tab.GetType();
                    if (inspectPane.OpenTabType == actualType)
                    {
                        inspectPane.CloseOpenTab();
                        Find.Selector.Deselect(pawn);
                    }
                    else
                        inspectPane.OpenTabType = actualType;
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

            float mainButtonHeight = 45f;

            switch (PerspectiveShiftMod.settings.gizmoCorner)
            {
                case GizmoCorner.TopRight:
                    startX = (UI.screenWidth - 10f) / 0.85f - actualSize;
                    startY = 10f / 0.85f;
                    break;
                case GizmoCorner.BottomRight:
                    startX = (UI.screenWidth - 10f) / 0.85f - actualSize;
                    startY = (UI.screenHeight - 10f - mainButtonHeight) / 0.85f - actualSize;
                    break;
                case GizmoCorner.BottomLeft:
                    startX = 10f / 0.85f;
                    startY = (UI.screenHeight - 10f - mainButtonHeight) / 0.85f - actualSize;
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
    }
}
