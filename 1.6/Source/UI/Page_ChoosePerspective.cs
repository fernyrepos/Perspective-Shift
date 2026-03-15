using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PerspectiveShift
{
    [HotSwappable]
    [StaticConstructorOnStartup]
    public class Page_ChoosePerspective : Page
    {
        private enum PageStep
        {
            Role,
            Playstyle,
            AuthenticOptions
        }

        private PageStep step = PageStep.Role;
        private bool roleIsCharacter = false;
        private PlaystyleMode selectedPlaystyle = PlaystyleMode.Authentic;

        private static Texture2D _directorIcon;
        private static Texture2D DirectorIcon =>
            _directorIcon ??= ContentFinder<Texture2D>.Get("MainMenu/DirectorMode");

        private static Texture2D _characterIcon;
        private static Texture2D CharacterIcon =>
            _characterIcon ??= ContentFinder<Texture2D>.Get("MainMenu/CharacterMode");

        private static Texture2D _authenticIcon;
        private static Texture2D AuthenticIcon =>
            _authenticIcon ??= ContentFinder<Texture2D>.Get("MainMenu/AuthenticMode");

        private static Texture2D _swapIcon;
        private static Texture2D SwapIcon =>
            _swapIcon ??= ContentFinder<Texture2D>.Get("MainMenu/SwapMode");

        private static Texture2D _dynamicIcon;
        private static Texture2D DynamicIcon =>
            _dynamicIcon ??= ContentFinder<Texture2D>.Get("MainMenu/DynamicMode");

        public override string PageTitle => step == PageStep.Role
            ? "PS_SelectRole".Translate()
            : step == PageStep.Playstyle
                ? "PS_SelectPlaystyle".Translate()
                : "PS_ModeAuthentic".Translate();

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 35f), PageTitle);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            var mainRect = GetMainRect(rect);
            mainRect.yMin += 10f;

            if (step == PageStep.Role)
                DrawRoleSelection(mainRect);
            else if (step == PageStep.Playstyle)
                DrawPlaystyleSelection(mainRect);
            else
                DrawAuthenticOptions(mainRect);

            string nextLabel = (step == PageStep.Role && !roleIsCharacter) ? "Start".Translate() : "Next".Translate();
            DoBottomButtons(rect, nextLabel, null, null, true, true);
        }

        private void DrawRoleOption(Rect rect, string label, string desc, Texture2D icon, bool selected, Action onSelect)
        {
            Widgets.DrawMenuSection(rect);

            if (selected)
            {
                Widgets.DrawHighlight(rect);
            }

            if (Widgets.ButtonInvisible(rect))
            {
                onSelect();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            var contentRect = rect.ContractedBy(20f);

            if (icon != null)
            {
                var iconRect = new Rect(rect.x, rect.y, rect.width, rect.width);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            var labelHeight = Text.CalcHeight(label, contentRect.width);
            var labelRect = new Rect(contentRect.x, contentRect.yMax - 80f, contentRect.width, labelHeight);
            Widgets.Label(labelRect, label);

            Text.Font = GameFont.Small;
            var descHeight = Text.CalcHeight(desc, contentRect.width);
            var descRect = new Rect(contentRect.x, labelRect.yMax + 5f, contentRect.width, descHeight);
            Widgets.Label(descRect, desc);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawRoleSelection(Rect rect)
        {
            rect.y += 10f;
            rect.height -= 40f;
            float cardWidth = 400f;
            float cardHeight = rect.height;
            float gap = 75f;

            float totalWidth = cardWidth * 2 + gap;
            float startX = rect.x + (rect.width - totalWidth) / 2f;
            float startY = rect.y + (rect.height - cardHeight) / 2f - 10f;

            var leftRect = new Rect(startX, startY, cardWidth, cardHeight);
            var rightRect = new Rect(startX + cardWidth + gap, startY, cardWidth, cardHeight);

            DrawRoleOption(leftRect,
                "PS_RoleDirector".Translate(),
                "PS_RoleDirectorDesc".Translate(),
                DirectorIcon,
                !roleIsCharacter,
                () => roleIsCharacter = false);

            DrawRoleOption(rightRect,
                "PS_RoleCharacter".Translate(),
                "PS_RoleCharacterDesc".Translate(),
                CharacterIcon,
                roleIsCharacter,
                () => roleIsCharacter = true);
        }

        private void DrawPlaystyleOption(Rect rect, PlaystyleMode mode, string label, string desc, Texture2D icon)
        {
            bool selected = selectedPlaystyle == mode;

            Widgets.DrawMenuSection(rect);

            if (selected)
            {
                Widgets.DrawHighlight(rect);
            }

            if (Widgets.ButtonInvisible(rect))
            {
                selectedPlaystyle = mode;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            var contentRect = rect.ContractedBy(15f);

            float iconHeight = contentRect.height;
            float iconWidth = iconHeight * 1.75f;
            var iconRect = new Rect(contentRect.xMax - iconWidth, contentRect.y, iconWidth, iconHeight);
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            float textWidth = contentRect.width - iconWidth - 30f;
            var textRect = new Rect(contentRect.x, contentRect.y, textWidth, contentRect.height);

            var titleStyle = new GUIStyle(Text.CurFontStyle);
            titleStyle.fontSize = 24;
            Text.Anchor = TextAnchor.UpperLeft;
            var titleRect = new Rect(textRect.x, textRect.y, textRect.width, 50f);
            GUI.Label(titleRect, label, titleStyle);

            Text.Font = GameFont.Small;
            var descRect = new Rect(textRect.x, titleRect.yMax + 5f, textRect.width, textRect.height - 55f);
            Widgets.Label(descRect, desc);
        }

        private void DrawPlaystyleSelection(Rect rect)
        {
            float itemHeight = 180f;
            float gap = 20f;
            float totalHeight = itemHeight * 3 + gap * 2;
            float margin = 20f;
            float width = rect.width - margin * 2;

            float startX = rect.x + margin;
            float startY = rect.y + (rect.height - totalHeight) / 2f - 20f;

            var r1 = new Rect(startX, startY, width, itemHeight);
            var r2 = new Rect(startX, startY + itemHeight + gap, width, itemHeight);
            var r3 = new Rect(startX, startY + (itemHeight + gap) * 2, width, itemHeight);

            DrawPlaystyleOption(r1, PlaystyleMode.Authentic,
                "PS_ModeAuthentic".Translate(),
                "PS_ModeAuthenticDesc".Translate(), AuthenticIcon);

            DrawPlaystyleOption(r2, PlaystyleMode.Swap,
                "PS_ModeSwap".Translate(),
                "PS_ModeSwapDesc".Translate(), SwapIcon);

            DrawPlaystyleOption(r3, PlaystyleMode.Dynamic,
                "PS_ModeDynamic".Translate(),
                "PS_ModeDynamicDesc".Translate(), DynamicIcon);
        }

        private void DrawAuthenticOption(Rect rect, string title, string desc, ref bool value)
        {
            if (Widgets.ButtonInvisible(rect))
            {
                value = !value;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            var inner = rect.ContractedBy(20f);

            float checkSize = 24f;
            var checkRect = new Rect(inner.xMax - checkSize - 100f, inner.y, checkSize, checkSize);
            Widgets.Checkbox(checkRect.x, checkRect.y, ref value);

            Text.Font = GameFont.Medium;
            var titleRect = new Rect(inner.x, inner.y, inner.width - checkSize - 110f, 30f);
            Widgets.Label(titleRect, title);

            Text.Font = GameFont.Small;
            var descRect = new Rect(inner.x, titleRect.yMax + 8f, inner.width - checkSize - 110f, inner.height - titleRect.height - 8f);
            Widgets.Label(descRect, desc);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawAuthenticOptions(Rect rect)
        {
            float panelHeight = 190f;
            float gap = 20f;
            float margin = 20f;
            float width = rect.width - margin * 2f;
            float startX = rect.x + margin;
            float startY = rect.y + (rect.height - (panelHeight * 2f + gap)) / 2f - 20f;

            var r1 = new Rect(startX, startY, width, panelHeight);
            DrawAuthenticOption(r1,
                "PS_Permadeath".Translate(),
                "PS_PermadeathDesc".Translate(),
                ref PerspectiveShiftMod.settings.permadeath);

            var r2 = new Rect(startX, startY + panelHeight + gap, width, panelHeight);
            DrawAuthenticOption(r2,
                "PS_AllowSwitchingToDirectorMode".Translate(),
                "PS_AllowSwitchingToDirectorModeDesc".Translate(),
                ref PerspectiveShiftMod.settings.allowDirectorInAuthentic);
        }

        public override void DoBack()
        {
            if (step == PageStep.AuthenticOptions)
            {
                step = PageStep.Playstyle;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            else if (step == PageStep.Playstyle)
            {
                step = PageStep.Role;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            else
            {
                base.DoBack();
            }
        }

        public override void DoNext()
        {
            if (step == PageStep.Role)
            {
                if (!roleIsCharacter)
                {
                    State.CurrentMode = PlaystyleMode.Director;
                    State.ClearAvatar();
                    base.DoNext();
                }
                else
                {
                    step = PageStep.Playstyle;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }
            else if (step == PageStep.Playstyle)
            {
                if (selectedPlaystyle == PlaystyleMode.Authentic)
                {
                    step = PageStep.AuthenticOptions;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                else
                {
                    State.CurrentMode = selectedPlaystyle;
                    State.ClearAvatar();
                    if (next == null && selectedPlaystyle == PlaystyleMode.Swap)
                        Find.WindowStack.Add(new Page_ChooseStartingCharacter());
                    base.DoNext();
                }
            }
            else
            {
                State.CurrentMode = PlaystyleMode.Authentic;
                State.ClearAvatar();
                if (next == null)
                    Find.WindowStack.Add(new Page_ChooseStartingCharacter());
                base.DoNext();
            }
        }
    }
}
