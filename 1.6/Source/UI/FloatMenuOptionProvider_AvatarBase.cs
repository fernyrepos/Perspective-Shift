using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public abstract class FloatMenuOptionProvider_AvatarBase : FloatMenuOptionProvider
    {
        public override bool Drafted => false;
        public override bool Undrafted => true;
        public override bool Multiselect => false;
        public override bool AppliesInt(FloatMenuContext context)
        {
            return State.IsActive && context.FirstSelectedPawn.IsAvatar() && !Avatar.IsAvatarLeftClick;
        }
    }
}
