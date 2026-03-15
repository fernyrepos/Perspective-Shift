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
        private List<Pawn> _allPawnsCache;
        public override string PageTitle => State.CurrentMode == PlaystyleMode.Authentic
            ? "PS_ChooseYourCharacter".Translate()
            : "PS_ChooseYourStartingCharacter".Translate();

        private List<Pawn> StartingPawns
        {
            get
            {
                if (_allPawnsCache != null) return _allPawnsCache;

                if (Current.ProgramState == ProgramState.Playing)
                {
                    _allPawnsCache = Find.Maps
                        .SelectMany(m => m.mapPawns.FreeColonistsSpawned)
                        .Where(p => !p.Dead)
                        .ToList();
                    return _allPawnsCache;
                }

                var list = Find.GameInitData.startingAndOptionalPawns
                    .Take(Find.GameInitData.startingPawnCount)
                    .ToList();

                if (PerspectiveShiftMod.settings.allowNonHuman)
                {
                    foreach (var part in Find.Scenario.AllParts.OfType<ScenPart_StartingAnimal>())
                    {
                        if (!ScenPart_StartingAnimal_PlayerStartingThings_Patch.cachedAnimals.TryGetValue(part, out var cache))
                        {
                            ScenPart_StartingAnimal_PlayerStartingThings_Patch.bypassPart = part;
                            cache = part.PlayerStartingThings().ToList();
                            ScenPart_StartingAnimal_PlayerStartingThings_Patch.cachedAnimals[part] = cache;
                            ScenPart_StartingAnimal_PlayerStartingThings_Patch.bypassPart = null;
                        }
                        foreach (var t in cache) if (t is Pawn p) list.Add(p);
                    }
                }
                _allPawnsCache = list;
                return _allPawnsCache;
            }
        }

        public override void PreOpen()
        {
            base.PreOpen();
            _allPawnsCache = null;
            ScenPart_StartingAnimal_PlayerStartingThings_Patch.ClearCache();
            selectedPawn = StartingPawns.FirstOrDefault();
        }

        public override void PostOpen()
        {
            base.PostOpen();
            if (State.CurrentMode == PlaystyleMode.Director ||
                (StartingPawns.Count <= 1 && Current.ProgramState != ProgramState.Playing))
            {
                if (State.CurrentMode != PlaystyleMode.Director && StartingPawns.Count == 1)
                {
                    State.SetAvatar(StartingPawns[0]);
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
                Widgets.Label(nameRect, pawn.Name?.ToStringShort ?? pawn.LabelShort);

                Text.Font = GameFont.Tiny;
                var titleRect = new Rect(cardRect.x, nameRect.yMax + 2f, cardWidth, 20f);
                Widgets.Label(titleRect, pawn.story?.TitleCap ?? pawn.KindLabel.CapitalizeFirst());

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
                State.SetAvatar(selectedPawn);
            base.DoNext();
        }

        public override void DoBack()
        {
            State.Avatar = null;
            base.DoBack();
        }
    }
}
