using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor))]
    public static class StoreUtility_TryFindBestBetterNonSlotGroupStorageFor_Patch
    {
        public static bool Prefix(Pawn carrier, ref IHaulDestination haulDestination, ref bool __result)
        {
            if (carrier != null && carrier.IsAvatar() && !carrier.InMentalState && !Avatar.IsAvatarLeftClick)
            {
                haulDestination = null;
                __result = false;
                return false;
            }
            return true;
        }
    }
}
