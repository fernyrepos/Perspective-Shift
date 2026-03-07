using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStoreCellFor))]
    public static class StoreUtility_TryFindBestBetterStoreCellFor_Patch
    {
        public static bool Prefix(Pawn carrier, ref IntVec3 foundCell, ref bool __result)
        {
            if (carrier != null && carrier.IsAvatar() && !carrier.InMentalState && !Avatar.IsAvatarLeftClick)
            {
                foundCell = IntVec3.Invalid;
                __result = false;
                return false;
            }
            return true;
        }
    }
}
