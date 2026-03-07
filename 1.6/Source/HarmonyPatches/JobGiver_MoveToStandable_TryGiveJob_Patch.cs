using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
	[HarmonyPatch(typeof(JobGiver_MoveToStandable), "TryGiveJob")]
	public static class JobGiver_MoveToStandable_TryGiveJob_Patch
	{
		public static bool Prefix(Pawn pawn)
		{
			if (!pawn.IsAvatar()) return true;
			return false;
		}
	}
}
