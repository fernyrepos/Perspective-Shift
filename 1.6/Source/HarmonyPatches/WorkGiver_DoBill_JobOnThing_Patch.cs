using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    public static class WorkGiver_DoBill_JobOnThing_Patch
    {
        public static void Prefix(Pawn pawn, Thing thing, out Dictionary<Bill, float> __state)
        {
            __state = null;
            if (Avatar.IsAvatarLeftClick && pawn.IsAvatar())
            {
                if (thing is IBillGiver billGiver)
                {
                    __state = new Dictionary<Bill, float>();
                    foreach (var bill in billGiver.BillStack)
                    {
                        __state[bill] = bill.ingredientSearchRadius;
                        bill.ingredientSearchRadius = PerspectiveShiftMod.settings.grabRange + 1.5f;
                    }
                }
            }
        }

        public static void Postfix(Thing thing, Dictionary<Bill, float> __state)
        {
            if (__state != null && thing is IBillGiver billGiver)
            {
                foreach (var bill in billGiver.BillStack)
                {
                    if (__state.TryGetValue(bill, out float rad))
                    {
                        bill.ingredientSearchRadius = rad;
                    }
                }
            }
        }
    }
}
