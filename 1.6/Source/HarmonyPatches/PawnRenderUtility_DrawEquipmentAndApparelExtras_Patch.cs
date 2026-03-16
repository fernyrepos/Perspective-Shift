using HarmonyLib;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
	[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
	public static class PawnRenderUtility_DrawEquipmentAndApparelExtras_Patch
	{
		public static bool Prefix(Pawn pawn, Vector3 drawPos, Rot4 facing, PawnRenderFlags flags)
		{
			if (!pawn.IsAvatar()) return true;

			if (!pawn.Drafted) return true;
			if (pawn.InMentalState) return true;
			if (State.ControlsFrozen) return true;
			if (pawn.stances.curStance is Stance_Busy) return true;
			if (pawn.equipment?.Primary == null) return true;
			if (pawn.CurJob?.def?.neverShowWeapon == true) return true;

			float aimAngle = State.Avatar.aimAngle;
			if (aimAngle < 0f) aimAngle = pawn.Rotation.AsAngle;

			float equipmentDrawDistanceFactor = pawn.ageTracker.CurLifeStage.equipmentDrawDistanceFactor;
			Vector3 weaponDrawPos = drawPos + new Vector3(0f, 0f, 0.4f + pawn.equipment.Primary.def.equippedDistanceOffset)
				.RotatedBy(aimAngle) * equipmentDrawDistanceFactor;

			PawnRenderUtility.DrawEquipmentAiming(pawn.equipment.Primary, weaponDrawPos, aimAngle);

			if (pawn.apparel != null)
				foreach (var apparel in pawn.apparel.WornApparel)
					apparel.DrawWornExtras();

			return false;
		}
	}
}
