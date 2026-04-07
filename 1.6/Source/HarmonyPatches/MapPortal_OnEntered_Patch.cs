using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MapPortal), nameof(MapPortal.OnEntered))]
    public static class MapPortal_OnEntered_Patch
    {
        public static void Postfix(Pawn pawn)
        {
            if (!State.IsActive || pawn != State.Avatar.pawn) return;
            Current.Game.CurrentMap = pawn.Map;
        }
    }
}
