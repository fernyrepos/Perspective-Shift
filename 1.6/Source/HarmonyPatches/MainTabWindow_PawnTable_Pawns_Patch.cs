using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MainTabWindow_PawnTable), "Pawns", MethodType.Getter)]
    public static class MainTabWindow_PawnTable_Pawns_Patch
    {
        public static void Postfix( MainTabWindow_PawnTable __instance, ref IEnumerable<Pawn> __result)
        {
        	if (__instance is MainTabWindow_Work or MainTabWindow_Schedule or MainTabWindow_Assign)
            {
                __result = __result.Where(p => !p.IsAvatar());
            }
        }
    }
}
