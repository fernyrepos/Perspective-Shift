using HarmonyLib;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public static class JobDriver_Wait_CheckForAutoAttack_Patch
    {
        public static bool Prefix(JobDriver_Wait __instance)
        {
            if (__instance.pawn.IsAvatar()) return false;
            return true;
        }
    }
}
