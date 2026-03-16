using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_AvatarChat : FloatMenuOptionProvider_AvatarInteraction
    {
        public override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            var avatar = State.Avatar.pawn;
            if (!CanInteractWithTarget(avatar, clickedPawn, InteractionDefOf.Chitchat))
                return null;

            return new FloatMenuOption("PS_ChatWith".Translate(clickedPawn.LabelShort), () =>
            {
                var def = Rand.Chance(0.75f) ? InteractionDefOf.Chitchat : InteractionDefOf.DeepTalk;
                PerformInteraction(avatar, clickedPawn, def);
            });
        }
    }
}
