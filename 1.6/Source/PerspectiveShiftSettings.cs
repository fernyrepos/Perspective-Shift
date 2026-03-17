using System.Collections.Generic;
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
        public float minZoom = 1f;
        public float maxZoom = 40f;
        public float grabRange = 1.5f;
        public bool disableAimingDelay = true;

        public bool enableSprinting = true;
        public bool enableSneaking = true;
        public float sprintSpeedMultiplier = 1.3f;
        public float sneakSpeedMultiplier = 0.5f;

        public float moveSpeedMultiplier = 1.0f;
        public float shootAccuracyMultiplier = 1.0f;
        public GizmoCorner gizmoCorner = GizmoCorner.TopRight;
        public bool totalFreedom = false;
        public bool allowNonHuman = false;
        public bool requirePawnInFaction = true;
        public float sprintFoodDrain = 1.5f;
        public float sprintSleepDrain = 2f;
        public bool disableCustomGizmos = false;
        public List<string> pinnedNeeds = new List<string>() { "Mood", "Food", "Rest" };

        public override void ExposeData()
        {
            Scribe_Values.Look(ref zoomSpeed, "zoomSpeed", 0.35f);
            Scribe_Values.Look(ref minZoom, "minZoom", 1f);
            Scribe_Values.Look(ref maxZoom, "maxZoom", 40f);
            Scribe_Values.Look(ref grabRange, "grabRange", 1.5f);
            Scribe_Values.Look(ref disableAimingDelay, "disableAimingDelay", true);

            Scribe_Values.Look(ref enableSprinting, "enableSprinting", true);
            Scribe_Values.Look(ref enableSneaking, "enableSneaking", true);
            Scribe_Values.Look(ref sprintSpeedMultiplier, "sprintSpeedMultiplier", 1.3f);
            Scribe_Values.Look(ref sneakSpeedMultiplier, "sneakSpeedMultiplier", 0.5f);

            Scribe_Values.Look(ref moveSpeedMultiplier, "moveSpeedMultiplier", 1.0f);
            Scribe_Values.Look(ref shootAccuracyMultiplier, "shootAccuracyMultiplier", 1.0f);
            Scribe_Values.Look(ref gizmoCorner, "gizmoCorner", GizmoCorner.TopRight);
            Scribe_Values.Look(ref totalFreedom, "totalFreedom", false);
            Scribe_Values.Look(ref allowNonHuman, "allowNonHuman", false);
            Scribe_Values.Look(ref requirePawnInFaction, "requirePawnInFaction", true);
            Scribe_Values.Look(ref sprintFoodDrain, "sprintFoodDrain", 1.5f);
            Scribe_Values.Look(ref sprintSleepDrain, "sprintSleepDrain", 2f);
            Scribe_Values.Look(ref disableCustomGizmos, "disableCustomGizmos", false);
            Scribe_Collections.Look(ref pinnedNeeds, "pinnedNeeds", LookMode.Value);

            if (pinnedNeeds == null)
            {
                pinnedNeeds = new List<string>() { "Mood", "Food", "Rest" };
            }
        }
    }
}
