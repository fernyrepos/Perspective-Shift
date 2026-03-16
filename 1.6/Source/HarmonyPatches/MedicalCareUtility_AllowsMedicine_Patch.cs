using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
	[HarmonyPatch(typeof(MedicalCareUtility), nameof(MedicalCareUtility.AllowsMedicine))]
	public static class MedicalCareUtility_AllowsMedicine_Patch
	{
		public static void Postfix(MedicalCareCategory cat, ThingDef meds, ref bool __result)
		{
			if (__result) return;

			if (State.IsActive && State.Avatar?.pawn?.CurJob != null)
			{
				var curJob = State.Avatar.pawn.CurJob;
				if (curJob.playerForced && 
					(State.Avatar.pawn.jobs.curDriver is JobDriver_TendPatient) && 
					curJob.targetB.Thing?.def == meds)
				{
					__result = true;
				}
			}
		}
	}
}
