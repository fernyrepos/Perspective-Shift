using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public abstract class FloatMenuOptionProvider_AvatarInteraction : FloatMenuOptionProvider
    {
        public override bool Drafted => false;
        public override bool Undrafted => true;
        public override bool Multiselect => false;

        public override bool AppliesInt(FloatMenuContext context)
        {
            return State.IsActive && context.FirstSelectedPawn.IsAvatar();
        }

        protected bool CanInteractWithTarget(Pawn avatar, Pawn target, InteractionDef interactionDef)
        {
            if (target == avatar)
                return false;
            if (Find.TickManager.TicksGame < avatar.interactions.lastInteractionTime + (GenDate.TicksPerDay / 2))
                return false;
            if (!avatar.interactions.CanInteractNowWith(target, interactionDef))
                return false;

            if (!SocialInteractionUtility.CanReceiveRandomInteraction(target))
                return false;

            if (avatar.HostileTo(target))
                return false;

            return true;
        }
    }
}
