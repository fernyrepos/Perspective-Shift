using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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
            yield return AccessTools.Method(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints), "JobOnThing");
            yield return AccessTools.Method(typeof(WorkGiver_ConstructDeliverResourcesToFrames), "JobOnThing");
            yield return AccessTools.Method(typeof(JobGiver_Wander), "TryGiveJob");
            yield return AccessTools.Method(typeof(JobGiver_ExitMapBest), "TryGiveJob");
            yield return AccessTools.Method(typeof(JobGiver_ExitMap), "TryGiveJob");
        }

        public static bool Prefix(Pawn pawn, ref Job __result, MethodBase __originalMethod)
        {
            if (pawn.IsAvatar() && !pawn.InMentalState)
            {
                if (State.Avatar.seekAtWill && (__originalMethod.DeclaringType == typeof(JobGiver_AIFightEnemy) || __originalMethod.DeclaringType == typeof(JobGiver_ConfigurableHostilityResponse)))
                {
                    return true;
                }
                if (!pawn.Drafted && (pawn.GetLord() != null || pawn.mindState?.duty != null))
                {
                    return true;
                }
                __result = null;
                return false;
            }
            return true;
        }
    }
}
