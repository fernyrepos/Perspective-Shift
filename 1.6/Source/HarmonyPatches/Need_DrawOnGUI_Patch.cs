using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HotSwappable]
    [HarmonyPatch(typeof(Need), nameof(Need.DrawOnGUI))]
    public static class Need_DrawOnGUI_Patch
    {
        private static Texture2D _pinTex;
        private static Texture2D _pinOutlineTex;
        private static Texture2D PinTex => _pinTex ??= ContentFinder<Texture2D>.Get("UI/Icons/Pin");
        private static Texture2D PinOutlineTex => _pinOutlineTex ??= ContentFinder<Texture2D>.Get("UI/Icons/Pin-Outline");

        public static void Postfix(Need __instance, Rect rect)
        {
            if (Avatar.DrawingAvatarNeeds || __instance.pawn != State.Avatar.pawn) return;

            float pinScale = Mathf.InverseLerp(0f, 70f, rect.height);
            if (rect.height >= 70f) pinScale = 0.9f;
            float pinSize = 16f * pinScale;
            float pinOffset = 12f * pinScale;
            if (rect.height > 50f) pinOffset += 4f;
            float pinXOffset = 50f - (50f - 50f * pinScale) * 0.5f;
            Rect pinRect = new Rect(rect.xMax - pinXOffset, rect.y + pinOffset, pinSize, pinSize);
            bool isPinned = PerspectiveShiftMod.settings.pinnedNeeds.Contains(__instance.def.defName);            
            Texture2D tex = isPinned ? PinTex : PinOutlineTex;
            Color prevColor = GUI.color;
            if (!isPinned)
                GUI.color = Color.grey;
            GUI.DrawTexture(pinRect, tex, ScaleMode.ScaleToFit);
            GUI.color = prevColor;

            if (Widgets.ButtonInvisible(pinRect))
            {
                isPinned = !isPinned;
                if (isPinned)
                    PerspectiveShiftMod.settings.pinnedNeeds.Add(__instance.def.defName);
                else
                    PerspectiveShiftMod.settings.pinnedNeeds.Remove(__instance.def.defName);

                PerspectiveShiftMod.settings.Write();
            }
        }
    }
}
