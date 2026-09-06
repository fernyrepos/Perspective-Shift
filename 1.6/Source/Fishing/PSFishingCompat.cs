using RimWorld;
using System.Collections.Generic;
using Verse;

namespace PerspectiveShift
{
    public static class PSFishingCompat
    {
        private static bool resolved;

        private static HediffDef caughtSpecialHediff;
        private static PreceptDef recreationPrecept;
        private static PreceptDef adeptPrecept;
        private static TraitDef fishermanTrait;
        private static ThoughtDef fishingThought;
        private static EffecterDef mechNewEffecter;
        private static EffecterDef mechAncientEffecter;
        private static readonly List<ThingDef> adeptCatches = new List<ThingDef>();

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            caughtSpecialHediff = DefDatabase<HediffDef>.GetNamedSilentFail("VCEF_CaughtSpecialHediff");
            recreationPrecept = DefDatabase<PreceptDef>.GetNamedSilentFail("VME_Recreation_Fishing");
            adeptPrecept = DefDatabase<PreceptDef>.GetNamedSilentFail("VME_Fishing_Adept");
            fishermanTrait = DefDatabase<TraitDef>.GetNamedSilentFail("VCEF_Fisherman");
            fishingThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("VCEF_FishingThought");
            mechNewEffecter = DefDatabase<EffecterDef>.GetNamedSilentFail("VCEF_Fishing_MechNew");
            mechAncientEffecter = DefDatabase<EffecterDef>.GetNamedSilentFail("VCEF_Fishing_MechAncient");

            foreach (string name in new[] { "VCEF_Crayfish", "VCEF_ButterFish", "VCEF_FreshwaterStingray", "VCEF_FlyingFish", "VCEF_Arapaima", "VCEF_ShortfinMakoShark" })
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def != null) adeptCatches.Add(def);
            }
        }

        public static EffecterDef FishingEffecterFor(Pawn pawn)
        {
            Resolve();
            if (pawn == null || !pawn.IsColonyMech) return EffecterDefOf.Fishing;

            var def = pawn.ageTracker.AgeChronologicalYears < 100 ? mechNewEffecter : mechAncientEffecter;
            return def ?? EffecterDefOf.Fishing;
        }

        public static void Notify_Caught(Pawn pawn, bool rare)
        {
            Resolve();
            if (pawn == null) return;

            if (rare && caughtSpecialHediff != null)
            {
                pawn.health?.AddHediff(caughtSpecialHediff);
            }

            if (HasPrecept(pawn, recreationPrecept))
            {
                pawn.needs?.joy?.GainJoy(0.1f, JoyKindDefOf.Meditative);
            }

            if (HasPrecept(pawn, adeptPrecept) && adeptCatches.Count > 0 && Rand.Chance(0.2f) && pawn.Spawned)
            {
                var thing = ThingMaker.MakeThing(adeptCatches.RandomElement());
                thing.stackCount = 10;
                GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }

            if (fishermanTrait != null && fishingThought != null && (pawn.story?.traits?.HasTrait(fishermanTrait) ?? false))
            {
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(fishingThought);
            }
        }

        private static bool HasPrecept(Pawn pawn, PreceptDef precept)
        {
            if (precept == null || !ModLister.IdeologyInstalled) return false;
            return pawn.ideo?.Ideo?.HasPrecept(precept) ?? false;
        }
    }
}
