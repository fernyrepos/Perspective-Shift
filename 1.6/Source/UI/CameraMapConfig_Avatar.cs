using Verse;

namespace PerspectiveShift
{
    public class CameraMapConfig_Avatar : CameraMapConfig_Normal
    {
        public CameraMapConfig_Avatar()
        {
            dollyRateKeys = 0f;
            dollyRateScreenEdge = 0f;
            sizeRange = ModCompatibility.GetCameraSizeRange();
            zoomSpeed = PerspectiveShiftMod.settings.zoomSpeed * 10f;
        }
    }
}
