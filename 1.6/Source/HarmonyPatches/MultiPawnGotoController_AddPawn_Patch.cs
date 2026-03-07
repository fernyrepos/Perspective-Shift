using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.AddPawn))]
    public static class MultiPawnGotoController_AddPawn_Patch
    {
        public static bool Prefix(Pawn pawn)
        {
            if (pawn.IsAvatar()) return false;
            return true;
        }
    }
}
