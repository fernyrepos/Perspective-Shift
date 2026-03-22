using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(WorkGiver_CleanFilth), nameof(WorkGiver_CleanFilth.HasJobOnThing))]
    public static class WorkGiver_CleanFilth_HasJobOnThing_Patch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn.IsAvatar() && Avatar.IsAvatarLeftClick is false)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
