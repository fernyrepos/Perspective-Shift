using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public class JobGiver_SeekAtWill : JobGiver_AIFightEnemies
    {
        public JobGiver_SeekAtWill()
        {
            targetAcquireRadius = 9999f;
            targetKeepRadius = 9999f;
            chaseTarget = true;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            var prev = pawn.playerSettings.hostilityResponse;
            pawn.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
            var job = base.TryGiveJob(pawn);
            pawn.playerSettings.hostilityResponse = prev;
            return job;
        }

        public override Thing FindAttackTarget(Pawn pawn)
        {
            TargetScanFlags targetScanFlags = TargetScanFlags.NeedReachableIfCantHitFromMyPos | TargetScanFlags.NeedActiveThreat;
            if (needLOSToAcquireNonPawnTargets)
            {
                targetScanFlags |= TargetScanFlags.NeedLOSToNonPawns;
            }
            if (PrimaryVerbIsIncendiary(pawn))
            {
                targetScanFlags |= TargetScanFlags.NeedNonBurning;
            }
            if (ignoreNonCombatants)
            {
                targetScanFlags |= TargetScanFlags.IgnoreNonCombatants;
            }
            return (Thing)AttackTargetFinder.BestAttackTarget(pawn, targetScanFlags, (Thing x) => ExtraTargetValidator(pawn, x), 0f, targetAcquireRadius, GetFlagPosition(pawn), GetFlagRadius(pawn), canBashDoors: false, canTakeTargetsCloserThanEffectiveMinRange: true, canBashFences: false, OnlyUseRangedSearch);
        }
    }
}
