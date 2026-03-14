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
            PatchThinkTreeDefs();
        }

        public static void PatchThinkTreeDefs()
        {
            var thinkTreeDefs = DefDatabase<ThinkTreeDef>.AllDefsListForReading;
            foreach (var thinkTreeDef in thinkTreeDefs)
            {
                if (thinkTreeDef.defName == "Downed") continue;
                var rootNode = thinkTreeDef.thinkRoot;
                if (rootNode == null || rootNode.subNodes == null) continue;

                int isColonist = rootNode.subNodes.FindIndex(node => node.GetType() == typeof(ThinkNode_ConditionalColonist));
                if (isColonist >= 0)
                {
                    InsertNodeAt(rootNode.subNodes, isColonist);
                }
                else
                {
                    int queuedJobNodeIndex = rootNode.subNodes.FindIndex(node => node.GetType() == typeof(ThinkNode_QueuedJob));
                    if (queuedJobNodeIndex >= 0)
                    {
                        InsertNodeAt(rootNode.subNodes, queuedJobNodeIndex);
                    }
                    else
                    {
                        int subtreeNodeIndex = rootNode.subNodes.FindIndex(node => node.GetType() == typeof(ThinkNode_Subtree) &&
                            (node as ThinkNode_Subtree)?.treeDef.defName == "LordDuty");

                        if (subtreeNodeIndex >= 0)
                        {
                            InsertNodeAt(rootNode.subNodes, subtreeNodeIndex);
                        }
                        else
                        {
                            var revenantIndex = rootNode.subNodes.FindIndex(node => node.GetType() == typeof(ThinkNode_ConditionalRevenantState));
                            if (revenantIndex >= 0)
                            {
                                InsertNodeAt(rootNode.subNodes, revenantIndex);
                            }
                        }
                    }
                }
            }
        }

        private static void InsertNodeAt(List<ThinkNode> subNodes, int index)
        {
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

            subNodes.Insert(index, newNode);
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
