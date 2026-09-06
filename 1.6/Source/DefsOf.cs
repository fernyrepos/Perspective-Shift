using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [DefOf]
    public static class DefsOf
    {
        public static KeyBindingDef PS_MoveForward;
        public static KeyBindingDef PS_MoveBack;
        public static KeyBindingDef PS_MoveLeft;
        public static KeyBindingDef PS_MoveRight;
        public static KeyBindingDef PS_Sprint;
        public static KeyBindingDef PS_Walk;
        public static KeyBindingDef PS_OpenGearTab;
        public static KeyBindingDef PS_HealthTab;
        public static KeyBindingDef PS_NeedsTab;
        public static KeyBindingDef PS_EatFood;
        public static KeyBindingDef PS_DoRecreation;
        public static SoundDef PS_PackInventory;
        public static SoundDef PS_StorageOpen;
        public static SoundDef PS_SprintSound;
        public static SoundDef PS_FishBite;
        public static SoundDef PS_FishHit;
        public static SoundDef PS_FishEscape;
        public static SoundDef PS_ReelSlow;
        public static SoundDef PS_ReelFast;
        public static JobDef PS_FishMinigame;
        public static ThinkTreeDef LordDuty;
        public static ThinkTreeDef Downed;
        static DefsOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DefsOf));
        }
    }
}
