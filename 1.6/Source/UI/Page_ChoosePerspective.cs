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

        private const float RecommendedFlashDuration = 1.2f;
        private static readonly Color RecommendedColor = Color.white;

        private PageStep step = PageStep.Role;
        private bool roleIsCharacter = false;
        private PlaystyleMode selectedPlaystyle = PlaystyleMode.Authentic;
        private float stepShownAt;

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

        public override void PreOpen()
        {
            base.PreOpen();
            stepShownAt = Time.realtimeSinceStartup;
        }

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

            string nextLabel = NextButtonLabel();
            DrawBottomNote(rect);
            DoBottomButtons(rect, nextLabel, null, null, true, true);
        }

        private void DrawBottomNote(Rect rect)
        {
            float noteWidth = rect.width - BottomButSize.x * 2f - 40f;
            if (noteWidth <= 0f) return;

            var noteRect = new Rect(rect.x + BottomButSize.x + 20f, rect.y + rect.height - BottomButSize.y, noteWidth, BottomButSize.y);

            var prevFont = Text.Font;
            var prevAnchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            Widgets.Label(noteRect, "PS_SettingsChangeableNote".Translate());
            GUI.color = Color.white;
            Text.Anchor = prevAnchor;
            Text.Font = prevFont;
        }

        private string NextButtonLabel()
        {
            if (step == PageStep.Role)
                return roleIsCharacter ? "PS_Setup".Translate() : "Start".Translate();

            if (step == PageStep.Playstyle)
            {
                if (selectedPlaystyle == PlaystyleMode.Dynamic) return "Start".Translate();
                if (selectedPlaystyle == PlaystyleMode.Swap && Page_ChooseStartingCharacter.WillSkipToSingleCandidate())
                    return "Start".Translate();
                return "PS_Configure".Translate();
            }

            return "Next".Translate();
        }

        private void DrawRoleOption(Rect rect, string label, string desc, Texture2D icon, bool selected, bool recommended, Action onSelect)
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

            var iconRect = new Rect(rect.x, rect.y, rect.width, rect.width).ExpandedBy(21);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            if (recommended) DrawRecommendedTag(rect);

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

        private void DrawRecommendedTag(Rect cardRect)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            var label = "PS_Recommended".Translate();
            var size = Text.CalcSize(label);
            var tagRect = new Rect(cardRect.xMax - size.x - 14f, cardRect.y - 9f, size.x + 14f, 18f);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(tagRect, Widgets.WindowBGFillColor);
            GUI.color = Color.Lerp(RecommendedColor, Color.white, RecommendedFlashIntensity());
            Widgets.DrawBox(tagRect);
            Widgets.Label(tagRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private float RecommendedFlashIntensity()
        {
            var t = (Time.realtimeSinceStartup - stepShownAt) / RecommendedFlashDuration;
            if (t <= 0f || t >= 1f) return 0f;
            return 0.5f - (0.5f * Mathf.Cos(t * 2f * Mathf.PI));
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
                false,
                () => roleIsCharacter = false);

            DrawRoleOption(rightRect,
                "PS_RoleCharacter".Translate(),
                "PS_RoleCharacterDesc".Translate(),
                CharacterIcon,
                roleIsCharacter,
                ModCompatibility.ThemingModAvailable,
                () => roleIsCharacter = true);
        }

        private void DrawPlaystyleOption(Rect rect, PlaystyleMode mode, string label, string desc, Texture2D icon, bool recommended)
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

            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            float textWidth = contentRect.width - iconWidth - 30f;
            var textRect = new Rect(contentRect.x, contentRect.y, textWidth, contentRect.height);

            var titleStyle = new GUIStyle(Text.CurFontStyle);
            titleStyle.fontSize = 24;
            Text.Anchor = TextAnchor.UpperLeft;
            var titleRect = new Rect(textRect.x, textRect.y, textRect.width, 50f);
            GUI.Label(titleRect, label, titleStyle);

            if (recommended) DrawRecommendedTag(rect);

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
                "PS_ModeAuthenticDesc".Translate(), AuthenticIcon,
                ModCompatibility.ThemingModAvailable);

            DrawPlaystyleOption(r2, PlaystyleMode.Swap,
                "PS_ModeSwap".Translate(),
                "PS_ModeSwapDesc".Translate(), SwapIcon,
                false);

            DrawPlaystyleOption(r3, PlaystyleMode.Dynamic,
                "PS_ModeDynamic".Translate(),
                "PS_ModeDynamicDesc".Translate(), DynamicIcon,
                false);
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
            Widgets.CheckboxDraw(checkRect.x, checkRect.y, value, false, checkSize);

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
                ref State.permadeath);

            var r2 = new Rect(startX, startY + panelHeight + gap, width, panelHeight);
            DrawAuthenticOption(r2,
                "PS_AllowSwitchingToDirectorMode".Translate(),
                "PS_AllowSwitchingToDirectorModeDesc".Translate(),
                ref State.allowDirectorInAuthentic);
        }

        private void ShowStep(PageStep next)
        {
            step = next;
            stepShownAt = Time.realtimeSinceStartup;
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        public override void DoBack()
        {
            if (step == PageStep.AuthenticOptions)
            {
                ShowStep(PageStep.Playstyle);
            }
            else if (step == PageStep.Playstyle)
            {
                ShowStep(PageStep.Role);
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
                    ShowStep(PageStep.Playstyle);
                }
            }
            else if (step == PageStep.Playstyle)
            {
                if (selectedPlaystyle == PlaystyleMode.Authentic)
                {
                    ShowStep(PageStep.AuthenticOptions);
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
