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
        public PerspectiveShiftMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<PerspectiveShiftSettings>();
            new Harmony("PerspectiveShiftMod").PatchAll();
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            var listing = new Listing_Standard();
            var outRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            var viewRect = new Rect(0f, 0f, rect.width - 30f, CalculateHeight());
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            listing.Begin(viewRect);

            listing.Label("PS_ZoomSpeed".Translate(settings.zoomSpeed.ToString("F2")));
            settings.zoomSpeed = listing.Slider(settings.zoomSpeed, 0.1f, 1.0f);

            listing.Label("PS_MinZoom".Translate(settings.minZoom.ToString("F1")));
            settings.minZoom = listing.Slider(settings.minZoom, 0.1f, 20f);

            listing.Label("PS_MaxZoom".Translate(settings.maxZoom.ToString("F1")));
            settings.maxZoom = listing.Slider(settings.maxZoom, 40f, 100f);

            listing.Label("PS_GrabRange".Translate(settings.grabRange.ToString("F1")));
            settings.grabRange = listing.Slider(settings.grabRange, 0.5f, 3f);

            listing.CheckboxLabeled("PS_DisableAimingDelay".Translate(), ref settings.disableAimingDelay, "PS_DisableAimingDelayDesc".Translate());

            listing.CheckboxLabeled("PS_EnableSprinting".Translate(), ref settings.enableSprinting);
            if (settings.enableSprinting)
            {
                listing.Label("PS_SprintSpeedMultiplier".Translate(settings.sprintSpeedMultiplier.ToString("F1")));
                settings.sprintSpeedMultiplier = listing.Slider(settings.sprintSpeedMultiplier, 1.1f, 3f);

                listing.Label("PS_SprintFoodDrainMultiplier".Translate(settings.sprintFoodDrain.ToString("F1")));
                settings.sprintFoodDrain = listing.Slider(settings.sprintFoodDrain, 1f, 5f);

                listing.Label("PS_SprintSleepDrainMultiplier".Translate(settings.sprintSleepDrain.ToString("F1")));
                settings.sprintSleepDrain = listing.Slider(settings.sprintSleepDrain, 1f, 5f);
            }
            listing.CheckboxLabeled("PS_EnableSneaking".Translate(), ref settings.enableSneaking);
            if (settings.enableSneaking)
            {
                listing.Label("PS_SneakSpeedMultiplier".Translate(settings.sneakSpeedMultiplier.ToString("F1")));
                settings.sneakSpeedMultiplier = listing.Slider(settings.sneakSpeedMultiplier, 0.1f, 0.9f);
            }

            listing.Label("PS_MoveSpeedMultiplier".Translate(settings.moveSpeedMultiplier.ToString("P0")));
            settings.moveSpeedMultiplier = listing.Slider(settings.moveSpeedMultiplier, 0.1f, 5f);

            listing.Label("PS_ShootAccuracyMultiplier".Translate(settings.shootAccuracyMultiplier.ToString("P0")));
            settings.shootAccuracyMultiplier = listing.Slider(settings.shootAccuracyMultiplier, 0.1f, 100f);

            if (listing.ButtonTextLabeled("PS_GizmoCorner".Translate(), settings.gizmoCorner.ToString()))
            {
                var list = new List<FloatMenuOption>();
                foreach (GizmoCorner corner in Enum.GetValues(typeof(GizmoCorner)))
                {
                    list.Add(new FloatMenuOption(corner.ToString(), () => settings.gizmoCorner = corner));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            }

            listing.CheckboxLabeled("PS_TotalFreedom".Translate(), ref settings.totalFreedom, "PS_TotalFreedomDesc".Translate());
            listing.CheckboxLabeled("PS_AllowNonHuman".Translate(), ref settings.allowNonHuman, "PS_AllowNonHumanDesc".Translate());
            listing.CheckboxLabeled("PS_RequirePawnInFaction".Translate(), ref settings.requirePawnInFaction, "PS_RequirePawnInFactionDesc".Translate());

            listing.End();
            Widgets.EndScrollView();
            base.DoSettingsWindowContents(rect);
        }

        private float CalculateHeight()
        {
            var labels = 6;
            var sliders = 6;
            var checkboxes = 6;
            var buttons = 1;
            if (settings.enableSprinting)
            {
                labels += 3;
                sliders += 3;
            }
            if (settings.enableSneaking)
            {
                labels += 1;
                sliders += 1;
            }
            return (labels * 24f) + (sliders * 24f) + (checkboxes * 24f) + (buttons * 32f);
        }

        public override string SettingsCategory()
        {
            return Content.Name;
        }
    }
}
