using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace PerspectiveShift
{
	[HarmonyPatch]
	public static class GenClosest_MaxDistance_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global));
			yield return AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
			yield return AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Regionwise_ReachablePrioritized));
			yield return AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable));
		}

		public static void Prefix(ref float maxDistance)
		{
			if (Avatar.IsAvatarLeftClick)
				maxDistance = PerspectiveShiftMod.settings.grabRange + 1.5f;
		}
	}
}
