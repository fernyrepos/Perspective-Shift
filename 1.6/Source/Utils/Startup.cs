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
            InjectComps();
        }

        private static void InjectComps()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.IsWorkTable || typeof(Building_Storage).IsAssignableFrom(def.thingClass))
                {
                    if (def.comps == null) def.comps = new List<CompProperties>();
                    def.comps.Add(new CompProperties_PlayerOnly());
                }
                if (typeof(Building_Storage).IsAssignableFrom(def.thingClass))
                {
                    if (def.comps == null) def.comps = new List<CompProperties>();
                    def.comps.Add(new CompProperties_StorageSlotOrder());
                }
            }
        }

        public static void PatchThinkTreeDefs()
        {
            var thinkTreeDefs = DefDatabase<ThinkTreeDef>.AllDefsListForReading;
            foreach (var thinkTreeDef in thinkTreeDefs)
            {
                if (thinkTreeDef == DefsOf.Downed) continue;
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
                            (node as ThinkNode_Subtree)?.treeDef == DefsOf.LordDuty);

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

            var seekAtWillNode = new ThinkNode_ConditionalSeekAtWill
            {
                subNodes = new List<ThinkNode>
                {
                    new JobGiver_SeekAtWill()
                }
            };

            subNodes.Insert(index, seekAtWillNode);
        }
    }
}
