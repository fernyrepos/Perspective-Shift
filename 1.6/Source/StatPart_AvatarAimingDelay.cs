using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class StatPart_AvatarAimingDelay : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (PerspectiveShiftMod.settings.disableAimingDelay == true && req.HasThing && req.Thing is Pawn pawn && pawn.IsAvatar())
            {
                val = 0f;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (PerspectiveShiftMod.settings.disableAimingDelay == true && req.HasThing && req.Thing is Pawn pawn && pawn.IsAvatar())
            {
                return "PS_StatPart_AvatarAimingDelay".Translate() + ": 0%";
            }
            return null;
        }
    }
}
