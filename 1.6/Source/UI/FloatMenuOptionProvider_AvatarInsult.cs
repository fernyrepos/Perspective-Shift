using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_AvatarInsult : FloatMenuOptionProvider
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

            if (!avatar.interactions.CanInteractNowWith(clickedPawn, InteractionDefOf.Insult))
                return null;

            return new FloatMenuOption("PS_Insult".Translate(clickedPawn.LabelShort), () =>
            {
                avatar.interactions.TryInteractWith(clickedPawn, InteractionDefOf.Insult);
            });
        }
    }
}
