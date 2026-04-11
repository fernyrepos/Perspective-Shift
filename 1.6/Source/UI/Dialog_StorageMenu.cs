using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PerspectiveShift
{
    [HotSwappable]
    [StaticConstructorOnStartup]
    public class Dialog_StorageMenu : Window
    {
        private Building_Storage storage;
        private CompStorageSlotOrder slotComp;
        private Thing cursorItem;
        private Vector2 scrollPosition;
        private static readonly Texture2D Keep = ContentFinder<Texture2D>.Get("Storage/Keep");
        private static readonly Texture2D Hold = ContentFinder<Texture2D>.Get("Storage/Hold");
        private static readonly Texture2D Equip = ContentFinder<Texture2D>.Get("Storage/Equip");
        private static readonly Texture2D Wear = ContentFinder<Texture2D>.Get("Storage/Wear");

        public override Vector2 InitialSize => new Vector2(600f, 600f);
        public Dialog_StorageMenu(Building_Storage storage)
        {
            this.storage = storage;
            slotComp = storage.GetComp<CompStorageSlotOrder>();
            closeOnClickedOutside = true;
            doCloseButton = false;
            doCloseX = true;
            absorbInputAroundWindow = false;
            forcePause = true;
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

            const float buttonSpacing = 30f;
            float btnW = (inRect.width - buttonSpacing * 2f) / 3f;
            float btnY = inRect.yMax - bottomHeight;

            var keepRect = new Rect(inRect.x, btnY, btnW, bottomHeight);
            var holdRect = new Rect(inRect.x + btnW + buttonSpacing, btnY, btnW, bottomHeight);
            var equipRect = new Rect(inRect.x + (btnW + buttonSpacing) * 2f, btnY, btnW, bottomHeight);

            bool hasItem = cursorItem != null && !cursorItem.Destroyed;
            bool isWeapon = hasItem && cursorItem.def.IsWeapon;
            bool isApparel = hasItem && cursorItem is Apparel;
            bool canEquip = isWeapon || isApparel;
            string equipLabel = isApparel ? "PS_Wear".Translate() : "PS_Equip".Translate();

            DrawActionButton(keepRect, Keep, "PS_Keep".Translate(), hasItem, (button) =>
            {
                int countToTake = (button == 1) ? 1 : cursorItem.stackCount;

                int maxCount = MassUtility.CountToPickUpUntilOverEncumbered(State.Avatar.pawn, cursorItem);
                if (maxCount < countToTake)
                {
                    countToTake = maxCount;
                }

                if (countToTake <= 0)
                {
                    Messages.Message("PS_CannotCarryMoreWeight".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                var item = cursorItem.SplitOff(countToTake);
                if (item == cursorItem) cursorItem = null;

                bool added = State.Avatar.pawn.inventory.innerContainer.TryAdd(item, true);
                if (added)
                {
                    item.def.soundDrop.PlayOneShot(State.Avatar.pawn);
                }
                else if (!item.Destroyed && item.stackCount > 0)
                {
                    if (cursorItem == null) cursorItem = item;
                    else cursorItem.TryAbsorbStack(item, true);
                }
                if (cursorItem != null && cursorItem.stackCount <= 0) cursorItem = null;
            });

            DrawActionButton(holdRect, Hold, "PS_Hold".Translate(), hasItem, (button) =>
            {
                int countToTake = (button == 1) ? 1 : cursorItem.stackCount;
                var item = cursorItem.SplitOff(countToTake);
                if (item == cursorItem) cursorItem = null;

                int originalCount = item.stackCount;
                int added = State.Avatar.pawn.carryTracker.TryStartCarry(item, originalCount, reserve: false);
                if (added > 0)
                {
                    item.def.soundDrop.PlayOneShot(State.Avatar.pawn);
                }

                if (added < originalCount && !item.Destroyed && item.stackCount > 0)
                {
                    if (cursorItem == null) cursorItem = item;
                    else cursorItem.TryAbsorbStack(item, true);
                }
                if (cursorItem != null && cursorItem.stackCount <= 0) cursorItem = null;
                Close();
            });

            DrawActionButton(equipRect, isApparel ? Wear : Equip, equipLabel, canEquip, (button) =>
            {
                var item = cursorItem;
                cursorItem = null;
                if (!item.Spawned)
                    GenSpawn.Spawn(item, State.Avatar.pawn.Position, State.Avatar.pawn.Map, WipeMode.Vanish);
                if (Avatar.TryMakeWearOrEquipJob(State.Avatar.pawn, item, out Job job))
                {
                    State.Avatar.pawn.jobs.TryTakeOrderedJob(job);
                }
                Close();
            });

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
                        HandleClick(item, Event.current.button, targetCell, i);
                        Event.current.Use();
                    }
                }
            }

            Widgets.EndScrollView();

            if (cursorItem != null && !cursorItem.Destroyed)
            {
                var mouseRect = new Rect(UI.MousePositionOnUIInverted.x - cellSize / 2f, UI.MousePositionOnUIInverted.y - cellSize / 2f, cellSize, cellSize);
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

        private void DrawActionButton(Rect rect, Texture2D icon, string label, bool enabled, Action<int> onClick)
        {
            Widgets.DrawMenuSection(rect);

            Color prev = GUI.color;
            if (!enabled)
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                if (Event.current.type == EventType.MouseUp && (Event.current.button == 0 || Event.current.button == 1))
                {
                    onClick(Event.current.button);
                    Event.current.Use();
                }
            }

            DrawButtonContent(rect, icon, label);
            GUI.color = prev;
        }

        private static void DrawButtonContent(Rect rect, Texture2D icon, string label)
        {
            const float iconSize = 36f;
            const float spacing = 8f;

            Vector2 textSize = Text.CalcSize(label);
            float blockW = iconSize + spacing + textSize.x;
            float startX = rect.x + (rect.width - blockW) / 2f;
            float centerY = rect.y + rect.height / 2f;

            var iconRect = new Rect(startX, centerY - iconSize / 2f, iconSize, iconSize);
            GUI.DrawTexture(iconRect, icon);

            Text.Anchor = TextAnchor.MiddleLeft;
            var textRect = new Rect(iconRect.xMax + spacing, rect.y, textSize.x + 2f, rect.height);
            Widgets.Label(textRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void ClearItemReservations(Thing item)
        {
            if (item == null) return;
            var map = State.Avatar.pawn.Map;
            if (map == null) return;

            var reservers = new HashSet<Pawn>();
            map.reservationManager.ReserversOf(item, reservers);
            foreach (var r in reservers)
            {
                if (r != State.Avatar.pawn && r.jobs != null)
                    r.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            map.reservationManager.ReleaseAllForTarget(item);
            map.physicalInteractionReservationManager.ReleaseAllForTarget(item);
        }

        private void PlaceAndRegister(Thing item, IntVec3 targetCell, int slotIdx)
        {
            GenSpawn.Spawn(item, targetCell, storage.Map, WipeMode.Vanish);

            if (!item.Destroyed)
            {
                slotComp.SetItemSlot(item.thingIDNumber, slotIdx);
                storage.Map.mapDrawer.MapMeshDirty(targetCell, MapMeshFlagDefOf.Things);
            }
        }

        private void HandleClick(Thing clickedItem, int button, IntVec3 targetCell, int slotIdx)
        {
            if (button == 0)
            {
                if (cursorItem == null && clickedItem != null)
                {
                    cursorItem = clickedItem.SplitOff(clickedItem.stackCount);
                    ClearItemReservations(cursorItem);
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
                        PlaceAndRegister(toPlace, targetCell, slotIdx);
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
                            PlaceAndRegister(temp, targetCell, slotIdx);
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
                    ClearItemReservations(cursorItem);
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
                            PlaceAndRegister(single, targetCell, slotIdx);
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
