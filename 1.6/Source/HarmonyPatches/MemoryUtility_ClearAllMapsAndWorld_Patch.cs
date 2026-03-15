using HarmonyLib;
using Verse.Profile;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    public static class MemoryUtility_ClearAllMapsAndWorld_Patch
    {
        public static void Prefix()
        {
            State.ClearAvatar();
            ModCompatibility.ClearCaches();
            ScenPart_StartingAnimal_PlayerStartingThings_Patch.ClearCache();
        }
    }
}
