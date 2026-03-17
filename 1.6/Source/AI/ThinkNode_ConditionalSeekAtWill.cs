using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class ThinkNode_ConditionalSeekAtWill : ThinkNode_Conditional
    {
        public override bool Satisfied(Pawn pawn)
        {
            return pawn.ShouldSeekEnemy();
        }
    }
}
