using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
	[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThing_Regionwise_ReachablePrioritized))]
	public static class GenClosest_ClosestThing_Regionwise_ReachablePrioritized_Patch
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
