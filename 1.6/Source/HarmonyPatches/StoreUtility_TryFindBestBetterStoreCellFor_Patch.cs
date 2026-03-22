using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace PerspectiveShift
{
	[HarmonyPatch]
	public static class StoreUtility_TryFindBestBetterStoreCellFor_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStoreCellFor));
			yield return AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStoreCellForIn));
		}

		public static bool Prefix(Pawn carrier, ref IntVec3 foundCell, ref bool __result)
		{
			if (carrier != null && carrier.IsAvatar() && !carrier.InMentalState && !Avatar.IsAvatarLeftClick)
			{
				foundCell = IntVec3.Invalid;
				__result = false;
				return false;
			}
			return true;
		}
	}
}
