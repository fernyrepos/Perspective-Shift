using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.HasJobOnThing))]
    public static class WorkGiver_Scanner_HasJobOnThing_Patch
    {
        public static void Postfix(Pawn pawn, Thing t, ref bool __result)
        {
            if (!__result || pawn.IsAvatar()) return;
            var comp = (t as ThingWithComps)?.GetComp<CompPlayerOnly>();
            if (comp != null && comp.mode == PlayerOnlyMode.Use)
            {
                __result = false;
            }
        }
    }
}
