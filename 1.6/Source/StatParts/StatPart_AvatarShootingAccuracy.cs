using RimWorld;
using Verse;

namespace PerspectiveShift
{
    public class StatPart_AvatarShootingAccuracy : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing && req.Thing is Pawn pawn && pawn.IsAvatar())
            {
                if (PerspectiveShiftMod.settings.shootAccuracyMultiplier >= 100f)
                    val = 9999f;
                else
                    val *= PerspectiveShiftMod.settings.shootAccuracyMultiplier;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.HasThing && req.Thing is Pawn pawn && pawn.IsAvatar() && PerspectiveShiftMod.settings.shootAccuracyMultiplier != 1f)
            {
                return "PS_StatPart_AvatarShootingAccuracy".Translate() + ": x" + PerspectiveShiftMod.settings.shootAccuracyMultiplier.ToStringPercent();
            }
            return null;
        }
    }
}
