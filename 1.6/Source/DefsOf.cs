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
        static DefsOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DefsOf));
        }
    }
}
