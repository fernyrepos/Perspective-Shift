using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace PerspectiveShift
{
    public partial class Avatar
    {
        private const int GatheringCheckInterval = 60;

        private readonly HashSet<int> promptedGatherings = new HashSet<int>();
        private readonly HashSet<int> declinedGatherings = new HashSet<int>();
        private bool gatheringPromptOpen;

        public bool IsGatheringDeclined(Lord lord)
        {
            return lord != null && declinedGatherings.Contains(lord.loadID);
        }

        private void HandleGatheringPrompt()
        {
            if (gatheringPromptOpen) return;
            if (Find.TickManager.TicksGame % GatheringCheckInterval != 0) return;
            if (pawn.Map == null) return;

            var lords = pawn.Map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                var lord = lords[i];
                if (promptedGatherings.Contains(lord.loadID)) continue;
                if (lord.ownedPawns.Contains(pawn)) continue;

                string key = GatheringPromptKey(lord.LordJob);
                if (key == null) continue;
                if (((LordJob_VoluntarilyJoinable)lord.LordJob).VoluntaryJoinPriorityFor(pawn) <= 0f) continue;

                AskToAttendGathering(lord, key);
                return;
            }
        }

        private string GatheringPromptKey(LordJob job)
        {
            if (job is LordJob_Joinable_Gathering) return "PS_AttendParty";

            if (job is LordJob_Joinable_MarriageCeremony ceremony)
            {
                if (ceremony.firstPawn == pawn || ceremony.secondPawn == pawn) return null;
                return "PS_AttendWedding";
            }

            return null;
        }

        private void AskToAttendGathering(Lord lord, string key)
        {
            int id = lord.loadID;
            promptedGatherings.Add(id);
            gatheringPromptOpen = true;

            void Accept() => gatheringPromptOpen = false;

            void Decline()
            {
                declinedGatherings.Add(id);
                gatheringPromptOpen = false;
            }

            var dialog = new Dialog_MessageBox(key.Translate(),
                "Yes".Translate(), Accept,
                "No".Translate(), Decline,
                null, false, Accept, Decline);

            dialog.forcePause = true;
            Find.WindowStack.Add(dialog);
        }
    }
}
