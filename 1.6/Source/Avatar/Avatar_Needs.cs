using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PerspectiveShift
{
    public partial class Avatar
    {
        private const int AutoNeedCheckInterval = 60;

        private static Dictionary<AutoNeed, ThinkNode_JobGiver> autoNeedGivers;
        private static ThinkNode_Priority_GetJoy joyPriority;

        private static NeedDef hygieneNeedDef;
        private static NeedDef bladderNeedDef;
        private static bool resolvedModNeedDefs;

        private readonly HashSet<AutoNeed> autoNeedTried = new HashSet<AutoNeed>();
        private Job autoNeedJob;
        private Job autoNeedReturnJob;
        private IntVec3? autoNeedReturnCell;
        private bool autoNeedChainActive;

        private void HandleSuppressedNeeds()
        {
            var settings = PerspectiveShiftMod.settings;

            if (settings.noPlayerRestNeed) TopUpNeed(pawn.needs.rest);
            if (settings.noPlayerFoodNeed) TopUpNeed(pawn.needs.food);
            if (settings.noPlayerRecreationNeed) TopUpNeed(pawn.needs.joy);

            if (!settings.noPlayerHygieneNeed && !settings.noPlayerBladderNeed) return;

            ResolveModNeedDefs();

            if (settings.noPlayerHygieneNeed && hygieneNeedDef != null) TopUpNeed(pawn.needs.TryGetNeed(hygieneNeedDef));
            if (settings.noPlayerBladderNeed && bladderNeedDef != null) TopUpNeed(pawn.needs.TryGetNeed(bladderNeedDef));
        }

        private static void TopUpNeed(Need need)
        {
            if (need == null) return;
            if (need.CurLevel >= need.MaxLevel) return;
            need.CurLevel = need.MaxLevel;
        }

        private static void ResolveModNeedDefs()
        {
            if (resolvedModNeedDefs) return;

            resolvedModNeedDefs = true;
            hygieneNeedDef = DefDatabase<NeedDef>.GetNamedSilentFail("Hygiene");
            bladderNeedDef = DefDatabase<NeedDef>.GetNamedSilentFail("Bladder");
        }

        private Need GetAutoNeed(AutoNeed key)
        {
            switch (key)
            {
                case AutoNeed.Food: return pawn.needs.food;
                case AutoNeed.Sleep: return pawn.needs.rest;
                case AutoNeed.Recreation: return pawn.needs.joy;
                case AutoNeed.Bladder: return bladderNeedDef == null ? null : pawn.needs.TryGetNeed(bladderNeedDef);
                case AutoNeed.Hygiene: return hygieneNeedDef == null ? null : pawn.needs.TryGetNeed(hygieneNeedDef);
                default: return null;
            }
        }

        private float AutoNeedPriority(AutoNeed key, ThinkNode_JobGiver giver, float level, float threshold)
        {
            float vanilla = key == AutoNeed.Recreation ? joyPriority.GetPriority(pawn) : giver.GetPriority(pawn);
            if (vanilla > 0f) return vanilla;

            return 1f + (threshold - level);
        }

        private static void ResolveAutoNeedGivers()
        {
            if (autoNeedGivers != null) return;

            joyPriority = new ThinkNode_Priority_GetJoy();
            autoNeedGivers = new Dictionary<AutoNeed, ThinkNode_JobGiver>
            {
                { AutoNeed.Food, new JobGiver_GetFood() },
                { AutoNeed.Sleep, new JobGiver_GetRest() },
                { AutoNeed.Recreation, new JobGiver_GetJoy() }
            };

            var toilet = ModCompatibility.MakeHygieneJobGiver("DubsBadHygiene.JobGiver_UseToilet");
            if (toilet != null) autoNeedGivers[AutoNeed.Bladder] = toilet;

            var wash = ModCompatibility.MakeHygieneJobGiver("DubsBadHygiene.JobGiver_HaveWash");
            if (wash != null) autoNeedGivers[AutoNeed.Hygiene] = wash;

            foreach (var giver in autoNeedGivers.Values)
            {
                giver.ResolveReferences();
            }
        }

        private void HandleAutoNeeds()
        {
            if (!State.fulfillNeeds || !PerspectiveShiftMod.settings.enableFulfillNeedsGizmo)
            {
                ForgetAutoNeedReturn();
                return;
            }

            if (pawn.needs == null || pawn.jobs == null || pawn.Drafted || IsMoving)
            {
                ForgetAutoNeedReturn();
                return;
            }

            if (Find.TickManager.TicksGame % AutoNeedCheckInterval != 0) return;

            if (autoNeedReturnJob != null)
            {
                if (pawn.CurJob == autoNeedReturnJob) return;
                autoNeedReturnJob = null;
                ConcludeAutoNeedChain();
            }

            if (autoNeedJob != null)
            {
                if (pawn.CurJob == autoNeedJob) return;
                autoNeedJob = null;
            }

            if (CarriedThing != null || !IsIdleForAutoNeeds())
            {
                ForgetAutoNeedReturn();
                return;
            }

            ResolveAutoNeedGivers();
            ResolveModNeedDefs();

            autoNeedTried.Clear();
            while (true)
            {
                ThinkNode_JobGiver best = null;
                AutoNeed bestNeed = AutoNeed.Food;
                float bestPriority = 0f;

                foreach (var pair in autoNeedGivers)
                {
                    if (autoNeedTried.Contains(pair.Key)) continue;
                    if (!State.IsAutoNeedEnabled(pair.Key)) continue;

                    var need = GetAutoNeed(pair.Key);
                    if (need == null) continue;

                    float level = need.CurLevelPercentage;
                    float threshold = State.GetAutoNeedThreshold(pair.Key);
                    if (level > threshold) continue;

                    float priority = AutoNeedPriority(pair.Key, pair.Value, level, threshold);
                    if (priority <= bestPriority) continue;

                    best = pair.Value;
                    bestNeed = pair.Key;
                    bestPriority = priority;
                }

                if (best == null) break;

                autoNeedTried.Add(bestNeed);

                var job = TryIssueAutoNeedJob(best);
                if (job == null) continue;

                if (State.returnAfterFulfillment && !autoNeedReturnCell.HasValue) autoNeedReturnCell = pawn.Position;

                pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, false);
                autoNeedJob = job;
                autoNeedChainActive = true;
                ThrowAutoNeedCallout(("PS_Callout_" + bestNeed).Translate());
                return;
            }

            EndAutoNeedChain();
        }

        private Job TryIssueAutoNeedJob(ThinkNode_JobGiver giver)
        {
            JobGivers_DisableForAvatar_Patch.allowAvatarJobs = true;
            try
            {
                return giver.TryIssueJobPackage(pawn, default).Job;
            }
            catch (System.Exception ex)
            {
                State.Error($"Auto-need job giver {giver.GetType().Name} failed: {ex}");
                return null;
            }
            finally
            {
                JobGivers_DisableForAvatar_Patch.allowAvatarJobs = false;
            }
        }

        private void ForgetAutoNeedReturn()
        {
            autoNeedJob = null;
            autoNeedReturnJob = null;
            autoNeedReturnCell = null;
            autoNeedChainActive = false;
        }

        private void EndAutoNeedChain()
        {
            if (!autoNeedChainActive)
            {
                autoNeedReturnCell = null;
                return;
            }

            if (TryReturnAfterAutoNeed()) return;

            ConcludeAutoNeedChain();
        }

        private void ConcludeAutoNeedChain()
        {
            autoNeedChainActive = false;
            autoNeedReturnCell = null;
            ThrowAutoNeedCallout("PS_Callout_Done".Translate());
            if (State.playDoneSound) DefsOf.PS_NeedsDone.PlayOneShotOnCamera();
        }

        private void ThrowAutoNeedCallout(string text)
        {
            if (!State.showJobCallouts) return;
            if (!pawn.Spawned || pawn.Map == null) return;

            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, text, 3.65f);
        }

        private bool TryReturnAfterAutoNeed()
        {
            if (!autoNeedReturnCell.HasValue) return false;

            var cell = autoNeedReturnCell.Value;
            autoNeedReturnCell = null;

            if (!State.returnAfterFulfillment) return false;
            if (cell == pawn.Position) return false;
            if (!cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map)) return false;
            if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) return false;

            var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, false);
            autoNeedReturnJob = job;
            return true;
        }

        private bool IsIdleForAutoNeeds()
        {
            var job = pawn.CurJob;
            if (job == null) return true;

            return job.def == JobDefOf.Wait
                || job.def == JobDefOf.Wait_Combat
                || job.def == JobDefOf.Wait_MaintainPosture
                || job.def == JobDefOf.Wait_Wander
                || job.def == JobDefOf.GotoWander;
        }
    }
}
