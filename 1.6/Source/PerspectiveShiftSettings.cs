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
        public bool holdToFire = true;
        public bool cameraEasing = true;
        public bool requireHeldClickForJobs = true;
        public bool disableDoubleClickEquip = false;
        public bool disableDoubleClickEat = false;

        public bool enableSprinting = true;
        public bool enableSneaking = true;
        public float sprintSpeedMultiplier = 1.3f;
        public float sneakSpeedMultiplier = 0.5f;

        public float moveSpeedMultiplier = 1.0f;
        public float workSpeedMultiplier = 1.0f;
        public float shootAccuracyMultiplier = 1.0f;
        public float playerMoveSpeedCap = 0.8f;
        public GizmoCorner gizmoCorner = GizmoCorner.TopRight;
        public bool totalFreedom = false;
        public bool allowNonHuman = false;
        public bool requirePawnInFaction = true;
        public float sprintFoodDrain = 1.5f;
        public float sprintSleepDrain = 2f;
        public bool disableCustomGizmos = false;
        public List<string> pinnedNeeds = new List<string>() { "Mood", "Food", "Rest", "Joy" };
        public bool enableDamageScreenEffect = true;
        public bool showControlsOnFirstInhabit = true;
        public bool sleepingPreventsVision = true;
        public float avatarUIScale = 1f;
        public bool disallowOtherMapsInAuthentic = false;
        public bool weaponTooltips = true;
        public bool apparelTooltips = true;
        public bool eatTooltips = true;
        public bool harvestTooltips = true;
        public bool haulingCursor = false;
        public bool mineCursor = true;
        public bool buildCursor = true;
        public bool chopCursor = true;
        public bool harvestCursor = true;
        public bool sleepCursor = true;
        public bool researchCursor = true;
        public bool roofCursor = true;
        public bool fishingMinigame = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref zoomSpeed, "zoomSpeed", 0.35f);
            Scribe_Values.Look(ref minZoom, "minZoom", 1f);
            Scribe_Values.Look(ref maxZoom, "maxZoom", 40f);
            Scribe_Values.Look(ref grabRange, "grabRange", 1.5f);
            Scribe_Values.Look(ref disableAimingDelay, "disableAimingDelay", true);
            Scribe_Values.Look(ref holdToFire, "holdToFire", true);
            Scribe_Values.Look(ref cameraEasing, "cameraEasing", true);
            Scribe_Values.Look(ref requireHeldClickForJobs, "requireHeldClickForJobs", true);
            Scribe_Values.Look(ref disableDoubleClickEquip, "disableDoubleClickEquip", false);
            Scribe_Values.Look(ref disableDoubleClickEat, "disableDoubleClickEat", false);

            Scribe_Values.Look(ref showControlsOnFirstInhabit, "showControlsOnFirstInhabit", true);
            Scribe_Values.Look(ref sleepingPreventsVision, "sleepingPreventsVision", true);
            Scribe_Values.Look(ref avatarUIScale, "avatarUIScale", 1f);
            Scribe_Values.Look(ref disallowOtherMapsInAuthentic, "disallowOtherMapsInAuthentic", false);
            Scribe_Values.Look(ref weaponTooltips, "weaponTooltips", true);
            Scribe_Values.Look(ref apparelTooltips, "apparelTooltips", true);
            Scribe_Values.Look(ref eatTooltips, "eatTooltips", true);
            Scribe_Values.Look(ref harvestTooltips, "harvestTooltips", true);
            Scribe_Values.Look(ref haulingCursor, "haulingCursor", false);
            Scribe_Values.Look(ref mineCursor, "mineCursor", true);
            Scribe_Values.Look(ref buildCursor, "buildCursor", true);
            Scribe_Values.Look(ref chopCursor, "chopCursor", true);
            Scribe_Values.Look(ref harvestCursor, "harvestCursor", true);
            Scribe_Values.Look(ref sleepCursor, "sleepCursor", true);
            Scribe_Values.Look(ref researchCursor, "researchCursor", true);
            Scribe_Values.Look(ref roofCursor, "roofCursor", true);
            Scribe_Values.Look(ref fishingMinigame, "fishingMinigame", true);
            Scribe_Values.Look(ref enableSprinting, "enableSprinting", true);
            Scribe_Values.Look(ref enableSneaking, "enableSneaking", true);
            Scribe_Values.Look(ref sprintSpeedMultiplier, "sprintSpeedMultiplier", 1.3f);
            Scribe_Values.Look(ref sneakSpeedMultiplier, "sneakSpeedMultiplier", 0.5f);

            Scribe_Values.Look(ref moveSpeedMultiplier, "moveSpeedMultiplier", 1.0f);
            Scribe_Values.Look(ref workSpeedMultiplier, "workSpeedMultiplier", 1.0f);
            Scribe_Values.Look(ref shootAccuracyMultiplier, "shootAccuracyMultiplier", 1.0f);
            Scribe_Values.Look(ref playerMoveSpeedCap, "playerMoveSpeedCap", 0.8f);
            Scribe_Values.Look(ref gizmoCorner, "gizmoCorner", GizmoCorner.TopRight);
            Scribe_Values.Look(ref totalFreedom, "totalFreedom", false);
            Scribe_Values.Look(ref allowNonHuman, "allowNonHuman", false);
            Scribe_Values.Look(ref requirePawnInFaction, "requirePawnInFaction", true);
            Scribe_Values.Look(ref sprintFoodDrain, "sprintFoodDrain", 1.5f);
            Scribe_Values.Look(ref sprintSleepDrain, "sprintSleepDrain", 2f);
            Scribe_Values.Look(ref disableCustomGizmos, "disableCustomGizmos", false);
            Scribe_Collections.Look(ref pinnedNeeds, "pinnedNeeds", LookMode.Value);
            Scribe_Values.Look(ref enableDamageScreenEffect, "enableDamageScreenEffect", true);

            if (pinnedNeeds == null)
            {
                pinnedNeeds = new List<string>() { "Mood", "Food", "Rest", "Joy" };
            }
        }
    }
}
