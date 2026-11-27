using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HotSwappable]
    public class Dialog_FulfillNeeds : Window
    {
        private const float RowHeight = 56f;
        private const float SliderHeight = 24f;
        private const float HeaderBlock = 60f;
        private const float FooterBlock = 98f;
        private const float WindowWidth = 420f;

        private static readonly AutoNeed[] Order =
        {
            AutoNeed.Food,
            AutoNeed.Sleep,
            AutoNeed.Bladder,
            AutoNeed.Hygiene,
            AutoNeed.Recreation
        };

        private readonly List<AutoNeed> rows = new List<AutoNeed>();

        public Dialog_FulfillNeeds()
        {
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            draggable = true;
            preventCameraMotion = false;

            foreach (var need in Order)
            {
                if (need == AutoNeed.Bladder && !ModCompatibility.IsDBHBladderEnabled()) continue;
                if (need == AutoNeed.Hygiene && !ModCompatibility.IsDBHHygieneEnabled()) continue;
                rows.Add(need);
            }
        }

        public override Vector2 InitialSize => new Vector2(WindowWidth, HeaderBlock + rows.Count * RowHeight + FooterBlock + Margin * 2f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(inRect);

            Text.Font = GameFont.Small;
            listing.Label("PS_FulfillNeeds".Translate());
            listing.GapLine(6f);

            foreach (var need in rows)
            {
                bool enabled = State.IsAutoNeedEnabled(need);
                bool previous = enabled;
                listing.CheckboxLabeled(("PS_AutoNeed_" + need).Translate(), ref enabled);
                if (enabled != previous) State.SetAutoNeedEnabled(need, enabled);

                float threshold = State.GetAutoNeedThreshold(need);
                var sliderRect = listing.GetRect(SliderHeight);
                sliderRect.xMin += 24f;

                GUI.color = enabled ? Color.white : Color.gray;
                float updated = Widgets.HorizontalSlider(sliderRect, threshold, 0.05f, 1f, true,
                    "PS_AutoNeedThreshold".Translate(threshold.ToStringPercent()), null, null, 0.05f);
                GUI.color = Color.white;

                if (updated != threshold) State.SetAutoNeedThreshold(need, updated);

                listing.Gap(4f);
            }

            listing.GapLine(6f);
            listing.CheckboxLabeled("PS_ReturnAfterFulfillment".Translate(), ref State.returnAfterFulfillment, "PS_ReturnAfterFulfillmentDesc".Translate());
            listing.CheckboxLabeled("PS_ShowJobCallouts".Translate(), ref State.showJobCallouts, "PS_ShowJobCalloutsDesc".Translate());
            listing.CheckboxLabeled("PS_PlayDoneSound".Translate(), ref State.playDoneSound, "PS_PlayDoneSoundDesc".Translate());

            listing.End();
        }
    }
}
