using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell))]
    public static class StoreUtility_IsGoodStoreCell_Patch
    {
        public static void Postfix(IntVec3 c, Map map, Pawn carrier, ref bool __result)
        {
            if (!__result || carrier == null || carrier.IsAvatar()) return;
            var parent = c.GetSlotGroup(map)?.parent as ThingWithComps;
            var comp = parent?.GetComp<CompPlayerOnly>();
            if (comp != null && (comp.mode == PlayerOnlyMode.Store || comp.mode == PlayerOnlyMode.Use))
            {
                __result = false;
            }
        }
    }
}
