using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Dialog_Options), "DoGameplayOptions")]
    public static class Dialog_Options_DoGameplayOptions_Patch
    {
        public static void Postfix(Listing_Standard listing)
        {
            if (!PerspectiveShiftMod.settings.permadeath)
            {
                if (listing.ButtonTextLabeledPct("PS_PerspectiveSettings".Translate(), "PS_Modify".Translate(), 0.6f, TextAnchor.UpperLeft))
                {
                    Find.WindowStack.Add(new Page_ChoosePerspective());
                }
            }
        }
    }
}
