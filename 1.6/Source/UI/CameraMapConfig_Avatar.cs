using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    public class CameraMapConfig_Avatar : CameraMapConfig_Normal
    {
        public CameraMapConfig_Avatar()
        {
            dollyRateKeys = 0f;
            dollyRateScreenEdge = 0f;
            sizeRange = new FloatRange(
                Mathf.Max(PerspectiveShiftMod.settings?.minZoom ?? 11f, 11f),
                PerspectiveShiftMod.settings?.maxZoom ?? 60f
            );
        }
    }
}
