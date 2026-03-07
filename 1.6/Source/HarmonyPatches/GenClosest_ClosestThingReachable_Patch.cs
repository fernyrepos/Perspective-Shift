using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable))]
    public static class GenClosest_ClosestThingReachable_Patch
    {
        public static void Prefix(ref float maxDistance)
        {
            if (Avatar.IsAvatarLeftClick)
            {
                maxDistance = PerspectiveShiftMod.settings.grabRange + 1.5f;
            }
        }
    }
}
