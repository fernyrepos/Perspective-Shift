using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
	[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.CarryWeaponOpenly))]
	public static class PawnRenderUtility_CarryWeaponOpenly_Patch
	{
		public static void Postfix(Pawn pawn, ref bool __result)
		{
			if (__result) return;
			if (pawn.ShouldSeekEnemy())
			{
				__result = true;
			}
		}
	}
}
