using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PerspectiveShift
{
	[HotSwappable]
	[HarmonyPatch(typeof(JobGiver_Orders), "TryGiveJob")]
	public static class JobGiver_Orders_TryGiveJob_Patch
	{
		public static void Postfix(Pawn pawn, ref Job __result)
		{
			if (__result != null || !pawn.IsAvatar()) return;
			if (pawn.InMentalState) return;

			if (pawn.mindState?.duty != null) return;
			if (pawn.GetLord() != null) return;
			if (HasVoluntarilyJoinableLord(pawn)) return;

			var wait = JobMaker.MakeJob(JobDefOf.Wait);
			wait.expiryInterval = 60;
			wait.checkOverrideOnExpire = true;
			__result = wait;
		}

		private static bool HasVoluntarilyJoinableLord(Pawn pawn)
		{
			if (pawn.Map == null) return false;
			foreach (var lord in pawn.Map.lordManager.lords)
			{
				if (lord.LordJob is LordJob_VoluntarilyJoinable joinable
					&& !lord.ownedPawns.Contains(pawn)
					&& joinable.VoluntaryJoinPriorityFor(pawn) > 0f)
					return true;
			}
			return false;
		}
	}
}
