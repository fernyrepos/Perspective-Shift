using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaul))]
    public static class HaulAIUtility_PawnCanAutomaticallyHaul_Patch
    {
        public static bool Prefix(Pawn p, Thing t, bool forced, ref bool __result)
        {
            if (p.IsAvatar())
            {
                float grabRange = PerspectiveShiftMod.settings.grabRange + 1.5f;
                bool inRange = p.Position.DistanceTo(t.Position) <= grabRange;
                __result = inRange;
                return false;
            }
            if (!forced)
            {
                var slotGroup = StoreUtility.GetSlotGroup(t);
                var comp = (slotGroup?.parent as ThingWithComps)?.GetComp<CompPlayerOnly>();
                if (comp != null && comp.mode == PlayerOnlyMode.Store)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }
}
