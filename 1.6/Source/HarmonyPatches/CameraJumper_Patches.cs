using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(CameraJumper))]
    public static class CameraJumper_Patches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(CameraJumper.TryJumpInternal), typeof(IntVec3), typeof(Map), typeof(CameraJumper.MovementMode))]
        public static bool TryJumpInternal_Prefix(Map map)
        {
            if (!State.MapViewBlocked(map)) return true;
            State.NotifyMapViewBlocked();
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CameraJumper.TrySelectInternal), typeof(Thing))]
        public static bool TrySelectInternal_Prefix(Thing thing)
        {
            if (!State.MapViewBlocked(thing.Map)) return true;
            State.NotifyMapViewBlocked();
            return false;
        }
    }
}
