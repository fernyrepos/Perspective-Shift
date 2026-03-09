using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;

namespace PerspectiveShift
{
    [HotSwappable]
    public class Page_ChooseStartingCharacter : Page
    {
        private Pawn selectedPawn;
        private Vector2 scrollPosition;
        public override string PageTitle => State.CurrentMode == PlaystyleMode.Authentic
            ? "PS_ChooseYourCharacter".Translate()
            : "PS_ChooseYourStartingCharacter".Translate();

        private List<Pawn> StartingPawns =>
            Find.GameInitData.startingAndOptionalPawns
                .Take(Find.GameInitData.startingPawnCount)
                .ToList();

        public override void PreOpen()
        {
            base.PreOpen();
            selectedPawn = StartingPawns.FirstOrDefault();
        }

        public override void PostOpen()
        {
            base.PostOpen();
            if (State.CurrentMode == PlaystyleMode.Director || StartingPawns.Count <= 1)
            {
                if (State.CurrentMode != PlaystyleMode.Director && StartingPawns.Count == 1)
                {
                    State.Avatar = new Avatar(StartingPawns[0]);
                }
                DoNext();
                return;
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            DrawPageTitle(rect);

            var mainRect = GetMainRect(rect);
            mainRect.yMin += 10f;

            DrawPawnList(mainRect);

            DoBottomButtons(rect, "Start".Translate(), null, null, showNext: true, doNextOnKeypress: false);
        }

        private void DrawPawnList(Rect rect)
        {
            List<Pawn> pawns = StartingPawns;

            float cardWidth = 160f;
            float cardHeight = 220f;
            float gap = 20f;
            int columns = 5;

            var totalRows = Mathf.CeilToInt((float)pawns.Count / columns);
            var actualColumns = Mathf.Min(pawns.Count, columns);
            float contentWidth = actualColumns * cardWidth + (actualColumns - 1) * gap;
            float totalHeight = totalRows * cardHeight + (totalRows - 1) * gap;

            var viewRect = new Rect(0f, 0f, contentWidth, totalHeight);
            var scrollRect = new Rect(rect.x + (rect.width - contentWidth) / 2f, rect.y, contentWidth, rect.height);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float startY = 0f;
            if (totalRows <= 2 && totalHeight < scrollRect.height)
            {
                startY = (scrollRect.height - totalHeight) / 2f;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                int col = i % columns;
                int row = i / columns;
                var cardRect = new Rect(col * (cardWidth + gap), startY + row * (cardHeight + gap), cardWidth, cardHeight);

                Widgets.DrawMenuSection(cardRect);
                if (selectedPawn == pawn)
                    Widgets.DrawHighlight(cardRect);

                if (Widgets.ButtonInvisible(cardRect))
                {
                    selectedPawn = pawn;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                var portraitRect = new Rect(cardRect.x + (cardWidth - 100f) / 2f, cardRect.y + 10f, 100f, 140f);
                GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(100f, 140f), Rot4.South, default, 1f, supersample: true, compensateForUIScale: true, true, true));

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                var nameRect = new Rect(cardRect.x, portraitRect.yMax + 5f, cardWidth, 25f);
                Widgets.Label(nameRect, pawn.Name.ToStringShort);

                Text.Font = GameFont.Tiny;
                var titleRect = new Rect(cardRect.x, nameRect.yMax + 2f, cardWidth, 20f);
                Widgets.Label(titleRect, pawn.story?.TitleCap ?? "");

                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.EndScrollView();
        }

        public override bool CanDoNext()
        {
            if (State.CurrentMode == PlaystyleMode.Director)
            {
                return true;
            }
            if (selectedPawn == null)
            {
                Messages.Message("PS_MustSelectCharacter".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }

        public override void DoNext()
        {
            if (State.CurrentMode != PlaystyleMode.Director && selectedPawn != null)
                State.Avatar = new Avatar(selectedPawn);
            base.DoNext();
        }

        public override void DoBack()
        {
            State.Avatar = null;
            base.DoBack();
        }
    }
}
