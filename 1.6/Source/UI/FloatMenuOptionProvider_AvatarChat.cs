using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_AvatarChat : FloatMenuOptionProvider
    {
        public override bool Drafted => false;
        public override bool Undrafted => true;
        public override bool Multiselect => false;

        public override bool AppliesInt(FloatMenuContext context)
        {
            return State.IsActive && context.FirstSelectedPawn.IsAvatar();
        }

        public override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            var avatar = State.Avatar.pawn;
            if (clickedPawn == avatar) return null;

            if (!avatar.interactions.CanInteractNowWith(clickedPawn, InteractionDefOf.Chitchat) ||
                Find.TickManager.TicksGame < avatar.interactions.lastInteractionTime + Pawn_InteractionsTracker.DirectTalkInteractInterval)
                return null;

            return new FloatMenuOption("PS_ChatWith".Translate(clickedPawn.LabelShort), () =>
            {
                var def = Rand.Chance(0.75f) ? InteractionDefOf.Chitchat : InteractionDefOf.DeepTalk;
                avatar.interactions.TryInteractWith(clickedPawn, def);
            });
        }
    }
}
