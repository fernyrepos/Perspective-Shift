using System;
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
            try
            {
                State.ClearAvatar();
                State.seekAtWillPawns = new HashSet<int>();
                State.fulfillNeeds = false;
                State.returnAfterFulfillment = true;
                State.showJobCallouts = true;
                State.playDoneSound = true;
                State.cameraRecenterFrames = 0;
                State.cameraRecenterSettled = -1;
                State.cameraRecenterSnap = false;
                State.disabledAutoNeeds = new HashSet<AutoNeed>();
                State.autoNeedThresholds = new Dictionary<AutoNeed, float>();
                State.pendingDeathMenu = false;
                State.permadeath = false;
                State.allowDirectorInAuthentic = false;
                State.authenticPawnId = -1;
                ModCompatibility.ClearCaches();
                ScenPart_StartingAnimal_PlayerStartingThings_Patch.ClearCache();
            }
            catch (Exception ex)
            {
                State.Error($"Pre-load cleanup failed, continuing load: {ex}");
            }
        }
    }
}
