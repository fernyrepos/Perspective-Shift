using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.PanToMapLoc), new[] { typeof(IntVec3) })]
    public static class CameraDriver_PanToMapLoc_Patch
    {
        public static void Postfix(IntVec3 cell)
        {
            if (!State.IsActive) return;
            if (State.Avatar.pawn.Map != Find.CurrentMap) return;
            if (cell == State.Avatar.pawn.Position)
            {
                State.CameraLockPosition = null;
                return;
            }
            State.CameraLockPosition = cell.ToVector3Shifted();
        }
    }
}
