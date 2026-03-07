using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
	[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable))]
	public static class GenClosest_ClosestThing_Global_Reachable_Patch
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
