using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PerspectiveShift
{
    public class Dialog_FishingMinigame : Window
    {
        private const float TrackWidth = 46f;
        private const float TrackHeight = 440f;
        private const float ProgressWidth = 18f;
        private const float ColumnGap = 8f;
        private const float StartProgress = 0.5f;
        private const float StartGrace = 0.8f;

        private const float FishHitHeight = 32f / TrackHeight;

        private const float Gravity = -2.81f;
        private const float Lift = 5.73f;
        private const float MaxSpeed = 1.49f;
        private const float Bounce = 0.28f;

        private const float FishPull = 4.6f;
        private const float FishSmoothing = 9f;
        private const float FishRetargetMin = 0.28f;
        private const float FishRetargetMax = 0.95f;

        private static readonly Color TrackColor = new Color(0.09f, 0.11f, 0.14f);
        private static readonly Color TrackEdgeColor = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color BarColor = new Color(0.44f, 0.72f, 0.42f, 0.55f);
        private static readonly Color BarEdgeColor = new Color(0.62f, 0.88f, 0.60f, 0.85f);
        private static readonly Color ProgressColor = new Color(0.50f, 0.78f, 0.46f);
        private static readonly Color ProgressLowColor = new Color(0.85f, 0.53f, 0.53f);
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.45f);

        private readonly Pawn pawn;
        private readonly IntVec3 cell;
        private readonly ThingDef fishDef;
        private readonly Texture2D fishIcon;

        private float barHeight;
        private float barPos;
        private float barVel;

        private float fishPos;
        private float fishVel;
        private float fishTarget;
        private float fishRetargetLeft;
        private float fishSpeed;

        private float progress = StartProgress;
        private float graceLeft = StartGrace;
        private float gainRate;
        private float drainRate;

        private Sustainer reelSustainer;
        private SoundDef reelSustainerDef;

        private bool resolved;
        private bool won;
        private float resultTimer;

        public Dialog_FishingMinigame(Pawn pawn, IntVec3 cell)
        {
            this.pawn = pawn;
            this.cell = cell;
            fishDef = PSFishingUtility.PreviewFishDef(cell, pawn.Map);
            fishIcon = SingleItemIcon(fishDef);

            forcePause = true;
            absorbInputAroundWindow = true;
            preventCameraMotion = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            doCloseX = false;
            doCloseButton = false;
            draggable = false;
            onlyOneOfTypeAllowed = true;

            float skill = Mathf.Clamp(PSFishingUtility.FishingSkill(pawn), 0f, 20f) / 20f;
            barHeight = Mathf.Lerp(0.17f, 0.36f, skill);
            gainRate = Mathf.Lerp(0.21f, 0.33f, skill);
            drainRate = gainRate;
            fishSpeed = Mathf.Lerp(1.44f, 1.10f, skill);

            barPos = 0.5f - barHeight / 2f;
            fishPos = 0.5f;
            fishTarget = 0.5f;
        }

        public override Vector2 InitialSize => new Vector2(
            TrackWidth + ProgressWidth + ColumnGap + Margin * 2f + 8f,
            TrackHeight + Margin * 2f + 68f);

        public override float Margin => 12f;

        public override void PreClose()
        {
            base.PreClose();
            StopReelLoop();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                if (resolved) resultTimer -= dt;
                else Reel(dt, Input.GetMouseButton(0));
            }

            var titleRect = new Rect(inRect.x, inRect.y, inRect.width, 26f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(titleRect, resolved
                ? (won ? "PS_FishingLanded".Translate() : "PS_FishingEscaped".Translate())
                : "PS_FishingMinigameTitle".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            float top = titleRect.yMax + 6f;
            var progressRect = new Rect(inRect.x + 4f, top, ProgressWidth, TrackHeight);
            var trackRect = new Rect(progressRect.xMax + ColumnGap, top, TrackWidth, TrackHeight);

            DrawProgress(progressRect);
            DrawTrack(trackRect);

            var hintRect = new Rect(inRect.x, trackRect.yMax + 4f, inRect.width, 22f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = HintColor;
            if (!resolved) Widgets.Label(hintRect, "PS_FishingReelHint".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (resolved && resultTimer <= 0f) Close(false);
        }

        private void Reel(float dt, bool held)
        {
            barVel += (held ? Lift : Gravity) * dt;
            barVel = Mathf.Clamp(barVel, -MaxSpeed, MaxSpeed);
            barPos += barVel * dt;

            float barMax = 1f - barHeight;
            if (barPos <= 0f)
            {
                barPos = 0f;
                if (barVel < 0f) barVel = -barVel * Bounce;
            }
            else if (barPos >= barMax)
            {
                barPos = barMax;
                if (barVel > 0f) barVel = -barVel * Bounce;
            }

            fishRetargetLeft -= dt;
            if (fishRetargetLeft <= 0f)
            {
                fishRetargetLeft = UnityEngine.Random.Range(FishRetargetMin, FishRetargetMax);
                fishTarget = UnityEngine.Random.value < 0.22f
                    ? UnityEngine.Random.value
                    : Mathf.Clamp01(fishPos + UnityEngine.Random.Range(-0.45f, 0.45f));
            }

            fishVel = Mathf.Lerp(fishVel, (fishTarget - fishPos) * FishPull, 1f - Mathf.Exp(-FishSmoothing * dt));
            fishVel = Mathf.Clamp(fishVel, -fishSpeed, fishSpeed);
            fishPos = Mathf.Clamp01(fishPos + fishVel * dt);

            float fishLow = fishPos * (1f - FishHitHeight);
            bool onFish = fishLow + FishHitHeight >= barPos && fishLow <= barPos + barHeight;
            UpdateReelLoop(onFish ? DefsOf.PS_ReelFast : (held ? DefsOf.PS_ReelSlow : null));

            if (graceLeft > 0f)
            {
                graceLeft -= dt;
                return;
            }

            progress += (onFish ? gainRate : -drainRate) * dt;
            progress = Mathf.Clamp01(progress);

            if (progress >= 1f) Resolve(true);
            else if (progress <= 0f) Resolve(false);
        }

        private void UpdateReelLoop(SoundDef def)
        {
            if (def != reelSustainerDef)
            {
                StopReelLoop();
                reelSustainerDef = def;
                if (def != null) reelSustainer = def.TrySpawnSustainer(SoundInfo.OnCamera(MaintenanceType.PerFrame));
            }
            if (reelSustainer != null && !reelSustainer.Ended) reelSustainer.Maintain();
        }

        private void StopReelLoop()
        {
            if (reelSustainer != null && !reelSustainer.Ended) reelSustainer.End();
            reelSustainer = null;
            reelSustainerDef = null;
        }

        private void Resolve(bool success)
        {
            StopReelLoop();
            resolved = true;
            won = success;

            if (success)
            {
                resultTimer = 0.55f;
                pawn.skills?.Learn(SkillDefOf.Animals, 260f);
                PSFishingUtility.ResolveCatch(pawn, cell);
            }
            else
            {
                resultTimer = 1.1f;
                pawn.skills?.Learn(SkillDefOf.Animals, 60f);
                DefsOf.PS_FishEscape.PlayOneShotOnCamera();
                Messages.Message("PS_FishingEscapedMessage".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
            }
        }

        private void DrawProgress(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, TrackColor);
            float h = rect.height * progress;
            var fill = new Rect(rect.x, rect.yMax - h, rect.width, h);
            Widgets.DrawBoxSolid(fill, progress < 0.25f ? ProgressLowColor : ProgressColor);
            GUI.color = TrackEdgeColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        private static Texture2D SingleItemIcon(ThingDef def)
        {
            if (def == null) return null;

            var graphic = def.graphic;
            if (graphic is Graphic_StackCount stackGraphic) graphic = stackGraphic.SubGraphicForStackCount(1, def);

            return graphic?.MatSingle?.mainTexture as Texture2D ?? def.uiIcon;
        }

        private void DrawTrack(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, TrackColor);

            float barPix = rect.height * barHeight;
            float barY = rect.yMax - (barPos + barHeight) * rect.height;
            var bar = new Rect(rect.x + 2f, barY, rect.width - 4f, barPix);
            Widgets.DrawBoxSolid(bar, BarColor);
            GUI.color = BarEdgeColor;
            Widgets.DrawBox(bar);
            GUI.color = Color.white;

            float iconSize = rect.height * FishHitHeight;
            float fishY = rect.yMax - iconSize - fishPos * (rect.height - iconSize);
            var fishRect = new Rect(rect.center.x - iconSize / 2f, fishY, iconSize, iconSize);
            if (fishIcon != null)
            {
                GUI.color = fishDef.uiIconColor;
                GUI.DrawTexture(fishRect, fishIcon);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBoxSolid(fishRect, Color.white);
            }

            GUI.color = TrackEdgeColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }
    }
}
