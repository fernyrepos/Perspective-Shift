using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HotSwappable]
    public abstract class FloatMenuOptionProvider_AvatarInteraction : FloatMenuOptionProvider_AvatarBase
    {
        public static Dictionary<int, int> lastInteractionByTarget = new Dictionary<int, int>();

        protected bool CanInteractWithTarget(Pawn target, InteractionDef interactionDef)
        {
            if (target == State.Avatar.pawn || target.Spawned is false)
                return false;
            lastInteractionByTarget ??= new Dictionary<int, int>();
            if (lastInteractionByTarget.TryGetValue(target.thingIDNumber, out int lastTick))
            {
                if (Find.TickManager.TicksGame < lastTick + (GenDate.TicksPerDay / 2))
                    return false;
            }

            if (!State.Avatar.pawn.interactions.CanInteractNowWith(target, interactionDef))
                return false;

            if (!SocialInteractionUtility.CanReceiveRandomInteraction(target))
                return false;

            if (State.Avatar.pawn.HostileTo(target))
                return false;

            return true;
        }

        protected void PerformInteraction(Pawn target, InteractionDef def)
        {
            if (State.Avatar.pawn.interactions.TryInteractWith(target, def))
            {
                lastInteractionByTarget[target.thingIDNumber] = Find.TickManager.TicksGame;
            }
        }
    }
}
