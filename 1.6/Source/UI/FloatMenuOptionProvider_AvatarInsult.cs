using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_AvatarInsult : FloatMenuOptionProvider_AvatarInteraction
    {
        public override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!CanInteractWithTarget(clickedPawn, InteractionDefOf.Insult))
                return null;

            return new FloatMenuOption("PS_Insult".Translate(clickedPawn.LabelShort), () =>
            {
                PerformInteraction(clickedPawn, InteractionDefOf.Insult);
            });
        }
    }
}
