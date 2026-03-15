using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class CompProperties_StorageSlotOrder : CompProperties
    {
        public CompProperties_StorageSlotOrder() => compClass = typeof(CompStorageSlotOrder);
    }

    public class CompStorageSlotOrder : ThingComp
    {
        private Dictionary<int, int> thingIDToSlot = new Dictionary<int, int>();
        private HashSet<int> gapSlots = new HashSet<int>();

        public void SetItemSlot(int thingID, int slotIdx)
        {
            var prev = thingIDToSlot.Where(kv => kv.Value == slotIdx && kv.Key != thingID).ToList();
            foreach (var kv in prev) thingIDToSlot.Remove(kv.Key);

            if (thingIDToSlot.TryGetValue(thingID, out int oldSlot) && oldSlot != slotIdx)
                thingIDToSlot.Remove(thingID);

            thingIDToSlot[thingID] = slotIdx;
            gapSlots.Remove(slotIdx);
        }

        public void AddGap(int slotIdx) => gapSlots.Add(slotIdx);
        public void RemoveGap(int slotIdx) => gapSlots.Remove(slotIdx);
        public bool IsGap(int slotIdx) => gapSlots.Contains(slotIdx);

        public void ReconcileStaleEntries(Building_Storage storage)
        {
            var allCells = storage.AllSlotCellsList();
            int maxPerCell = storage.def.building.maxItemsInCell;
            for (int cellIdx = 0; cellIdx < allCells.Count; cellIdx++)
            {
                var realIDs = new HashSet<int>(
                    allCells[cellIdx].GetThingList(storage.Map)
                        .Where(t => t.def.category == ThingCategory.Item)
                        .Select(t => t.thingIDNumber));
                int slotBase = cellIdx * maxPerCell;
                var stale = thingIDToSlot
                    .Where(kv => kv.Value >= slotBase && kv.Value < slotBase + maxPerCell
                                 && !realIDs.Contains(kv.Key))
                    .ToList();
                foreach (var kv in stale)
                {
                    gapSlots.Add(kv.Value);
                    thingIDToSlot.Remove(kv.Key);
                }
            }
        }

        public Thing[] BuildSlotArray(Building_Storage storage)
        {
            var allCells = storage.AllSlotCellsList();
            int maxPerCell = storage.def.building.maxItemsInCell;
            int capacity = allCells.Count * maxPerCell;
            var result = new Thing[capacity];

            for (int cellIdx = 0; cellIdx < allCells.Count; cellIdx++)
            {
                var realItems = allCells[cellIdx].GetThingList(storage.Map)
                    .Where(t => t.def.category == ThingCategory.Item).ToList();
                int slotBase = cellIdx * maxPerCell;

                var unregistered = new List<Thing>();
                foreach (var item in realItems)
                {
                    if (thingIDToSlot.TryGetValue(item.thingIDNumber, out int slot)
                        && slot >= slotBase && slot < slotBase + maxPerCell)
                        result[slot] = item;
                    else
                        unregistered.Add(item);
                }

                int unregPtr = 0;
                for (int pass = 0; pass < 2 && unregPtr < unregistered.Count; pass++)
                for (int sub = 0; sub < maxPerCell && unregPtr < unregistered.Count; sub++)
                {
                    int idx = slotBase + sub;
                    bool isFreeNonGap = result[idx] == null && !gapSlots.Contains(idx);
                    bool isFreeGap    = result[idx] == null && gapSlots.Contains(idx);
                    if ((pass == 0 && isFreeNonGap) || (pass == 1 && isFreeGap))
                    {
                        var item = unregistered[unregPtr++];
                        result[idx] = item;
                        thingIDToSlot[item.thingIDNumber] = idx;
                        gapSlots.Remove(idx);
                    }
                }

                ReorderCellThingGrid(storage, allCells[cellIdx], cellIdx);
            }

            return result;
        }

        public void ReorderCellThingGrid(Building_Storage storage, IntVec3 cell, int cellIdx)
        {
            var list = cell.GetThingList(storage.Map);
            var items = list.Where(t => t.def.category == ThingCategory.Item).ToList();

            var registered = items
                .Where(t => thingIDToSlot.ContainsKey(t.thingIDNumber))
                .OrderBy(t => thingIDToSlot[t.thingIDNumber])
                .ToList();
            var unregistered = items
                .Where(t => !thingIDToSlot.ContainsKey(t.thingIDNumber))
                .ToList();

            var ordered = registered.Concat(unregistered).ToList();

            int writeIdx = 0;
            for (int i = 0; i < list.Count && writeIdx < ordered.Count; i++)
                if (list[i].def.category == ThingCategory.Item)
                    list[i] = ordered[writeIdx++];
        }

        public override void PostExposeData()
        {
            var keys = thingIDToSlot?.Keys.ToList() ?? new List<int>();
            var vals = thingIDToSlot?.Values.ToList() ?? new List<int>();
            Scribe_Collections.Look(ref keys, "thingIDKeys", LookMode.Value);
            Scribe_Collections.Look(ref vals, "thingIDVals", LookMode.Value);

            var gaps = gapSlots?.ToList() ?? new List<int>();
            Scribe_Collections.Look(ref gaps, "gapSlots", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                thingIDToSlot = new Dictionary<int, int>();
                if (keys != null && vals != null)
                    for (int i = 0; i < keys.Count && i < vals.Count; i++)
                        thingIDToSlot[keys[i]] = vals[i];
                gapSlots = gaps != null ? new HashSet<int>(gaps) : new HashSet<int>();
            }
        }
    }
}
