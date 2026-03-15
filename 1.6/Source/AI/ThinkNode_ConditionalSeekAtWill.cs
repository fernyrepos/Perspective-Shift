using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class ThinkNode_ConditionalSeekAtWill : ThinkNode_Conditional
    {
        public override bool Satisfied(Pawn pawn)
        {
            State.seekAtWillPawns ??= new HashSet<int>();
            return !pawn.IsAvatar() && pawn.InMentalState is false && pawn.Faction == Faction.OfPlayer && State.seekAtWillPawns.Contains(pawn.thingIDNumber);
        }
    }
}
