using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace PerspectiveShift
{
	[HarmonyPatch]
	public static class JobDriver_ManTurret_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(JobDriver_ManTurret), nameof(JobDriver_ManTurret.GunNeedsLoading));
			yield return AccessTools.Method(typeof(JobDriver_ManTurret), nameof(JobDriver_ManTurret.GunNeedsRefueling));
		}

		public static bool Prefix(Building b, ref bool __result)
		{
			if (State.IsActive && State.Avatar?.pawn?.CurJob != null &&
				State.Avatar.pawn.CurJob.def == JobDefOf.ManTurret &&
				State.Avatar.pawn.CurJob.targetA.Thing == b)
			{
				__result = false;
				return false;
			}
			return true;
		}
	}
}
