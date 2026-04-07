using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch]
    public static class InteractionWorkers_DisableForAvatar_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight));
            yield return AccessTools.Method(typeof(InteractionWorker_Insult), nameof(InteractionWorker_Insult.RandomSelectionWeight));
        }

        public static void Postfix(Pawn initiator, ref float __result)
        {
            if (initiator.IsAvatar())
            {
                __result = 0f;
            }
        }
    }
}
