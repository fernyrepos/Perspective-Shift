using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            foreach (var thinkTreeDef in DefDatabase<ThinkTreeDef>.AllDefsListForReading)
            {
                var rootNode = thinkTreeDef.thinkRoot;
                if (rootNode?.subNodes == null) continue;

                var newNode = new ThinkNode_ConditionalAvatarDrafted
                {
                    subNodes = new List<ThinkNode>
                    {
                        new ThinkNode_QueuedJob(),
                        new ThinkNode_Tagger
                        {
                            tagToGive = JobTag.DraftedOrder,
                            subNodes = new List<ThinkNode>
                            {
                                new JobGiver_MoveToStandable(),
                                new JobGiver_Orders()
                            }
                        }
                    }
                };
                rootNode.subNodes.Insert(0, newNode);
            }
        }
    }

    public class ThinkNode_ConditionalAvatarDrafted : ThinkNode_Conditional
    {
        public override bool Satisfied(Pawn pawn)
        {
            return pawn.IsAvatar() && pawn.Drafted;
        }
    }
}
