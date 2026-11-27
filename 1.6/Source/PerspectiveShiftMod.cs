using HarmonyLib;
using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;

namespace PerspectiveShift
{
    [HotSwappable]
    public class PerspectiveShiftMod : Mod
    {
        public static PerspectiveShiftSettings settings;
        private Vector2 scrollPosition;
        private float scrollHeight = 9999999f;

        private enum SettingsTab
        {
            Camera,
            Interaction,
            Cursors,
            Movement,
            Interface,
            Gameplay,
        }

        private SettingsTab activeTab = SettingsTab.Camera;
        private readonly List<TabRecord> tabRecords = new List<TabRecord>();
        public PerspectiveShiftMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<PerspectiveShiftSettings>();
            new Harmony("PerspectiveShiftMod").PatchAll(typeof(PerspectiveShiftMod).Assembly);
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            var footerRect = new Rect(rect.x, rect.yMax - 34f, rect.width, 30f);
            var tabRect = rect;
            tabRect.yMin += 32f;
            tabRect.yMax -= 40f;

            tabRecords.Clear();
            AddTab(SettingsTab.Camera, "PS_TabCamera");
            AddTab(SettingsTab.Interaction, "PS_TabInteraction");
            AddTab(SettingsTab.Cursors, "PS_TabCursors");
            AddTab(SettingsTab.Movement, "PS_TabMovement");
            AddTab(SettingsTab.Interface, "PS_TabInterface");
            AddTab(SettingsTab.Gameplay, "PS_TabGameplay");

            Widgets.DrawMenuSection(tabRect);
            TabDrawer.DrawTabs(tabRect, tabRecords);

            var inner = tabRect.ContractedBy(12f);
            var viewRect = new Rect(0f, 0f, inner.width - 30f, scrollHeight);
            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);
            var listing = new Listing_Standard();
            listing.maxOneColumn = true;
            listing.Begin(viewRect);
            var startY = listing.curY;

            switch (activeTab)
            {
                case SettingsTab.Camera: DoCameraTab(listing); break;
                case SettingsTab.Interaction: DoInteractionTab(listing); break;
                case SettingsTab.Cursors: DoCursorsTab(listing); break;
                case SettingsTab.Movement: DoMovementTab(listing); break;
                case SettingsTab.Interface: DoInterfaceTab(listing); break;
                default: DoGameplayTab(listing); break;
            }

            scrollHeight = listing.curY - startY;
            listing.End();
            Widgets.EndScrollView();

            DoResetButton(footerRect);
        }

        private void DoResetButton(Rect rect)
        {
            var buttonRect = new Rect(rect.xMax - 220f, rect.y, 220f, rect.height);
            if (!Widgets.ButtonText(buttonRect, "PS_ResetToDefault".Translate())) return;

            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
            {
                new FloatMenuOption("PS_ResetThisTab".Translate(TabLabel(activeTab)), delegate
                {
                    ResetTab(activeTab);
                    settings.Write();
                }),
                new FloatMenuOption("PS_ResetAll".Translate(), ConfirmResetAll),
            }));
        }

        private static void ConfirmResetAll()
        {
            var dialog = Dialog_MessageBox.CreateConfirmation("PS_ResetAllConfirm".Translate(), ResetAll, true);
            dialog.layer = WindowLayer.Super;
            Find.WindowStack.Add(dialog);
        }

        private static string TabLabel(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.Camera: return "PS_TabCamera".Translate();
                case SettingsTab.Interaction: return "PS_TabInteraction".Translate();
                case SettingsTab.Cursors: return "PS_TabCursors".Translate();
                case SettingsTab.Movement: return "PS_TabMovement".Translate();
                case SettingsTab.Interface: return "PS_TabInterface".Translate();
                default: return "PS_TabGameplay".Translate();
            }
        }

        private static void ResetAll()
        {
            foreach (SettingsTab tab in Enum.GetValues(typeof(SettingsTab)))
            {
                ResetTab(tab);
            }

            var d = new PerspectiveShiftSettings();
            settings.avatarUIScale = d.avatarUIScale;
            settings.pinnedNeeds = new List<string>(d.pinnedNeeds);
            settings.Write();
        }

        private static void ResetTab(SettingsTab tab)
        {
            var d = new PerspectiveShiftSettings();
            switch (tab)
            {
                case SettingsTab.Camera:
                    settings.zoomSpeed = d.zoomSpeed;
                    settings.minZoom = d.minZoom;
                    settings.maxZoom = d.maxZoom;
                    settings.cameraEasing = d.cameraEasing;
                    break;

                case SettingsTab.Interaction:
                    settings.grabRange = d.grabRange;
                    settings.holdToFire = d.holdToFire;
                    settings.disableAimingDelay = d.disableAimingDelay;
                    settings.requireHeldClickForJobs = d.requireHeldClickForJobs;
                    settings.disableDoubleClickEquip = d.disableDoubleClickEquip;
                    settings.disableDoubleClickEat = d.disableDoubleClickEat;
                    settings.disableDoubleClickDrug = d.disableDoubleClickDrug;
                    settings.disableDoubleClickRead = d.disableDoubleClickRead;
                    settings.weaponTooltips = d.weaponTooltips;
                    settings.apparelTooltips = d.apparelTooltips;
                    settings.eatTooltips = d.eatTooltips;
                    settings.drugTooltips = d.drugTooltips;
                    settings.bookTooltips = d.bookTooltips;
                    settings.harvestTooltips = d.harvestTooltips;
                    settings.billTooltips = d.billTooltips;
                    break;

                case SettingsTab.Cursors:
                    settings.customCursors = d.customCursors;
                    settings.haulingCursor = d.haulingCursor;
                    settings.mineCursor = d.mineCursor;
                    settings.smoothCursor = d.smoothCursor;
                    settings.buildCursor = d.buildCursor;
                    settings.roofCursor = d.roofCursor;
                    settings.chopCursor = d.chopCursor;
                    settings.harvestCursor = d.harvestCursor;
                    settings.cutCursor = d.cutCursor;
                    settings.plantCursor = d.plantCursor;
                    settings.plowCursor = d.plowCursor;
                    settings.traverseCursor = d.traverseCursor;
                    settings.reloadCursors = d.reloadCursors;
                    settings.openCursor = d.openCursor;
                    settings.launchCursor = d.launchCursor;
                    settings.sleepCursor = d.sleepCursor;
                    settings.recreationCursor = d.recreationCursor;
                    settings.researchCursor = d.researchCursor;
                    settings.cookCursor = d.cookCursor;
                    settings.butcherCursor = d.butcherCursor;
                    settings.stonecuttingCursor = d.stonecuttingCursor;
                    settings.brewCursor = d.brewCursor;
                    settings.smeltCursor = d.smeltCursor;
                    settings.tailorCursor = d.tailorCursor;
                    settings.tameCursor = d.tameCursor;
                    settings.slaughterCursor = d.slaughterCursor;
                    settings.releaseToWildCursor = d.releaseToWildCursor;
                    break;

                case SettingsTab.Movement:
                    settings.enableSprinting = d.enableSprinting;
                    settings.sprintMode = d.sprintMode;
                    settings.sprintSpeedMultiplier = d.sprintSpeedMultiplier;
                    settings.sprintFoodDrain = d.sprintFoodDrain;
                    settings.sprintSleepDrain = d.sprintSleepDrain;
                    settings.enableSneaking = d.enableSneaking;
                    settings.sneakMode = d.sneakMode;
                    settings.sneakSpeedMultiplier = d.sneakSpeedMultiplier;
                    settings.moveSpeedMultiplier = d.moveSpeedMultiplier;
                    settings.playerMoveSpeedCap = d.playerMoveSpeedCap;
                    break;

                case SettingsTab.Interface:
                    settings.gizmoCorner = d.gizmoCorner;
                    settings.disableCustomGizmos = d.disableCustomGizmos;
                    settings.hideGizmosOnWorldMap = d.hideGizmosOnWorldMap;
                    settings.enableFulfillNeedsGizmo = d.enableFulfillNeedsGizmo;
                    settings.showControlsOnFirstInhabit = d.showControlsOnFirstInhabit;
                    settings.enableDamageScreenEffect = d.enableDamageScreenEffect;
                    settings.damageEffectStyle = d.damageEffectStyle;
                    break;

                default:
                    settings.workSpeedMultiplier = d.workSpeedMultiplier;
                    settings.shootAccuracyMultiplier = d.shootAccuracyMultiplier;
                    settings.fishingMinigame = d.fishingMinigame;
                    settings.instantEquip = d.instantEquip;
                    settings.sleepingPreventsVision = d.sleepingPreventsVision;
                    settings.totalFreedom = d.totalFreedom;
                    settings.allowNonHuman = d.allowNonHuman;
                    settings.requirePawnInFaction = d.requirePawnInFaction;
                    settings.noPlayerRestNeed = d.noPlayerRestNeed;
                    settings.noPlayerFoodNeed = d.noPlayerFoodNeed;
                    settings.noPlayerHygieneNeed = d.noPlayerHygieneNeed;
                    settings.noPlayerBladderNeed = d.noPlayerBladderNeed;
                    settings.noPlayerRecreationNeed = d.noPlayerRecreationNeed;
                    settings.noPlayerMentalBreaks = d.noPlayerMentalBreaks;
                    break;
            }

        }

        private void AddTab(SettingsTab tab, string labelKey)
        {
            tabRecords.Add(new TabRecord(labelKey.Translate(), delegate
            {
                if (activeTab == tab) return;
                activeTab = tab;
                scrollPosition = Vector2.zero;
                scrollHeight = 9999999f;
            }, activeTab == tab));
        }

        private void DoCameraTab(Listing_Standard listing)
        {
            listing.Label("PS_ZoomSpeed".Translate(settings.zoomSpeed.ToString("F2")));
            settings.zoomSpeed = listing.Slider(settings.zoomSpeed, 0.1f, 1.0f);

            listing.Label("PS_MinZoom".Translate(settings.minZoom.ToString("F1")));
            settings.minZoom = listing.Slider(settings.minZoom, 0.1f, 20f);

            listing.Label("PS_MaxZoom".Translate(settings.maxZoom.ToString("F1")));
            settings.maxZoom = listing.Slider(settings.maxZoom, 40f, 100f);

            listing.CheckboxLabeled("PS_CameraEasing".Translate(), ref settings.cameraEasing, "PS_CameraEasingDesc".Translate());
        }

        private void DoInteractionTab(Listing_Standard listing)
        {
            listing.Label("PS_GrabRange".Translate(settings.grabRange.ToString("F1")));
            settings.grabRange = listing.Slider(settings.grabRange, 0.5f, 3f);

            listing.CheckboxLabeled("PS_HoldToFire".Translate(), ref settings.holdToFire, "PS_HoldToFireDesc".Translate());
            listing.CheckboxLabeled("PS_DisableAimingDelay".Translate(), ref settings.disableAimingDelay, "PS_DisableAimingDelayDesc".Translate());
            listing.CheckboxLabeled("PS_RequireHeldClickForJobs".Translate(), ref settings.requireHeldClickForJobs, "PS_RequireHeldClickForJobsDesc".Translate());

            listing.GapLine();

            listing.CheckboxLabeled("PS_DisableDoubleClickEquip".Translate(), ref settings.disableDoubleClickEquip, "PS_DisableDoubleClickEquipDesc".Translate());
            listing.CheckboxLabeled("PS_DisableDoubleClickEat".Translate(), ref settings.disableDoubleClickEat, "PS_DisableDoubleClickEatDesc".Translate());
            listing.CheckboxLabeled("PS_DisableDoubleClickDrug".Translate(), ref settings.disableDoubleClickDrug, "PS_DisableDoubleClickDrugDesc".Translate());
            listing.CheckboxLabeled("PS_DisableDoubleClickRead".Translate(), ref settings.disableDoubleClickRead, "PS_DisableDoubleClickReadDesc".Translate());
            if (!settings.disableDoubleClickEquip)
            {
                listing.CheckboxLabeled("PS_WeaponTooltips".Translate(), ref settings.weaponTooltips, "PS_WeaponTooltipsDesc".Translate());
                listing.CheckboxLabeled("PS_ApparelTooltips".Translate(), ref settings.apparelTooltips, "PS_ApparelTooltipsDesc".Translate());
            }
            if (!settings.disableDoubleClickEat)
            {
                listing.CheckboxLabeled("PS_EatTooltips".Translate(), ref settings.eatTooltips, "PS_EatTooltipsDesc".Translate());
            }
            if (!settings.disableDoubleClickDrug)
            {
                listing.CheckboxLabeled("PS_DrugTooltips".Translate(), ref settings.drugTooltips, "PS_DrugTooltipsDesc".Translate());
            }
            if (!settings.disableDoubleClickRead)
            {
                listing.CheckboxLabeled("PS_BookTooltips".Translate(), ref settings.bookTooltips, "PS_BookTooltipsDesc".Translate());
            }
            listing.CheckboxLabeled("PS_HarvestTooltips".Translate(), ref settings.harvestTooltips, "PS_HarvestTooltipsDesc".Translate());
            listing.CheckboxLabeled("PS_BillTooltips".Translate(), ref settings.billTooltips, "PS_BillTooltipsDesc".Translate());
        }

        private void DoCursorsTab(Listing_Standard listing)
        {
            listing.CheckboxLabeled("PS_CustomCursors".Translate(), ref settings.customCursors, "PS_CustomCursorsDesc".Translate());
            if (!settings.customCursors) return;

            listing.GapLine();
            listing.CheckboxLabeled("PS_HaulingCursor".Translate(), ref settings.haulingCursor, "PS_HaulingCursorDesc".Translate());
            listing.CheckboxLabeled("PS_MineCursor".Translate(), ref settings.mineCursor, "PS_MineCursorDesc".Translate());
            listing.CheckboxLabeled("PS_SmoothCursor".Translate(), ref settings.smoothCursor, "PS_SmoothCursorDesc".Translate());
            listing.CheckboxLabeled("PS_BuildCursor".Translate(), ref settings.buildCursor, "PS_BuildCursorDesc".Translate());
            listing.CheckboxLabeled("PS_RoofCursor".Translate(), ref settings.roofCursor, "PS_RoofCursorDesc".Translate());
            listing.CheckboxLabeled("PS_ChopCursor".Translate(), ref settings.chopCursor, "PS_ChopCursorDesc".Translate());
            listing.CheckboxLabeled("PS_HarvestCursor".Translate(), ref settings.harvestCursor, "PS_HarvestCursorDesc".Translate());
            listing.CheckboxLabeled("PS_CutCursor".Translate(), ref settings.cutCursor, "PS_CutCursorDesc".Translate());
            listing.CheckboxLabeled("PS_PlantCursor".Translate(), ref settings.plantCursor, "PS_PlantCursorDesc".Translate());
            listing.CheckboxLabeled("PS_PlowCursor".Translate(), ref settings.plowCursor, "PS_PlowCursorDesc".Translate());
            listing.CheckboxLabeled("PS_TraverseCursor".Translate(), ref settings.traverseCursor, "PS_TraverseCursorDesc".Translate());
            listing.CheckboxLabeled("PS_ReloadCursors".Translate(), ref settings.reloadCursors, "PS_ReloadCursorsDesc".Translate());
            listing.CheckboxLabeled("PS_OpenCursor".Translate(), ref settings.openCursor, "PS_OpenCursorDesc".Translate());
            listing.CheckboxLabeled("PS_LaunchCursor".Translate(), ref settings.launchCursor, "PS_LaunchCursorDesc".Translate());
            listing.CheckboxLabeled("PS_SleepCursor".Translate(), ref settings.sleepCursor, "PS_SleepCursorDesc".Translate());
            listing.CheckboxLabeled("PS_RecreationCursor".Translate(), ref settings.recreationCursor, "PS_RecreationCursorDesc".Translate());
            listing.CheckboxLabeled("PS_ResearchCursor".Translate(), ref settings.researchCursor, "PS_ResearchCursorDesc".Translate());
            listing.CheckboxLabeled("PS_CookCursor".Translate(), ref settings.cookCursor, "PS_CookCursorDesc".Translate());
            listing.CheckboxLabeled("PS_ButcherCursor".Translate(), ref settings.butcherCursor, "PS_ButcherCursorDesc".Translate());
            listing.CheckboxLabeled("PS_StonecuttingCursor".Translate(), ref settings.stonecuttingCursor, "PS_StonecuttingCursorDesc".Translate());
            listing.CheckboxLabeled("PS_BrewCursor".Translate(), ref settings.brewCursor, "PS_BrewCursorDesc".Translate());
            listing.CheckboxLabeled("PS_SmeltCursor".Translate(), ref settings.smeltCursor, "PS_SmeltCursorDesc".Translate());
            listing.CheckboxLabeled("PS_TailorCursor".Translate(), ref settings.tailorCursor, "PS_TailorCursorDesc".Translate());
            listing.CheckboxLabeled("PS_TameCursor".Translate(), ref settings.tameCursor, "PS_TameCursorDesc".Translate());
            listing.CheckboxLabeled("PS_SlaughterCursor".Translate(), ref settings.slaughterCursor, "PS_SlaughterCursorDesc".Translate());
            listing.CheckboxLabeled("PS_ReleaseToWildCursor".Translate(), ref settings.releaseToWildCursor, "PS_ReleaseToWildCursorDesc".Translate());
        }

        private static string DamageEffectStyleLabel(DamageEffectStyle style) => ("PS_DamageEffectStyle_" + style).Translate();

        public static string KeyHoldModeLabel(KeyHoldMode mode) => ("PS_KeyHoldMode_" + mode).Translate();

        private static void DoKeyHoldModeRow(Listing_Standard listing, string labelKey, KeyHoldMode current, Action<KeyHoldMode> setter)
        {
            if (!listing.ButtonTextLabeled(labelKey.Translate(), KeyHoldModeLabel(current))) return;

            var list = new List<FloatMenuOption>();
            foreach (KeyHoldMode mode in Enum.GetValues(typeof(KeyHoldMode)))
            {
                var picked = mode;
                list.Add(new FloatMenuOption(KeyHoldModeLabel(picked), () => setter(picked)));
            }
            Find.WindowStack.Add(new FloatMenu(list));
        }

        private void DoMovementTab(Listing_Standard listing)
        {
            listing.CheckboxLabeled("PS_EnableSprinting".Translate(), ref settings.enableSprinting);
            if (settings.enableSprinting)
            {
                DoKeyHoldModeRow(listing, "PS_SprintBehavior", settings.sprintMode, m => settings.sprintMode = m);

                listing.Label("PS_SprintSpeedMultiplier".Translate(settings.sprintSpeedMultiplier.ToString("F1")));
                settings.sprintSpeedMultiplier = listing.Slider(settings.sprintSpeedMultiplier, 1.1f, 3f);

                listing.Label("PS_SprintFoodDrainMultiplier".Translate(settings.sprintFoodDrain.ToString("F1")));
                settings.sprintFoodDrain = listing.Slider(settings.sprintFoodDrain, 1f, 10f);

                listing.Label("PS_SprintSleepDrainMultiplier".Translate(settings.sprintSleepDrain.ToString("F1")));
                settings.sprintSleepDrain = listing.Slider(settings.sprintSleepDrain, 1f, 10f);
            }

            listing.CheckboxLabeled("PS_EnableSneaking".Translate(), ref settings.enableSneaking);
            if (settings.enableSneaking)
            {
                DoKeyHoldModeRow(listing, "PS_SneakBehavior", settings.sneakMode, m => settings.sneakMode = m);

                listing.Label("PS_SneakSpeedMultiplier".Translate(settings.sneakSpeedMultiplier.ToString("F1")));
                settings.sneakSpeedMultiplier = listing.Slider(settings.sneakSpeedMultiplier, 0.1f, 0.9f);
            }

            listing.GapLine();

            listing.Label("PS_MoveSpeedMultiplier".Translate(settings.moveSpeedMultiplier.ToString("P0")));
            settings.moveSpeedMultiplier = listing.Slider(settings.moveSpeedMultiplier, 0.1f, 5f);

            listing.Label("PS_PlayerMoveSpeedCap".Translate(settings.playerMoveSpeedCap.ToString("F2")));
            settings.playerMoveSpeedCap = listing.Slider(settings.playerMoveSpeedCap, 0.1f, 5.0f);
        }

        private void DoInterfaceTab(Listing_Standard listing)
        {
            if (listing.ButtonTextLabeled("PS_GizmoCorner".Translate(), settings.gizmoCorner.ToString()))
            {
                var list = new List<FloatMenuOption>();
                foreach (GizmoCorner corner in Enum.GetValues(typeof(GizmoCorner)))
                {
                    list.Add(new FloatMenuOption(corner.ToString(), () => settings.gizmoCorner = corner));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            }

            listing.CheckboxLabeled("PS_DisableCustomGizmos".Translate(), ref settings.disableCustomGizmos, "PS_DisableCustomGizmosDesc".Translate());
            listing.CheckboxLabeled("PS_HideGizmosOnWorldMap".Translate(), ref settings.hideGizmosOnWorldMap, "PS_HideGizmosOnWorldMapDesc".Translate());
            listing.CheckboxLabeled("PS_EnableFulfillNeedsGizmo".Translate(), ref settings.enableFulfillNeedsGizmo, "PS_EnableFulfillNeedsGizmoDesc".Translate());
            listing.CheckboxLabeled("PS_ShowControlsOnFirstInhabit".Translate(), ref settings.showControlsOnFirstInhabit, "PS_ShowControlsOnFirstInhabitDesc".Translate());
            listing.CheckboxLabeled("PS_EnableDamageScreenEffect".Translate(), ref settings.enableDamageScreenEffect, "PS_EnableDamageScreenEffectDesc".Translate());
            if (settings.enableDamageScreenEffect && listing.ButtonTextLabeled("PS_DamageEffectStyle".Translate(), DamageEffectStyleLabel(settings.damageEffectStyle)))
            {
                var styles = new List<FloatMenuOption>();
                foreach (DamageEffectStyle style in Enum.GetValues(typeof(DamageEffectStyle)))
                {
                    var picked = style;
                    styles.Add(new FloatMenuOption(DamageEffectStyleLabel(picked), () => settings.damageEffectStyle = picked));
                }
                Find.WindowStack.Add(new FloatMenu(styles));
            }
        }

        private void DoGameplayTab(Listing_Standard listing)
        {
            listing.Label("PS_WorkSpeedMultiplier".Translate(settings.workSpeedMultiplier.ToString("P0")));
            settings.workSpeedMultiplier = listing.Slider(settings.workSpeedMultiplier, 0.1f, 5f);

            listing.Label("PS_ShootAccuracyMultiplier".Translate(settings.shootAccuracyMultiplier.ToString("P0")));
            settings.shootAccuracyMultiplier = listing.Slider(settings.shootAccuracyMultiplier, 0.1f, 100f);

            listing.GapLine();

            if (ModsConfig.OdysseyActive)
            {
                listing.CheckboxLabeled("PS_FishingMinigame".Translate(), ref settings.fishingMinigame, "PS_FishingMinigameDesc".Translate());
            }
            listing.CheckboxLabeled("PS_InstantEquip".Translate(), ref settings.instantEquip, "PS_InstantEquipDesc".Translate());
            listing.CheckboxLabeled("PS_SleepingPreventsVision".Translate(), ref settings.sleepingPreventsVision, "PS_SleepingPreventsVisionDesc".Translate());
            listing.CheckboxLabeled("PS_TotalFreedom".Translate(), ref settings.totalFreedom, "PS_TotalFreedomDesc".Translate());
            listing.CheckboxLabeled("PS_AllowNonHuman".Translate(), ref settings.allowNonHuman, "PS_AllowNonHumanDesc".Translate());
            listing.CheckboxLabeled("PS_RequirePawnInFaction".Translate(), ref settings.requirePawnInFaction, "PS_RequirePawnInFactionDesc".Translate());

            listing.GapLine();

            listing.CheckboxLabeled("PS_NoPlayerRestNeed".Translate(), ref settings.noPlayerRestNeed, "PS_NoPlayerRestNeedDesc".Translate());
            listing.CheckboxLabeled("PS_NoPlayerFoodNeed".Translate(), ref settings.noPlayerFoodNeed, "PS_NoPlayerFoodNeedDesc".Translate());
            listing.CheckboxLabeled("PS_NoPlayerRecreationNeed".Translate(), ref settings.noPlayerRecreationNeed, "PS_NoPlayerRecreationNeedDesc".Translate());
            if (ModCompatibility.IsDBHHygieneEnabled())
            {
                listing.CheckboxLabeled("PS_NoPlayerHygieneNeed".Translate(), ref settings.noPlayerHygieneNeed, "PS_NoPlayerHygieneNeedDesc".Translate());
            }
            if (ModCompatibility.IsDBHBladderEnabled())
            {
                listing.CheckboxLabeled("PS_NoPlayerBladderNeed".Translate(), ref settings.noPlayerBladderNeed, "PS_NoPlayerBladderNeedDesc".Translate());
            }
            listing.CheckboxLabeled("PS_NoPlayerMentalBreaks".Translate(), ref settings.noPlayerMentalBreaks, "PS_NoPlayerMentalBreaksDesc".Translate());
        }

        public override string SettingsCategory()
        {
            return Content.Name;
        }
    }
}
