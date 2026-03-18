using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class StatPart_AvatarWorkSpeed : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing && req.Thing is Pawn pawn && pawn.IsAvatar())
                val *= PerspectiveShiftMod.settings.workSpeedMultiplier;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.HasThing && req.Thing is Pawn pawn && pawn.IsAvatar()
                && PerspectiveShiftMod.settings.workSpeedMultiplier != 1f)
                return "PS_StatPart_AvatarWorkSpeed".Translate()
                    + ": x" + PerspectiveShiftMod.settings.workSpeedMultiplier.ToStringPercent();
            return null;
        }
    }
}
