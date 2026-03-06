using HarmonyLib;
using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;

namespace PerspectiveShift
{
    public class PerspectiveShiftMod : Mod
    {
        public static PerspectiveShiftSettings settings;

        public PerspectiveShiftMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<PerspectiveShiftSettings>();
            new Harmony("PerspectiveShiftMod").PatchAll();
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("PS_ZoomSpeed".Translate(settings.zoomSpeed.ToString("F2")));
            settings.zoomSpeed = listing.Slider(settings.zoomSpeed, 0.1f, 1.0f);

            listing.Label("PS_MinZoom".Translate(settings.minZoom.ToString("F1")));
            settings.minZoom = listing.Slider(settings.minZoom, 0.1f, 20f);

            listing.Label("PS_MaxZoom".Translate(settings.maxZoom.ToString("F1")));
            settings.maxZoom = listing.Slider(settings.maxZoom, 40f, 100f);

            listing.Label("PS_GrabRange".Translate(settings.grabRange.ToString("F1")));
            settings.grabRange = listing.Slider(settings.grabRange, 0.5f, 3f);

            listing.CheckboxLabeled("PS_DisableAimingDelay".Translate(), ref settings.disableAimingDelay, "PS_DisableAimingDelayDesc".Translate());

            listing.Label("PS_MoveSpeedMultiplier".Translate(settings.moveSpeedMultiplier.ToString("P0")));
            settings.moveSpeedMultiplier = listing.Slider(settings.moveSpeedMultiplier, 0.1f, 5f);

            listing.Label("PS_ShootAccuracyMultiplier".Translate(settings.shootAccuracyMultiplier.ToString("P0")));
            settings.shootAccuracyMultiplier = listing.Slider(settings.shootAccuracyMultiplier, 0.1f, 5f);

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

            listing.End();
            base.DoSettingsWindowContents(rect);
        }

        public override string SettingsCategory()
        {
            return Content.Name;
        }
    }
}
