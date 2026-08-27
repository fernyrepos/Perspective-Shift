using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PerspectiveShift
{
    [HotSwappable]
    public class Dialog_InhabitedControls : Window
    {
        private struct ControlRow
        {
            public string label;
            public string desc;
            public bool isHeader;
        }

        private const float RowHeight = 30f;
        private const float HeaderHeight = 30f;
        private const float ListPadding = 8f;
        private const float SideMargin = 24f;
        private const float KeyColumnWidth = 190f;
        private const float ColumnGap = 18f;
        private const float TitleBlock = 46f;
        private const float WarningBlock = 42f;
        private const float ButtonBlock = 44f;
        private const float BlockGap = 14f;

        private static readonly Color KeyCapFill = new Color(0.16f, 0.17f, 0.19f);
        private static readonly Color KeyCapBorder = new Color(0.36f, 0.38f, 0.42f);
        private static readonly Color HeaderColor = new ColorInt(146, 190, 231).ToColor;

        private readonly List<ControlRow> rows = new List<ControlRow>();
        private bool neverShowAgain;

        public Dialog_InhabitedControls()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = false;
            BuildRows();
        }

        public override Vector2 InitialSize =>
            new Vector2(740f, TitleBlock + ListHeight + BlockGap + WarningBlock + ButtonBlock + Margin * 2f);

        private float ListHeight
        {
            get
            {
                var height = ListPadding * 2f;
                foreach (var row in rows)
                    height += row.isHeader ? HeaderHeight : RowHeight;
                return height;
            }
        }

        private void BuildRows()
        {
            var settings = PerspectiveShiftMod.settings;

            AddHeader("PS_ControlsMovement");
            AddRow("PS_KeyWASD", "PS_ControlMove");
            if (settings.enableSprinting) AddRow("PS_KeyShiftHold", "PS_ControlSprint");
            if (settings.enableSneaking) AddRow("PS_KeyCtrlHold", "PS_ControlSneak");

            AddHeader("PS_ControlsInteracting");
            AddRow("PS_KeyLeftClick", "PS_ControlLeftClick");
            AddRow("PS_KeyLeftClickHold", "PS_ControlLeftClickHold");
            AddRow("PS_KeyRightClick", "PS_ControlRightClick");
            AddRow("PS_KeyRightClickHauling", "PS_ControlStoreItem");
            if (!settings.disableDoubleClickEquip) AddRow("PS_KeyDoubleClickHauling", "PS_ControlEquipItem");

            AddHeader("PS_ControlsCharacter");
            AddRow("PS_KeyE", "PS_ControlInventory");
            AddRow("PS_KeyQ", "PS_ControlNeeds");
            AddRow("PS_KeyH", "PS_ControlHealth");
            AddRow("PS_KeyF", "PS_ControlEat");
            AddRow("PS_KeyJ", "PS_ControlRecreation");
        }

        private void AddHeader(string key) => rows.Add(new ControlRow { label = key.Translate(), isHeader = true });

        private void AddRow(string keyLabel, string descKey) =>
            rows.Add(new ControlRow { label = keyLabel.Translate(), desc = descKey.Translate() });

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "PS_InhabitedControls".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            var listRect = new Rect(inRect.x, inRect.y + TitleBlock, inRect.width, ListHeight);
            Widgets.DrawMenuSection(listRect);
            DrawRows(new Rect(listRect.x + SideMargin, listRect.y + ListPadding,
                listRect.width - SideMargin * 2f, listRect.height - ListPadding * 2f));

            var warningRect = new Rect(inRect.x + SideMargin, listRect.yMax + BlockGap,
                inRect.width - SideMargin * 2f, WarningBlock);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(warningRect, "PS_InhabitedControlsRebindWarning".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            var buttonRect = new Rect(inRect.x + (inRect.width - 200f) / 2f, warningRect.yMax, 200f, 38f);
            if (Widgets.ButtonText(buttonRect, "Close".Translate())) Close();

            DrawNeverShowAgain(new Rect(inRect.x, buttonRect.y, buttonRect.x - inRect.x - 10f, buttonRect.height));
        }

        public override void PreClose()
        {
            base.PreClose();
            if (!neverShowAgain) return;
            var settings = PerspectiveShiftMod.settings;
            if (!settings.showControlsOnFirstInhabit) return;
            settings.showControlsOnFirstInhabit = false;
            LoadedModManager.GetMod<PerspectiveShiftMod>()?.WriteSettings();
        }

        private void DrawNeverShowAgain(Rect rect)
        {
            const float checkSize = 24f;
            var label = "PS_NeverShowAgain".Translate();

            Text.Font = GameFont.Small;
            var rowRect = new Rect(rect.x, rect.y, Mathf.Min(rect.width, checkSize + 8f + Text.CalcSize(label).x), rect.height);

            Widgets.DrawHighlightIfMouseover(rowRect);
            if (Widgets.ButtonInvisible(rowRect))
            {
                neverShowAgain = !neverShowAgain;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            Widgets.CheckboxDraw(rowRect.x, rowRect.y + (rowRect.height - checkSize) / 2f, neverShowAgain, false, checkSize);

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(rowRect.x + checkSize + 8f, rowRect.y, rowRect.width - checkSize - 8f, rowRect.height), label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawRows(Rect inner)
        {
            var curY = inner.y;
            var stripe = false;
            foreach (var row in rows)
            {
                if (row.isHeader)
                {
                    DrawHeader(new Rect(inner.x, curY, inner.width, HeaderHeight), row.label);
                    curY += HeaderHeight;
                    stripe = false;
                    continue;
                }

                var rowRect = new Rect(inner.x, curY, inner.width, RowHeight);
                if (stripe) Widgets.DrawLightHighlight(rowRect);
                stripe = !stripe;

                DrawKeyCap(new Rect(rowRect.x, rowRect.y + 2f, KeyColumnWidth, RowHeight - 4f), row.label);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rowRect.x + KeyColumnWidth + ColumnGap, rowRect.y,
                    rowRect.width - KeyColumnWidth - ColumnGap, RowHeight), row.desc);
                Text.Anchor = TextAnchor.UpperLeft;

                curY += RowHeight;
            }
        }

        private static void DrawHeader(Rect rect, string label)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerLeft;
            GUI.color = HeaderColor;
            var labelRect = new Rect(rect.x, rect.y, rect.width, rect.height - 6f);
            Widgets.Label(labelRect, label);

            var labelWidth = Text.CalcSize(label).x + 10f;
            GUI.color = new Color(HeaderColor.r, HeaderColor.g, HeaderColor.b, 0.25f);
            Widgets.DrawLineHorizontal(rect.x + labelWidth, labelRect.yMax - 8f, rect.width - labelWidth);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void DrawKeyCap(Rect column, string label)
        {
            var capWidth = Mathf.Min(column.width, Text.CalcSize(label).x + 20f);
            var capRect = new Rect(column.xMax - capWidth, column.y, capWidth, column.height);

            Widgets.DrawBoxSolid(capRect, KeyCapFill);
            GUI.color = KeyCapBorder;
            Widgets.DrawBox(capRect);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(capRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
