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
            if (State.CurrentMode == PlaystyleMode.Director && PerspectiveShiftMod.settings.totalFreedom is false) yield break;

            var selectedComps = GetSelectedComps();

            if (isWorkbench)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "PS_PlayerOnly".Translate(),
                    icon = ContentFinder<Texture2D>.Get("Gizmos/OnlyPlayerCanUse"),
                    groupKey = 847562341,
                    isActive = () => mode == PlayerOnlyMode.Use,
                    toggleAction = () =>
                    {
                        var newMode = mode == PlayerOnlyMode.Use ? PlayerOnlyMode.None : PlayerOnlyMode.Use;
                        foreach (var comp in selectedComps)
                            comp.mode = newMode;
                    }
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
                    groupKey = 847562341,
                    action = () =>
                    {
                        var list = new List<FloatMenuOption>
                        {
                            new FloatMenuOption("PS_NoRestriction".Translate(), () => SetModeOnSelected(selectedComps, PlayerOnlyMode.None)),
                            new FloatMenuOption("PS_OnlyPlayerCanStore".Translate(), () => SetModeOnSelected(selectedComps, PlayerOnlyMode.Store)),
                            new FloatMenuOption("PS_OnlyPlayerCanTake".Translate(), () => SetModeOnSelected(selectedComps, PlayerOnlyMode.Take)),
                            new FloatMenuOption("PS_OnlyPlayerCanUse".Translate(), () => SetModeOnSelected(selectedComps, PlayerOnlyMode.Use))
                        };
                        Find.WindowStack.Add(new FloatMenu(list));
                    }
                };
            }
        }

        private static List<CompPlayerOnly> GetSelectedComps()
        {
            var result = new List<CompPlayerOnly>();
            foreach (var obj in Find.Selector.SelectedObjects)
                if (obj is ThingWithComps twc)
                {
                    var comp = twc.GetComp<CompPlayerOnly>();
                    if (comp != null)
                        result.Add(comp);
                }
            return result;
        }

        private static void SetModeOnSelected(List<CompPlayerOnly> comps, PlayerOnlyMode newMode)
        {
            foreach (var comp in comps)
            {
                comp.mode = newMode;
                if (newMode == PlayerOnlyMode.Store || newMode == PlayerOnlyMode.Use)
                    comp.WideFilter();
            }
        }
    }
}
