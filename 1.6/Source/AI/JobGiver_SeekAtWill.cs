using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PerspectiveShift
{
    [HotSwappable]
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
            try
            {
                return TryGiveJobInt(pawn);
            }
            finally
            {
                pawn.playerSettings.hostilityResponse = prev;
            }
        }

        private Job TryGiveJobInt(Pawn pawn)
        {
            bool isDuelist = pawn.GetLord()?.LordJob is LordJob_Ritual_Duel duel && duel.duelists.Contains(pawn);
            if ((pawn.IsColonist || pawn.IsColonySubhuman) && pawn.playerSettings.hostilityResponse != HostilityResponseMode.Attack && !isDuelist)
            {
                return null;
            }

            UpdateEnemyTarget(pawn);
            Thing enemyTarget = pawn.mindState.enemyTarget;

            if (enemyTarget == null)
            {
                return null;
            }

            if (enemyTarget is Pawn pawn2 && pawn2.IsPsychologicallyInvisible())
            {
                return null;
            }

            bool flag = !pawn.IsColonist && !pawn.IsColonySubhuman && !DisableAbilityVerbs;
            if (flag)
            {
                Job abilityJob = GetAbilityJob(pawn, enemyTarget);
                if (abilityJob != null)
                {
                    return abilityJob;
                }
            }

            if (OnlyUseAbilityVerbs)
            {
                if (!TryFindShootingPosition(pawn, out var dest))
                {
                    Log.Warning($"[JobGiver_SeekAtWill] TryGiveJob - FAIL: TryFindShootingPosition returned false (OnlyUseAbilityVerbs mode)");
                    return null;
                }
                if (dest == pawn.Position)
                {
                    pawn.pather?.StopDead();
                    var waitJob = JobMaker.MakeJob(JobDefOf.Wait_Combat, 30, checkOverrideOnExpiry: true);
                    return waitJob;
                }
                var gotoJob = JobMaker.MakeJob(JobDefOf.Goto, dest);
                gotoJob.expiryInterval = 30;
                gotoJob.checkOverrideOnExpire = true;
                return gotoJob;
            }

            Verb verb = pawn.TryGetAttackVerb(enemyTarget, flag, allowTurrets);
            if (verb == null)
            {
                return null;
            }

            if (verb.verbProps.IsMeleeAttack)
            {
                var meleeJob = MeleeAttackJob(pawn, enemyTarget);
                return meleeJob;
            }

            bool num = CoverUtility.CalculateOverallBlockChance(pawn, enemyTarget.Position, pawn.Map) > 0.01f;
            bool flag2 = pawn.Position.WalkableBy(pawn.Map, pawn) && pawn.Map.pawnDestinationReservationManager.CanReserve(pawn.Position, pawn, pawn.Drafted);
            bool flag3 = verb.CanHitTarget(enemyTarget);
            bool flag4 = (pawn.Position - enemyTarget.Position).LengthHorizontalSquared < 25;

            if ((num && flag2 && flag3) || (flag4 && flag3))
            {
                pawn.pather?.StopDead();
                var waitJob2 = JobMaker.MakeJob(JobDefOf.Wait_Combat, 30, checkOverrideOnExpiry: true);
                return waitJob2;
            }

            if (!TryFindShootingPosition(pawn, out var dest2))
            {
                Log.Warning($"[JobGiver_SeekAtWill] TryGiveJob - FAIL: TryFindShootingPosition returned false");
                return null;
            }

            if (dest2 == pawn.Position)
            {
                pawn.pather?.StopDead();
                var waitJob3 = JobMaker.MakeJob(JobDefOf.Wait_Combat, 30, checkOverrideOnExpiry: true);
                return waitJob3;
            }

            var gotoJob2 = JobMaker.MakeJob(JobDefOf.Goto, dest2);
            gotoJob2.expiryInterval = 30;
            gotoJob2.checkOverrideOnExpire = true;
            return gotoJob2;
        }

        public override Thing FindAttackTarget(Pawn pawn)
        {
            TargetScanFlags targetScanFlags = TargetScanFlags.NeedReachableIfCantHitFromMyPos | TargetScanFlags.NeedThreat;
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

        public override Job MeleeAttackJob(Pawn pawn, Thing enemyTarget)
        {
            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, enemyTarget);
            job.expiryInterval = 30;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = true;
            return job;
        }

        public override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
        {
            Thing enemyTarget = pawn.mindState.enemyTarget;
            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman;
            Verb verb = verbToUse ?? pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, allowTurrets);
            if (verb == null)
            {
                dest = IntVec3.Invalid;
                return false;
            }
            CastPositionRequest newReq = default(CastPositionRequest);
            newReq.caster = pawn;
            newReq.target = enemyTarget;
            newReq.verb = verb;
            newReq.maxRangeFromTarget = verb.EffectiveRange;
            newReq.wantCoverFromTarget = verb.EffectiveRange > 5f;
            return CastPositionFinder.TryFindCastPosition(newReq, out dest);
        }
    }
}
