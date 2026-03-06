using Verse;

namespace PerspectiveShift
{
    public enum GizmoCorner
    {
        TopRight,
        BottomRight,
        BottomLeft,
        TopLeft
    }

    public class PerspectiveShiftSettings : ModSettings
    {
        public float zoomSpeed = 0.35f;
        public float minZoom = 2f;
        public float maxZoom = 60f;
        public float grabRange = 1.5f;
        public bool disableAimingDelay = true;

        public float moveSpeedMultiplier = 1.0f;
        public float shootAccuracyMultiplier = 1.0f;
        public GizmoCorner gizmoCorner = GizmoCorner.TopRight;
        public bool totalFreedom = false;
        public bool allowNonHuman = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref zoomSpeed, "zoomSpeed", 0.35f);
            Scribe_Values.Look(ref minZoom, "minZoom", 2f);
            Scribe_Values.Look(ref maxZoom, "maxZoom", 60f);
            Scribe_Values.Look(ref grabRange, "grabRange", 1.5f);
            Scribe_Values.Look(ref disableAimingDelay, "disableAimingDelay", true);
            Scribe_Values.Look(ref moveSpeedMultiplier, "moveSpeedMultiplier", 1.0f);
            Scribe_Values.Look(ref shootAccuracyMultiplier, "shootAccuracyMultiplier", 1.0f);
            Scribe_Values.Look(ref gizmoCorner, "gizmoCorner", GizmoCorner.TopRight);
            Scribe_Values.Look(ref totalFreedom, "totalFreedom", false);
            Scribe_Values.Look(ref allowNonHuman, "allowNonHuman", false);
        }
    }
}
