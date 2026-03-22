using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Verse.AI;

namespace PerspectiveShift
{
	[HarmonyPatch]
	public static class Pawn_PathFollower_Moving_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.PropertyGetter(typeof(Pawn_PathFollower), "Moving");
			yield return AccessTools.PropertyGetter(typeof(Pawn_PathFollower), "MovingNow");
		}

		public static void Postfix(Pawn_PathFollower __instance, ref bool __result)
		{
			if (!__result && __instance.pawn.IsAvatar() && State.Avatar?.IsMoving == true)
				__result = true;
		}
	}
}
