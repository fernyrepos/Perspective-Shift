using HarmonyLib;
using UnityEngine;
using Verse;

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
            settings.minZoom = listing.Slider(settings.minZoom, 4f, 20f);

            listing.Label("PS_MaxZoom".Translate(settings.maxZoom.ToString("F1")));
            settings.maxZoom = listing.Slider(settings.maxZoom, 40f, 100f);

            listing.Label("PS_GrabRange".Translate(settings.grabRange.ToString("F1")));
            settings.grabRange = listing.Slider(settings.grabRange, 0.5f, 3f);

            listing.End();
            base.DoSettingsWindowContents(rect);
        }

        public override string SettingsCategory()
        {
            return Content.Name;
        }
    }
}
