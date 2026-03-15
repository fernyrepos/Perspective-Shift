using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    public class Dialog_YouDied : Window
    {
        public override Vector2 InitialSize => new Vector2(560f, 470f);

        private static readonly Color DeathCauseColor = new Color(0.85f, 0.3f, 0.25f);
        private static readonly Color EndRunBgColor = new Color(0.50f, 0.13f, 0.13f);

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
            const float btnH = 32f;
            const float gap = 6f;
            const float rowH = 72f;
            const float descOffset = btnW + 14f;
            const float portraitSize = 48f;
            var portraitRect = new Rect(inRect.x, inRect.y, portraitSize, portraitSize);
            var portrait = PortraitsCache.Get(pawn, new Vector2(portraitSize, portraitSize), Rot4.South);
            GUI.DrawTexture(portraitRect, portrait);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inRect.x + portraitSize, inRect.y, inRect.width - portraitSize, 42f), "PS_YouDied".Translate());
            Text.Font = GameFont.Small;
            GUI.color = DeathCauseColor;
            Widgets.Label(new Rect(inRect.x, inRect.y + 44f, inRect.width, 24f), deathMessage);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.UpperLeft;
            float y = inRect.y + 80f;

            if (!PerspectiveShiftMod.settings.permadeath)
            {
                DrawRow(inRect, ref y, btnW, btnH, rowH, gap, descOffset,
                    "PS_InhabitNewCharacter".Translate(),
                    "PS_InhabitNewCharacter_Desc".Translate(),
                    () => { Find.WindowStack.Add(new Page_ChooseStartingCharacter()); Close(); });

                DrawRow(inRect, ref y, btnW, btnH, rowH, gap, descOffset,
                    "PS_ContinueDynamicMode".Translate(),
                    "PS_ContinueDynamicMode_Desc".Translate(),
                    () => { State.CurrentMode = PlaystyleMode.Dynamic; Close(); });

                DrawRow(inRect, ref y, btnW, btnH, rowH, gap, descOffset,
                    "PS_ContinueSwapMode".Translate(),
                    "PS_ContinueSwapMode_Desc".Translate(),
                    () => { State.CurrentMode = PlaystyleMode.Swap; Close(); });

                DrawRow(inRect, ref y, btnW, btnH, rowH, gap, descOffset,
                    "PS_ContinueDirectorMode".Translate(),
                    "PS_ContinueDirectorMode_Desc".Translate(),
                    () => { State.CurrentMode = PlaystyleMode.Director; Close(); });

                y += 6f;
            }

            var endRect = new Rect(inRect.x + inRect.width / 4f, y, inRect.width / 2f, btnH);
            GUI.backgroundColor = EndRunBgColor;
            if (Widgets.ButtonText(endRect, "PS_EndRun".Translate()))
                GenScene.GoToMainMenu();
            GUI.backgroundColor = Color.white;

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawRow(Rect inRect, ref float y,
            float btnW, float btnH, float rowH, float gap, float descOffset,
            string label, string desc, System.Action onClick)
        {
            float descW = inRect.width - descOffset;
            float btnY = y + (rowH - btnH) / 2f;

            if (Widgets.ButtonText(new Rect(inRect.x, btnY, btnW, btnH), label))
                onClick();

            Widgets.Label(new Rect(inRect.x + descOffset, y, descW, rowH), desc);

            y += rowH + gap;
        }
    }
}
