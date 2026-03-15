using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    public enum PlayerOnlyMode { None, Store, Take, Use }

    public class CompProperties_PlayerOnly : CompProperties
    {
        public CompProperties_PlayerOnly() { compClass = typeof(CompPlayerOnly); }
    }

    public class CompPlayerOnly : ThingComp
    {
        public PlayerOnlyMode mode = PlayerOnlyMode.None;
        public bool isWorkbench;

        private void WideFilter()
        {
            if (parent is Building_Storage s)
                s.settings.filter.SetAllowAll(null);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            isWorkbench = parent is Building_WorkTable;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref mode, "ps_playerOnlyMode", PlayerOnlyMode.None);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (State.CurrentMode == PlaystyleMode.Director) yield break;

            if (isWorkbench)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "PS_PlayerOnly".Translate(),
                    icon = ContentFinder<Texture2D>.Get("Gizmos/OnlyPlayerCanUse"),
                    isActive = () => mode == PlayerOnlyMode.Use,
                    toggleAction = () => mode = mode == PlayerOnlyMode.Use ? PlayerOnlyMode.None : PlayerOnlyMode.Use
                };
            }
            else
            {
                yield return new Command_Action
                {
                    defaultLabel = (mode == PlayerOnlyMode.None ? "PS_NoRestriction" :
                                    mode == PlayerOnlyMode.Store ? "PS_OnlyPlayerCanStore" :
                                    mode == PlayerOnlyMode.Take ? "PS_OnlyPlayerCanTake" :
                                    "PS_OnlyPlayerCanUse").Translate(),
                    icon = ContentFinder<Texture2D>.Get(mode == PlayerOnlyMode.None ? "Gizmos/NoPlayerStorageRestriction" : mode == PlayerOnlyMode.Store ? "Gizmos/OnlyPlayerCanStore" : mode == PlayerOnlyMode.Take ? "Gizmos/OnlyPlayerCanTake" : "Gizmos/OnlyPlayerCanUse"),
                    action = () =>
                    {
                        var list = new List<FloatMenuOption>
                        {
                            new FloatMenuOption("PS_NoRestriction".Translate(), () => mode = PlayerOnlyMode.None),
                            new FloatMenuOption("PS_OnlyPlayerCanStore".Translate(), () => { mode = PlayerOnlyMode.Store; WideFilter(); }),
                            new FloatMenuOption("PS_OnlyPlayerCanTake".Translate(), () => mode = PlayerOnlyMode.Take),
                            new FloatMenuOption("PS_OnlyPlayerCanUse".Translate(), () => { mode = PlayerOnlyMode.Use; WideFilter(); })
                        };
                        Find.WindowStack.Add(new FloatMenu(list));
                    }
                };
            }
        }
    }
}
