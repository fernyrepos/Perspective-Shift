using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MapPortal), nameof(MapPortal.Notify_ThingAdded))]
    public static class MapPortal_Notify_ThingAdded_Patch
    {
        public static void Postfix(Thing t)
        {
            if (t is not Pawn pawn ||!State.IsActive || pawn != State.Avatar.pawn) return;

            Current.Game.CurrentMap = pawn.Map;
            State.Avatar.physicsPosition = pawn.Position.ToVector3ShiftedWithAltitude(pawn.def.Altitude);
            State.CameraLockPosition = null;
        }
    }
}
