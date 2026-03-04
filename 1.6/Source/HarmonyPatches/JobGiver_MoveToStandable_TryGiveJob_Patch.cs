using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
	[HarmonyPatch(typeof(JobGiver_MoveToStandable), "TryGiveJob")]
	public static class JobGiver_MoveToStandable_TryGiveJob_Patch
	{
		public static bool Prefix(Pawn pawn, ref Job __result)
		{
			if (!pawn.IsAvatar()) return true;

			if (pawn.pather.Moving) return false;

			if (!pawn.Position.Standable(pawn.Map))
			{
				var dest = RCellFinder.BestOrderedGotoDestNear(pawn.Position, pawn);
				if (dest.IsValid && dest != pawn.Position)
					__result = JobMaker.MakeJob(JobDefOf.Goto, dest);
			}

			return false;
		}
	}
}
