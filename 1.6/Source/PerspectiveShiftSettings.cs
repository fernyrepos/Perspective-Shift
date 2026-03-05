using Verse;

namespace PerspectiveShift
{
    public class PerspectiveShiftSettings : ModSettings
    {
        public float zoomSpeed = 0.35f;
        public float minZoom = 2f;
        public float maxZoom = 60f;
        public float grabRange = 1.5f;
        public bool disableAimingDelay = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref zoomSpeed, "zoomSpeed", 0.35f);
            Scribe_Values.Look(ref minZoom, "minZoom", 2f);
            Scribe_Values.Look(ref maxZoom, "maxZoom", 60f);
            Scribe_Values.Look(ref grabRange, "grabRange", 1.5f);
            Scribe_Values.Look(ref disableAimingDelay, "disableAimingDelay", true);
        }
    }
}
