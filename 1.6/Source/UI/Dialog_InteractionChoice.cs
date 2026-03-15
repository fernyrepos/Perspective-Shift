using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace PerspectiveShift
{
    [HotSwappable]
    public class Dialog_InteractionChoice : Window
    {
        private struct InteractionProfile
        {
            public string titleKey, acceptKey, acceptDescKey, rejectKey, rejectDescKey;
        }

        private static readonly Dictionary<InteractionDef, InteractionProfile> Profiles = new Dictionary<InteractionDef, InteractionProfile>
        {
            { InteractionDefOf.RomanceAttempt, new InteractionProfile {
                titleKey = "PS_RomanceAdvanceTitle", acceptKey = "PS_Reciprocate", acceptDescKey = "PS_Reciprocate_Desc",
                rejectKey = "PS_Rebuff", rejectDescKey = "PS_Rebuff_Desc" } },
            { InteractionDefOf.MarriageProposal, new InteractionProfile {
                titleKey = "PS_MarriageProposalTitle", acceptKey = "PS_Yes", acceptDescKey = "PS_Yes_Desc",
                rejectKey = "PS_No", rejectDescKey = "PS_No_Desc" } }
        };

        private Pawn initiator, recipient;
        private InteractionDef intDef;
        private InteractionProfile profile;

        public override Vector2 InitialSize => new Vector2(800f, 550f);

        public Dialog_InteractionChoice(Pawn initiator, Pawn recipient, InteractionDef intDef)
        {
            this.initiator = initiator;
            this.recipient = recipient;
            this.intDef = intDef;
            this.profile = Profiles.TryGetValue(intDef, out var p) ? p : default;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnCancel = false;
            doCloseX = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            const float btnW = 185f, rowH = 60f, descOffset = 199f, portraitSize = 150;
            inRect = inRect.ContractedBy(50f, 20f);

            Text.Anchor = TextAnchor.MiddleCenter;
            var titleStyle = new GUIStyle(Text.CurFontStyle) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 60f), profile.titleKey.Translate(initiator.LabelShort).Resolve());

            var portraitRect = new Rect(inRect.center.x - portraitSize / 2f, inRect.y + 70f, portraitSize, portraitSize);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(initiator, new Vector2(portraitSize, portraitSize), Rot4.South, cameraZoom: 1.5f, healthStateOverride: PawnHealthState.Mobile));

            Text.Anchor = TextAnchor.UpperLeft;
            float y = portraitRect.yMax + 30f;

            DrawChoice(inRect, ref y, btnW, rowH, descOffset, profile.acceptKey.Translate(), profile.acceptDescKey.Translate(), State.ForcedInteractionOutcome.ForceAccept);
            DrawChoice(inRect, ref y, btnW, rowH, descOffset, profile.rejectKey.Translate(), profile.rejectDescKey.Translate(), State.ForcedInteractionOutcome.ForceReject);
            DrawChoice(inRect, ref y, btnW, rowH, descOffset, "PS_GoWithYourGut".Translate(), "PS_GoWithYourGut_Desc".Translate(), State.ForcedInteractionOutcome.None);
        }

        private void DrawChoice(Rect inRect, ref float y, float w, float h, float off, string label, string desc, State.ForcedInteractionOutcome outcome)
        {
            if (Widgets.ButtonText(new Rect(inRect.x, y + 2, w, h - 4), label)) Execute(outcome);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x + off, y, inRect.width - off, h), desc);
            Text.Anchor = TextAnchor.UpperLeft;
            y += h;
        }

        private void Execute(State.ForcedInteractionOutcome outcome)
        {
            State.forcedInteraction = outcome;
            State.skipDialog = true;
            initiator.interactions.TryInteractWith(recipient, intDef);
            State.skipDialog = false;
            State.forcedInteraction = State.ForcedInteractionOutcome.None;
            Close();
        }
    }
}
