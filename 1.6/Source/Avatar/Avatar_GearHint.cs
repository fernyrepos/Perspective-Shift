using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public partial class Avatar
    {
        private enum HintStatFormat
        {
            Number,
            Percent,
            Temperature,
            Nutrition,
        }

        private struct HintStat
        {
            public string label;
            public string value;
            public string delta;
            public float deltaWidth;
            public int sign;
        }

        private readonly List<Apparel> replacedApparelBuffer = new List<Apparel>();

        private static StatDef _insulationColdStat;
        private static StatDef InsulationColdStat => _insulationColdStat ??= DefDatabase<StatDef>.GetNamedSilentFail("Insulation_Cold");

        private static StatDef _insulationHeatStat;
        private static StatDef InsulationHeatStat => _insulationHeatStat ??= DefDatabase<StatDef>.GetNamedSilentFail("Insulation_Heat");

        private void BuildGearHintStats(Thing gear)
        {
            equipHintTitle = null;
            equipHintQuality = null;
            equipHintStatCount = 0;
            if (gear == null) return;

            if (gear is Apparel apparel)
            {
                SetHintIdentity(gear);
                BuildApparelHintStats(apparel);
                return;
            }

            if (gear.def.IsWeapon)
            {
                SetHintIdentity(gear);
                BuildWeaponHintStats(gear);
            }
        }

        private void BuildFoodHintStats(Thing food)
        {
            equipHintTitle = null;
            equipHintQuality = null;
            equipHintStatCount = 0;
            if (food == null) return;

            SetHintIdentity(food);
            AddHintStat("PS_HintNutrition".Translate(), FoodUtility.NutritionForEater(pawn, food), 0f, HintStatFormat.Nutrition);
        }

        private void BuildHarvestHintStats(Plant plant)
        {
            equipHintTitle = null;
            equipHintQuality = null;
            equipHintStatCount = 0;

            var yieldDef = plant?.def.plant?.harvestedThingDef;
            if (yieldDef == null) return;

            equipHintTitle = yieldDef.LabelCap;
            if (yieldDef.IsNutritionGivingIngestible)
            {
                AddHintStat("PS_HintNutrition".Translate(), yieldDef.GetStatValueAbstract(StatDefOf.Nutrition), 0f, HintStatFormat.Nutrition);
            }
        }

        private bool CanHarvestNow(Plant plant)
        {
            if (!plant.HarvestableNow || !plant.CanYieldNow() || !pawn.CanReserve(plant)) return false;

            return plant.def.plant.IsTree
                ? (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting) && PlantUtility.PawnWillingToCutPlant_Job(plant, pawn))
                : (plant.def.plant.harvestTag == "Standard" && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting));
        }

        private void SetHintIdentity(Thing gear)
        {
            equipHintTitle = gear.LabelNoParenthesisCap;
            if (gear.TryGetQuality(out QualityCategory quality))
            {
                equipHintQuality = quality.GetLabel().CapitalizeFirst();
            }
        }

        private void BuildWeaponHintStats(Thing weapon)
        {
            var current = pawn.equipment?.Primary;
            bool compare = current != null && current != weapon;

            float dps = WeaponDps(weapon);
            float armorPen = WeaponArmorPenetration(weapon);

            AddHintStat("PS_HintDps".Translate(), dps, compare ? dps - WeaponDps(current) : 0f, HintStatFormat.Number);
            AddHintStat("PS_HintArmorPenetration".Translate(), armorPen, compare ? armorPen - WeaponArmorPenetration(current) : 0f, HintStatFormat.Percent);
        }

        private void BuildApparelHintStats(Apparel apparel)
        {
            var replaced = ReplacedWornApparel(apparel);

            AddApparelStat(apparel, replaced, StatDefOf.ArmorRating_Sharp, "PS_HintArmorSharp".Translate(), HintStatFormat.Percent);
            AddApparelStat(apparel, replaced, StatDefOf.ArmorRating_Blunt, "PS_HintArmorBlunt".Translate(), HintStatFormat.Percent);
            AddApparelStat(apparel, replaced, InsulationColdStat, "PS_HintInsulationCold".Translate(), HintStatFormat.Temperature);
            AddApparelStat(apparel, replaced, InsulationHeatStat, "PS_HintInsulationHeat".Translate(), HintStatFormat.Temperature);
        }

        private void AddApparelStat(Apparel apparel, List<Apparel> replaced, StatDef stat, string label, HintStatFormat format)
        {
            if (stat == null) return;

            float value = apparel.GetStatValue(stat);
            float replacedValue = 0f;
            for (int i = 0; i < replaced.Count; i++)
            {
                replacedValue += replaced[i].GetStatValue(stat);
            }

            if (format == HintStatFormat.Temperature && Mathf.Abs(value) < 1f && Mathf.Abs(replacedValue) < 1f) return;

            AddHintStat(label, value, replaced.Count > 0 ? value - replacedValue : 0f, format);
        }

        private List<Apparel> ReplacedWornApparel(Apparel apparel)
        {
            replacedApparelBuffer.Clear();

            var worn = pawn.apparel?.WornApparel;
            if (worn == null) return replacedApparelBuffer;

            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i] == apparel) continue;
                if (ApparelUtility.CanWearTogether(apparel.def, worn[i].def, pawn.RaceProps.body)) continue;
                replacedApparelBuffer.Add(worn[i]);
            }
            return replacedApparelBuffer;
        }

        private void AddHintStat(string label, float value, float delta, HintStatFormat format)
        {
            if (equipHintStatCount >= equipHintStats.Length) return;

            var stat = new HintStat { label = label, value = FormatHintStat(value, format) };
            if (Mathf.Abs(delta) >= HintStatEpsilon(format))
            {
                stat.sign = delta > 0f ? 1 : -1;
                stat.delta = FormatHintStat(Mathf.Abs(delta), format);
            }
            equipHintStats[equipHintStatCount++] = stat;
        }

        private static string FormatHintStat(float value, HintStatFormat format)
        {
            switch (format)
            {
                case HintStatFormat.Percent: return value.ToStringPercent();
                case HintStatFormat.Temperature: return value.ToStringTemperatureOffset("F0");
                case HintStatFormat.Nutrition: return value.ToString("0.##");
                default: return value.ToString("F1");
            }
        }

        private static float HintStatEpsilon(HintStatFormat format)
        {
            switch (format)
            {
                case HintStatFormat.Percent: return 0.005f;
                case HintStatFormat.Temperature: return 0.5f;
                case HintStatFormat.Nutrition: return 0.005f;
                default: return 0.05f;
            }
        }

        private float WeaponDps(Thing weapon)
        {
            if (weapon == null) return 0f;

            if (weapon.def.IsRangedWeapon)
            {
                var verbProps = PrimaryRangedVerb(weapon.def);
                if (verbProps?.defaultProjectile?.projectile == null) return 0f;

                float cycleTime = verbProps.warmupTime
                    + weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown)
                    + ((verbProps.burstShotCount - 1) * verbProps.ticksBetweenBurstShots).TicksToSeconds();
                if (cycleTime <= 0f) return 0f;

                float damage = verbProps.defaultProjectile.projectile.GetDamageAmount(weapon);
                return damage * verbProps.burstShotCount / cycleTime;
            }

            if (weapon.def.IsMeleeWeapon)
            {
                return weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
            }

            return 0f;
        }

        private float WeaponArmorPenetration(Thing weapon)
        {
            if (weapon == null) return 0f;

            if (weapon.def.IsRangedWeapon)
            {
                var verbProps = PrimaryRangedVerb(weapon.def);
                if (verbProps?.defaultProjectile?.projectile == null) return 0f;
                return verbProps.defaultProjectile.projectile.GetArmorPenetration(weapon);
            }

            var tools = weapon.def.tools;
            if (tools == null || tools.Count == 0) return 0f;

            float weightTotal = 0f;
            float penetrationTotal = 0f;
            foreach (var entry in VerbUtility.GetAllVerbProperties(weapon.def.Verbs, tools))
            {
                if (!entry.verbProps.IsMeleeAttack) continue;

                float weight = entry.verbProps.AdjustedMeleeSelectionWeight(entry.tool, pawn, weapon, null, false);
                if (weight <= 0f) continue;

                weightTotal += weight;
                penetrationTotal += weight * entry.verbProps.AdjustedArmorPenetration(entry.tool, pawn, weapon, null);
            }

            return weightTotal > 0f ? penetrationTotal / weightTotal : 0f;
        }

        private static VerbProperties PrimaryRangedVerb(ThingDef def)
        {
            List<VerbProperties> verbs = def.Verbs;
            if (verbs == null) return null;

            VerbProperties fallback = null;
            for (int i = 0; i < verbs.Count; i++)
            {
                if (verbs[i].IsMeleeAttack) continue;
                if (verbs[i].isPrimary) return verbs[i];
                fallback ??= verbs[i];
            }
            return fallback;
        }
    }
}
