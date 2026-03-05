using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

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

			Vector3 toMouse = UI.MouseMapPosition() - pawn.DrawPos;
			toMouse.y = 0f;
			if (toMouse.sqrMagnitude < 0.01f) return true;

			float aimAngle = Mathf.Atan2(toMouse.x, toMouse.z) * Mathf.Rad2Deg;
			if (aimAngle < 0f) aimAngle += 360f;

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
