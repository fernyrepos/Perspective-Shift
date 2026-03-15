using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HotSwappable]
    public class Dialog_YouDied : Window
    {
        private static readonly Color DeathCauseColor = new ColorInt(228, 125, 124).ToColor;
        private static readonly Color EndRunBgColor = new Color(0.50f, 0.13f, 0.13f);
        public override Vector2 InitialSize => new Vector2(800f, 550f);
        private string deathMessage;
        private Pawn pawn;
        public Dialog_YouDied(Pawn pawn, DamageInfo? dinfo, Hediff hediff)
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = false;

            this.pawn = pawn;
            deathMessage = HealthUtility.GetDiedLetterText(pawn, dinfo, hediff);
        }

        public override void DoWindowContents(Rect inRect)
        {
            const float btnW = 185f;
            const float rowH = 60f;
            const float descOffset = btnW + 14f;
            const float portraitSize = 72f;
            inRect.x += 50;
            inRect.width -= 100;

            var portraitRect = new Rect(inRect.x, inRect.y + 20, portraitSize, portraitSize);
            var portrait = PortraitsCache.Get(pawn, new Vector2(portraitSize, portraitSize), Rot4.South, cameraZoom: 1.5f, healthStateOverride: PawnHealthState.Mobile);
            GUI.DrawTexture(portraitRect, portrait);
            Text.Anchor = TextAnchor.MiddleCenter;      
            var titleStyle = new GUIStyle(Text.CurFontStyle);
            titleStyle.fontSize = 26;
            var titleRect = new Rect(inRect.x, inRect.y + 20, inRect.width, 60f);
            GUI.Label(titleRect, "PS_YouDied".Translate(), titleStyle);
            Text.Font = GameFont.Medium;
            GUI.color = DeathCauseColor;
            Widgets.Label(new Rect(inRect.x, inRect.y + 65f, inRect.width, 32f), deathMessage);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Text.Anchor = TextAnchor.UpperLeft;
            float y = inRect.y + 140f;

            if (!PerspectiveShiftMod.settings.permadeath)
            {
                DrawRow(inRect, ref y, btnW, rowH, descOffset,
                    "PS_InhabitNewCharacter".Translate(),
                    "PS_InhabitNewCharacter_Desc".Translate(),
                    () => { Find.WindowStack.Add(new Page_ChooseStartingCharacter()); Close(); });

                DrawRow(inRect, ref y, btnW, rowH, descOffset,
                    "PS_ContinueDynamicMode".Translate(),
                    "PS_ContinueDynamicMode_Desc".Translate(),
                    () => { State.CurrentMode = PlaystyleMode.Dynamic; Close(); });

                DrawRow(inRect, ref y, btnW, rowH, descOffset,
                    "PS_ContinueSwapMode".Translate(),
                    "PS_ContinueSwapMode_Desc".Translate(),
                    () => { State.CurrentMode = PlaystyleMode.Swap; Close(); });

                DrawRow(inRect, ref y, btnW, rowH, descOffset,
                    "PS_ContinueDirectorMode".Translate(),
                    "PS_ContinueDirectorMode_Desc".Translate(),
                    () => { State.CurrentMode = PlaystyleMode.Director; Close(); });

                y += 6f;
            }

            var endRect = new Rect(inRect.x + inRect.width * 5f / 16f, y + 30, inRect.width * 3f / 8f, 44f);
            if (Widgets.CustomButtonText(ref endRect, "PS_EndRun".Translate(), EndRunBgColor, Color.white, EndRunBgColor))
                GenScene.GoToMainMenu();

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawRow(Rect inRect, ref float y,
            float btnW, float rowH, float descOffset,
            string label, string desc, System.Action onClick)
        {
            float descW = inRect.width - descOffset;
            float btnY = y + 2;

            if (Widgets.ButtonText(new Rect(inRect.x, btnY, btnW, rowH - 4), label))
                onClick();
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x + descOffset, y, descW, rowH), desc);
            Text.Anchor = TextAnchor.UpperLeft;
            y += rowH;
        }
    }
}
