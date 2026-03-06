using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch]
    public static class JobGivers_DisableForAvatar_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(JobGiver_AIFightEnemy), "TryGiveJob");
            yield return AccessTools.Method(typeof(JobGiver_ConfigurableHostilityResponse), "TryGiveJob");
            yield return AccessTools.Method(typeof(JobGiver_GetFood), "TryGiveJob");
        }

        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn.IsAvatar())
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}
