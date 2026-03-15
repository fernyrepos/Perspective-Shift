using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(ScenPart_StartingAnimal), nameof(ScenPart_StartingAnimal.PlayerStartingThings))]
    public static class ScenPart_StartingAnimal_PlayerStartingThings_Patch
    {
        public static Dictionary<ScenPart_StartingAnimal, List<Thing>> cachedAnimals = new Dictionary<ScenPart_StartingAnimal, List<Thing>>();
        public static ScenPart_StartingAnimal bypassPart = null;

        public static bool Prefix(ScenPart_StartingAnimal __instance, ref IEnumerable<Thing> __result)
        {
            if (__instance == bypassPart) return true;
            if (cachedAnimals.TryGetValue(__instance, out var list))
            {
                __result = list;
                return false;
            }
            return true;
        }

        public static void ClearCache() => cachedAnimals.Clear();
    }
}
