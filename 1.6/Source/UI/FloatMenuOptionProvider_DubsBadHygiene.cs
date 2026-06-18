using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class FloatMenuOptionProvider_DubsBadHygiene : FloatMenuOptionProvider_AvatarBase
    {
        public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
        {
            if (!ModCompatibility.DubsBadHygieneAvailable) yield break;

            var clickCell = context.clickPosition.ToIntVec3();
            if (!clickCell.InBounds(context.map)) yield break;

            var terrain = clickCell.GetTerrain(context.map);
            if (terrain != null && (terrain.IsWater || terrain.IsRiver || terrain.HasTag("dbh_water") || terrain.HasTag("dbh_ocean") || terrain.HasTag("dbh_s_water")))
            {
                if (ModCompatibility.IsDBHHygieneEnabled() && ModCompatibility.DBH_washAtCell != null)
                    yield return new FloatMenuOption("PS_WashYourself".Translate(), () =>
                    {
                        var job = JobMaker.MakeJob(ModCompatibility.DBH_washAtCell, clickCell);
                        State.Avatar.pawn.jobs.TryTakeOrderedJob(job);
                    });

                if (ModCompatibility.IsDBHThirstEnabled() && ModCompatibility.DBH_DBHDrinkFromGround != null)
                    yield return new FloatMenuOption("PS_DrinkWater".Translate(), () =>
                    {
                        var job = JobMaker.MakeJob(ModCompatibility.DBH_DBHDrinkFromGround, clickCell);
                        State.Avatar.pawn.jobs.TryTakeOrderedJob(job);
                    });

                if (ModCompatibility.IsDBHBladderEnabled() && ModCompatibility.DBH_haveWildPoo != null)
                    yield return new FloatMenuOption("PS_RelieveBladder".Translate(), () =>
                    {
                        var job = JobMaker.MakeJob(ModCompatibility.DBH_haveWildPoo, clickCell);
                        State.Avatar.pawn.jobs.TryTakeOrderedJob(job);
                    });
            }

            foreach (var thing in clickCell.GetThingList(context.map))
            {
                if (ModCompatibility.IsDBHThirstEnabled())
                {
                    if (ModCompatibility.IsDBHDrinkableItem(thing))
                    {
                        yield return new FloatMenuOption("PS_DrinkWater".Translate(), () =>
                        {
                            var job = JobMaker.MakeJob(JobDefOf.Ingest, thing);
                            job.count = 1;
                            State.Avatar.pawn.jobs.TryTakeOrderedJob(job);
                        });
                    }
                    else if (ModCompatibility.IsDBHBasin(thing) && ModCompatibility.DBH_DBHDrinkFromBasin != null)
                    {
                        yield return new FloatMenuOption("PS_DrinkWater".Translate(), () =>
                        {
                            var job = JobMaker.MakeJob(ModCompatibility.DBH_DBHDrinkFromBasin, thing);
                            State.Avatar.pawn.jobs.TryTakeOrderedJob(job);
                        });
                    }
                }
            }
        }
    }
}