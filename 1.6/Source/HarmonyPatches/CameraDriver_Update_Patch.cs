using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.Update))]
    public static class CameraDriver_Update_Patch
    {
        public static void Postfix()
        {
            if (State.IsActive && Find.CurrentMap != null)
            {
                State.Avatar.UpdateCamera();
            }
        }
    }
}
