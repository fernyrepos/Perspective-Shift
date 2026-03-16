using System.Collections.Generic;
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
            State.seekAtWillPawns = new HashSet<int>();
            State.pendingDeathMenu = false;
            State.permadeath = false;
            State.allowDirectorInAuthentic = false;
            ModCompatibility.ClearCaches();
            ScenPart_StartingAnimal_PlayerStartingThings_Patch.ClearCache();
        }
    }
}
