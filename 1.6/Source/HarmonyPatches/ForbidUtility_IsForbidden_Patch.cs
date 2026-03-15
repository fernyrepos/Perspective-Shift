using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new[] { typeof(Thing), typeof(Pawn) })]
    public static class ForbidUtility_IsForbidden_Patch
    {
        public static void Postfix(Thing t, Pawn pawn, ref bool __result)
        {
            if (__result || pawn.IsAvatar()) return;
            var slotGroup = t.GetSlotGroup();
            var comp = (slotGroup?.parent as ThingWithComps)?.GetComp<CompPlayerOnly>();
            if (comp != null && (comp.mode == PlayerOnlyMode.Take || comp.mode == PlayerOnlyMode.Use))
            {
                __result = true;
            }
        }
    }
}
