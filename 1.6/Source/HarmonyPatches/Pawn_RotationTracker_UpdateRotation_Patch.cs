using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_RotationTracker), nameof(Pawn_RotationTracker.UpdateRotation))]
    public static class Pawn_RotationTracker_UpdateRotation_Patch
    {
        public static bool Prefix(Pawn_RotationTracker __instance)
        {
            if (!__instance.pawn.IsAvatar()) return true;

            if (State.Avatar?.IsMoving == false)
            {
                if (__instance.pawn.Drafted && !__instance.pawn.InMentalState && !State.ControlsFrozen)
                {
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
