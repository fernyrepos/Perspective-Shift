using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class ThinkNode_ConditionalAvatarDrafted : ThinkNode_Conditional
    {
        public override bool Satisfied(Pawn pawn)
        {
            return pawn.IsAvatar() && pawn.Drafted;
        }
    }
}
