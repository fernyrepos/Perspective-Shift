using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Pawn_SpawnSetup_Patch
    {
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (!State.IsActive || respawningAfterLoad || __instance != State.Avatar.pawn) return;
            if (!__instance.Spawned || __instance.Map == null || __instance.Map == Current.Game.CurrentMap) return;

            Current.Game.CurrentMap = __instance.Map;
            State.Avatar.physicsPosition = __instance.Position.ToVector3ShiftedWithAltitude(__instance.def.Altitude);
            State.CameraLockPosition = null;
        }
    }
}
