using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.GetPriority))]
    public static class Pawn_WorkSettings_GetPriority_Patch
    {
        public static void Postfix(Pawn_WorkSettings __instance, WorkTypeDef w, ref int __result)
        {
            if (State.Avatar?.pawn == __instance.pawn && !__instance.pawn.WorkTypeIsDisabled(w))
            {
                __result = 3;
            }
        }
    }
}
