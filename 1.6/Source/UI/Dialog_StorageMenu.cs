using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PerspectiveShift
{
    [HotSwappable]
    public class Dialog_StorageMenu : Window
    {
        private Building_Storage storage;
        private CompStorageSlotOrder slotComp;
        private Thing cursorItem;
        private Vector2 scrollPosition;
        public override Vector2 InitialSize => new Vector2(600f, 600f);
        public Dialog_StorageMenu(Building_Storage storage)
        {
            this.storage = storage;
            this.slotComp = storage.GetComp<CompStorageSlotOrder>();
            this.closeOnClickedOutside = true;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.absorbInputAroundWindow = false;
            this.forcePause = true;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            DefsOf.PS_StorageOpen.PlayOneShot(State.Avatar.pawn);
        }

        public override void PreClose()
        {
            base.PreClose();
            if (cursorItem != null && !cursorItem.Destroyed)
            {
                GenPlace.TryPlaceThing(cursorItem, State.Avatar.pawn.Position, State.Avatar.pawn.Map, ThingPlaceMode.Near);
                cursorItem = null;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            const int itemsPerRow = 5;
            const float cellSpacing = 15f;
            const float iconPadding = 10f;
            const float headerHeight = 35f;
            const float storageButtonWidth = 100f;
            const float storageButtonHeight = 30f;
            const float bottomHeight = 80f;
            const float headerMargin = 10f;
            const float bottomMargin = 10f;
            const float gridBottomMargin = 10f;

            var headerRect = new Rect(inRect.x, inRect.y, inRect.width, headerHeight);

            var storageBtnRect = new Rect(headerRect.x, headerRect.y, storageButtonWidth, storageButtonHeight);
            if (Widgets.ButtonText(storageBtnRect, "PS_Storage".Translate()))
            {
                Find.WindowStack.Add(new Dialog_StorageSettings(storage));
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(headerRect, storage.LabelNoParenthesisCap);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            var bottomRect = new Rect(inRect.x, inRect.yMax - bottomHeight, inRect.width, bottomHeight);
            Widgets.DrawMenuSection(bottomRect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(bottomRect, "PS_DropItemHereToTake".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            if (Mouse.IsOver(bottomRect))
            {
                Widgets.DrawHighlight(bottomRect);
                if (Event.current.type == EventType.MouseUp && cursorItem != null)
                {
                    State.Avatar.pawn.carryTracker.TryStartCarry(cursorItem, cursorItem.stackCount, reserve: true);
                    cursorItem.def.soundDrop.PlayOneShot(State.Avatar.pawn);
                    cursorItem = null;
                    Close();
                    Event.current.Use();
                }
            }

            var gridOutRect = new Rect(inRect.x, headerRect.yMax + headerMargin, inRect.width, inRect.height - headerRect.height - bottomMargin - bottomHeight - gridBottomMargin);

            var allCells = storage.AllSlotCellsList();
            int maxPerCell = storage.def.building.maxItemsInCell;
            int capacity = maxPerCell * allCells.Count;
            slotComp.ReconcileStaleEntries(storage);
            var itemsBySlot = slotComp.BuildSlotArray(storage);

            int cols = itemsPerRow;
            float cellSize = (gridOutRect.width - 16f - (cols - 1) * cellSpacing) / cols;
            var rows = Mathf.CeilToInt((float)capacity / cols);
            float viewHeight = rows * (cellSize + cellSpacing);
            var viewRect = new Rect(0f, 0f, gridOutRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(gridOutRect, ref scrollPosition, viewRect);

            for (int i = 0; i < capacity; i++)
            {
                int row = i / cols;
                int col = i % cols;
                var cellRect = new Rect(col * (cellSize + cellSpacing), row * (cellSize + cellSpacing), cellSize, cellSize);
                Widgets.DrawMenuSection(cellRect);

                Thing item = itemsBySlot[i];

                if (item != null)
                {
                    var iconRect = new Rect(cellRect.x + iconPadding, cellRect.y + iconPadding, cellRect.width - iconPadding * 2f, cellRect.height - iconPadding * 2f);
                    Widgets.ThingIcon(iconRect, item);
                    if (item.stackCount > 1 || item.def.stackLimit > 1)
                    {
                        Text.Anchor = TextAnchor.LowerRight;
                        var labelRect = new Rect(cellRect.x, cellRect.y, cellRect.width - 2f, cellRect.height - 2f);
                        Widgets.Label(labelRect, item.stackCount.ToString());
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                    TooltipHandler.TipRegion(cellRect, item.LabelCap);
                }

                if (Mouse.IsOver(cellRect))
                {
                    Widgets.DrawHighlight(cellRect);
                    if (Event.current.type == EventType.MouseDown)
                    {
                        IntVec3 targetCell = allCells[i / maxPerCell];
                        int cellIdx = i / maxPerCell;
                        HandleClick(item, Event.current.button, targetCell, cellIdx, i);
                        Event.current.Use();
                    }
                }
            }

            Widgets.EndScrollView();

            if (cursorItem != null && !cursorItem.Destroyed)
            {
                var mouseRect = new Rect(Event.current.mousePosition.x - cellSize / 2f, Event.current.mousePosition.y - cellSize / 2f, cellSize, cellSize);
                var iconRect = new Rect(mouseRect.x + iconPadding, mouseRect.y + iconPadding, mouseRect.width - iconPadding * 2f, mouseRect.height - iconPadding * 2f);
                Widgets.ThingIcon(iconRect, cursorItem);
                if (cursorItem.stackCount > 1 || cursorItem.def.stackLimit > 1)
                {
                    Text.Anchor = TextAnchor.LowerRight;
                    var labelRect = new Rect(mouseRect.x, mouseRect.y, mouseRect.width - 2f, mouseRect.height - 2f);
                    Widgets.Label(labelRect, cursorItem.stackCount.ToString());
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
        }

        private void PlaceAndRegister(Thing item, IntVec3 targetCell, int cellIdx, int slotIdx)
        {
            GenSpawn.Spawn(item, targetCell, storage.Map, WipeMode.Vanish);

            if (!item.Destroyed)
            {
                slotComp.SetItemSlot(item.thingIDNumber, slotIdx);
                storage.Map.mapDrawer.MapMeshDirty(targetCell, MapMeshFlagDefOf.Things);
            }
        }

        private void HandleClick(Thing clickedItem, int button, IntVec3 targetCell, int cellIdx, int slotIdx)
        {
            if (button == 0)
            {
                if (cursorItem == null && clickedItem != null)
                {
                    cursorItem = clickedItem.SplitOff(clickedItem.stackCount);
                    slotComp.AddGap(slotIdx);
                    clickedItem.def.soundPickup.PlayOneShot(State.Avatar.pawn);
                }
                else if (cursorItem != null && clickedItem == null)
                {
                    if (storage.GetStoreSettings().AllowedToAccept(cursorItem))
                    {
                        var toPlace = cursorItem;
                        cursorItem = null;
                        slotComp.RemoveGap(slotIdx);
                        PlaceAndRegister(toPlace, targetCell, cellIdx, slotIdx);
                        toPlace.def.soundDrop.PlayOneShot(State.Avatar.pawn);
                    }
                    else
                    {
                        Messages.Message("PS_StorageDoesNotAccept".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                }
                else if (cursorItem != null && clickedItem != null)
                {
                    if (cursorItem.CanStackWith(clickedItem))
                    {
                        int room = clickedItem.def.stackLimit - clickedItem.stackCount;
                        if (room > 0)
                        {
                            var transfer = Mathf.Min(room, cursorItem.stackCount);
                            clickedItem.TryAbsorbStack(cursorItem.SplitOff(transfer), true);
                            if (cursorItem.stackCount == 0) cursorItem = null;
                            clickedItem.def.soundDrop.PlayOneShot(State.Avatar.pawn);
                        }
                    }
                    else
                    {
                        if (storage.GetStoreSettings().AllowedToAccept(cursorItem))
                        {
                            var temp = cursorItem;
                            cursorItem = clickedItem.SplitOff(clickedItem.stackCount);
                            slotComp.RemoveGap(slotIdx);
                            PlaceAndRegister(temp, targetCell, cellIdx, slotIdx);
                            cursorItem.def.soundPickup.PlayOneShot(State.Avatar.pawn);
                        }
                        else
                        {
                            Messages.Message("PS_StorageDoesNotAccept".Translate(), MessageTypeDefOf.RejectInput, false);
                        }
                    }
                }
            }
            else if (button == 1)
            {
                if (cursorItem == null && clickedItem != null)
                {
                    var toTake = Mathf.CeilToInt(clickedItem.stackCount / 2f);
                    cursorItem = clickedItem.SplitOff(toTake);
                    if (clickedItem.Destroyed || clickedItem.stackCount == 0)
                        slotComp.AddGap(slotIdx);
                    cursorItem.def.soundPickup.PlayOneShot(State.Avatar.pawn);
                }
                else if (cursorItem != null)
                {
                    if (clickedItem == null)
                    {
                        if (storage.GetStoreSettings().AllowedToAccept(cursorItem))
                        {
                            slotComp.RemoveGap(slotIdx);
                            var single = cursorItem.SplitOff(1);
                            PlaceAndRegister(single, targetCell, cellIdx, slotIdx);
                            if (single == cursorItem || cursorItem.stackCount == 0) cursorItem = null;
                            single.def.soundDrop.PlayOneShot(State.Avatar.pawn);
                        }
                        else
                        {
                            Messages.Message("PS_StorageDoesNotAccept".Translate(), MessageTypeDefOf.RejectInput, false);
                        }
                    }
                    else if (clickedItem.CanStackWith(cursorItem) && clickedItem.stackCount < clickedItem.def.stackLimit)
                    {
                        var single = cursorItem.SplitOff(1);
                        clickedItem.TryAbsorbStack(single, true);
                        if (single == cursorItem || cursorItem.stackCount == 0) cursorItem = null;
                        clickedItem.def.soundDrop.PlayOneShot(State.Avatar.pawn);
                    }
                }
            }
        }
    }
}
