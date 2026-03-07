using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(CellFinder), nameof(CellFinder.TryFindBestPawnStandCell))]
    public static class CellFinder_TryFindBestPawnStandCell_Patch
    {
        public static bool Prefix(Pawn forPawn, ref bool __result)
        {
            if (forPawn.IsAvatar() && State.Avatar?.physicsPosition != null)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
