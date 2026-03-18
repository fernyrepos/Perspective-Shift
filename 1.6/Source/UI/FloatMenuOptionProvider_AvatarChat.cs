using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_AvatarChat : FloatMenuOptionProvider_AvatarInteraction
    {
        public override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!CanInteractWithTarget(clickedPawn, InteractionDefOf.Chitchat))
                return null;

            return new FloatMenuOption("PS_ChatWith".Translate(clickedPawn.LabelShort), () =>
            {
                var def = Rand.Chance(0.75f) ? InteractionDefOf.Chitchat : InteractionDefOf.DeepTalk;
                PerformInteraction(clickedPawn, def);
            });
        }
    }
}
