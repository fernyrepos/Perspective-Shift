using RimWorld;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PerspectiveShift
{
    [StaticConstructorOnStartup]
    [HotSwappable]
    public static class State
    {
        public static HashSet<int> seekAtWillPawns = new HashSet<int>();
        private static bool _isActiveCache;
        private static int _isActiveCacheFrame = -999;
        public static bool ShouldSeekEnemy(this Pawn pawn)
        {
            seekAtWillPawns ??= new HashSet<int>();
            return !pawn.IsAvatar() && pawn.InMentalState is false && pawn.Drafted is false && pawn.Faction == Faction.OfPlayer && !pawn.RaceProps.Animal && seekAtWillPawns.Contains(pawn.thingIDNumber);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAvatar(this Pawn pawn)
        {
            return Avatar?.pawn == pawn;
        }
        public static bool IsUnderAIControl(this Pawn pawn)
        {
            return pawn.GetLord() != null || pawn.mindState?.duty != null;
        }
        public enum ForcedInteractionOutcome { None, ForceAccept, ForceReject }

        public static bool fulfillNeeds = false;
        public static bool returnAfterFulfillment = true;
        public static bool showJobCallouts = true;
        public static bool playDoneSound = true;
        public static HashSet<AutoNeed> disabledAutoNeeds = new HashSet<AutoNeed>();

        public static bool IsAutoNeedEnabled(AutoNeed need)
        {
            disabledAutoNeeds ??= new HashSet<AutoNeed>();
            return !disabledAutoNeeds.Contains(need);
        }

        public static void SetAutoNeedEnabled(AutoNeed need, bool enabled)
        {
            disabledAutoNeeds ??= new HashSet<AutoNeed>();
            if (enabled) disabledAutoNeeds.Remove(need);
            else disabledAutoNeeds.Add(need);
        }

        public static Dictionary<AutoNeed, float> autoNeedThresholds = new Dictionary<AutoNeed, float>();

        public static float DefaultAutoNeedThreshold(AutoNeed need)
        {
            switch (need)
            {
                case AutoNeed.Hygiene: return 0.2f;
                case AutoNeed.Recreation: return 0.35f;
                default: return 0.3f;
            }
        }

        public static float GetAutoNeedThreshold(AutoNeed need)
        {
            autoNeedThresholds ??= new Dictionary<AutoNeed, float>();
            return autoNeedThresholds.TryGetValue(need, out float value) ? value : DefaultAutoNeedThreshold(need);
        }

        public static void SetAutoNeedThreshold(AutoNeed need, float value)
        {
            autoNeedThresholds ??= new Dictionary<AutoNeed, float>();
            autoNeedThresholds[need] = Mathf.Clamp(value, 0.05f, 1f);
        }
        public static bool TryApplyForcedInteraction(ref float __result)
        {
            if (forcedInteraction == ForcedInteractionOutcome.ForceAccept)
            {
                __result = 2f;
                return false;
            }
            if (forcedInteraction == ForcedInteractionOutcome.ForceReject)
            {
                __result = -1f;
                return false;
            }
            return true;
        }

        public static int authenticPawnId = -1;
        public static Avatar Avatar;
        public static PlaystyleMode CurrentMode = PlaystyleMode.Director;
        public static bool DrawingTopRightGizmos = false;
        private static CameraMapConfig _savedConfig;
        public static Vector3? CameraLockPosition;
        public const int CameraRecenterFrames = 300;
        public static int cameraRecenterFrames;
        public static int cameraRecenterSettled = -1;
        public static bool cameraRecenterSnap;
        public static bool skipDialog = false;
        public static ForcedInteractionOutcome forcedInteraction = ForcedInteractionOutcome.None;
        public static bool pendingDeathMenu = false;
        public static bool permadeath = false;
        public static bool allowDirectorInAuthentic = false;
        public static bool shownControlsDialog = false;
        private const float ControlsDialogDelay = 10f;
        private static float controlsDialogDueAt = -1f;
        private const float SleepFadeDuration = 1.5f;
        private static float sleepFade;
        private static float lastSleepFadeTime = -1f;
        public static float lastDamageTime = -999f;
        private const float DamageFlashDuration = 0.2f;
        private const float DamageFlashPeak = 0.4f;
        private const float DamageVignetteDuration = 0.35f;
        private const float DamageVignettePeak = 0.65f;
        private static Texture2D _damageVignetteTex;
        private static Texture2D DamageVignetteTex => _damageVignetteTex ??= ContentFinder<Texture2D>.Get("UI/DamageVignette");
        public static bool IsActive
        {
            get
            {
                if (Avatar?.pawn is null) return false;
                if (Time.frameCount != _isActiveCacheFrame)
                {
                    _isActiveCache = Avatar?.pawn != null && !Avatar.pawn.Dead && !WorldComponent_GravshipController.CutsceneInProgress;
                    _isActiveCacheFrame = Time.frameCount;
                }
                return _isActiveCache;
            }
        }

        public static bool ControlsFrozen
        {
            get
            {
                if (Find.WindowStack.WindowsPreventCameraMotion) return true;
                if (GUI.GetNameOfFocusedControl() != "") return true;

                if (Avatar?.pawn?.CurJob != null && !Avatar.pawn.CurJob.def.playerInterruptible) return true;

                return false;
            }
        }

        public static void SetAvatar(Pawn pawn, bool showMessage = false)
        {
            pendingDeathMenu = false;
            if (CurrentMode == PlaystyleMode.Authentic && authenticPawnId == -1)
                authenticPawnId = pawn.thingIDNumber;

            if (Avatar?.pawn != null && Avatar.pawn != pawn)
            {
                CleanupPawnState(Avatar.pawn);
            }

            Avatar = new Avatar(pawn);
            CameraLockPosition = null;
            if (pawn.jobs != null && pawn.Spawned)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                pawn.pather.StopDead();
                var wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = 60;
                wait.checkOverrideOnExpire = true;
                pawn.jobs.TryTakeOrderedJob(wait);
                var lord = pawn.GetLord();
                if (lord != null)
                {
                    Avatar.savedLord = lord;
                    lord.Notify_PawnLost(pawn, PawnLostCondition.Undefined);
                }
            }
            if (showMessage)
                Messages.Message("PS_ControlTaken".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NeutralEvent);
        }

        public static void ClearAvatar()
        {
            if (Avatar?.pawn != null)
            {
                CleanupPawnState(Avatar.pawn);
            }

            if (Find.CameraDriver != null)
            {
                if (_savedConfig != null)
                {
                    Find.CameraDriver.config = _savedConfig;
                    _savedConfig = null;
                }
                else if (Find.CameraDriver.config is CameraMapConfig_Avatar)
                {
                    Find.CameraDriver.config = new CameraMapConfig_Normal();
                }
                ModCompatibility.ResetSimpleCameraSetting();
            }
            CameraLockPosition = null;
            Avatar = null;
            controlsDialogDueAt = -1f;
            Cursor.visible = true;
        }

        private static void CleanupPawnState(Pawn pawn)
        {
            if (pawn.drafter == null) return;
            if (pawn.Drafted)
            {
                pawn.drafter.Drafted = false;
            }

            if (Avatar?.savedLord != null && Avatar.savedLord.lordManager != null && Avatar.savedLord.lordManager.lords.Contains(Avatar.savedLord))
            {
                Avatar.savedLord.AddPawn(pawn);
                Avatar.savedLord.CurLordToil?.UpdateAllDuties();
            }
            Avatar.savedLord = null;
        }

        public static void RevokeControl(Pawn pawn, DamageInfo? dinfo = null, Hediff hediff = null)
        {
            if (Avatar?.pawn != pawn)
            {
                return;
            }
            if (CurrentMode == PlaystyleMode.Authentic)
            {
                if (pawn.Dead || pawn.IsKidnapped())
                {
                    pendingDeathMenu = true;
                    Find.WindowStack.Add(new Dialog_YouDied(pawn, dinfo, hediff));
                }
                return;
            }
            ClearAvatar();
            Messages.Message("PS_LostControl".Translate(), MessageTypeDefOf.NegativeEvent);
        }

        public static bool IsActiveOrInVehicle
        {
            get
            {
                if (IsActive) return true;
                if (Avatar?.pawn == null) return false;
                if (Avatar.pawn.Dead || WorldComponent_GravshipController.CutsceneInProgress) return false;
                return ModCompatibility.IsPawnInVehicle(Avatar.pawn, out _, out _, out _);
            }
        }

        public static void Update()
        {
            if (!IsActiveOrInVehicle)
            {
                if (!Cursor.visible) Cursor.visible = true;
                return;
            }
            Avatar.RenderPawn();
            if (Find.TickManager.Paused)
            {
                return;
            }
            Avatar.UpdatePhysics();
        }

        public static void Tick()
        {
            if (!IsActiveOrInVehicle) return;

            Avatar.Tick();
        }

        public static void OnGUI()
        {
            if (!IsActiveOrInVehicle)
            {
                if (!Cursor.visible) Cursor.visible = true;
                return;
            }
            if (Find.CameraDriver == null) return;

            bool pawnSpawned = Avatar.pawn.Spawned;
            bool mapMatch = Avatar.pawn.Map != null && Avatar.pawn.Map == Find.CurrentMap;
            bool pawnReady = pawnSpawned && mapMatch;

            if (!pawnReady)
            {
                Cursor.visible = true;
                var container = TryGetSpawnedContainer(Avatar.pawn);
                if (container != null && container.Map == Find.CurrentMap)
                {
                    if (Find.CameraDriver.config is not CameraMapConfig_Avatar)
                    {
                        _savedConfig = Find.CameraDriver.config;
                        Find.CameraDriver.config = new CameraMapConfig_Avatar();
                    }
                    Find.CameraDriver.rootPos = container.DrawPos;
                    if (ModCompatibility.IsPawnInVehicle(Avatar.pawn, out _, out _, out _))
                        Avatar.OnGUI();
                }
                else
                {
                    if (Find.CameraDriver.config is CameraMapConfig_Avatar && _savedConfig == null)
                        Warning("OnGUI - tried to restore config but _savedConfig is null");
                    if (Find.CameraDriver.config is CameraMapConfig_Avatar && _savedConfig != null)
                    {
                        Find.CameraDriver.config = _savedConfig;
                        _savedConfig = null;
                    }
                }
                return;
            }

            if (Find.CameraDriver.config is not CameraMapConfig_Avatar)
            {
                _savedConfig = Find.CameraDriver.config;
                Find.CameraDriver.config = new CameraMapConfig_Avatar();
            }

            if (!shownControlsDialog && PerspectiveShiftMod.settings.showControlsOnFirstInhabit)
            {
                if (controlsDialogDueAt < 0f)
                {
                    controlsDialogDueAt = Time.realtimeSinceStartup + ControlsDialogDelay;
                }
                else if (Time.realtimeSinceStartup >= controlsDialogDueAt)
                {
                    shownControlsDialog = true;
                    Find.WindowStack.Add(new Dialog_InhabitedControls());
                }
            }

            Avatar.OnGUI();

            if (PerspectiveShiftMod.settings.enableDamageScreenEffect && lastDamageTime > 0f)
            {
                bool vignette = PerspectiveShiftMod.settings.damageEffectStyle == DamageEffectStyle.Vignette;
                float duration = vignette ? DamageVignetteDuration : DamageFlashDuration;
                float timeSinceDamage = Time.realtimeSinceStartup - lastDamageTime;
                if (timeSinceDamage < duration)
                {
                    Color prevColor = GUI.color;
                    GUI.color = new Color(1f, 0f, 0f, Mathf.Lerp(vignette ? DamageVignettePeak : DamageFlashPeak, 0f, timeSinceDamage / duration));
                    GUI.DrawTexture(new Rect(0, 0, UI.screenWidth, UI.screenHeight), vignette ? DamageVignetteTex : BaseContent.WhiteTex);
                    GUI.color = prevColor;
                }
            }
        }

        public static void DrawSleepOverlay()
        {
            if (!PerspectiveShiftMod.settings.sleepingPreventsVision) return;

            if (!IsActive || !Avatar.pawn.Spawned || Avatar.pawn.Map != Find.CurrentMap)
            {
                sleepFade = 0f;
                lastSleepFadeTime = -1f;
                return;
            }

            var target = Avatar.pawn.Awake() ? 0f : 1f;
            if (target <= 0f && sleepFade <= 0f)
            {
                lastSleepFadeTime = -1f;
                return;
            }

            var now = Time.realtimeSinceStartup;
            var delta = lastSleepFadeTime < 0f ? 0f : Mathf.Min(now - lastSleepFadeTime, 0.25f);
            lastSleepFadeTime = now;
            sleepFade = Mathf.MoveTowards(sleepFade, target, delta / SleepFadeDuration);
            if (sleepFade <= 0f) return;

            GUI.color = new Color(1f, 1f, 1f, sleepFade);
            GUI.DrawTexture(new Rect(0f, 0f, UI.screenWidth, UI.screenHeight), BaseContent.BlackTex);
            GUI.color = Color.white;
        }

        public static Thing TryGetSpawnedContainer(Pawn pawn)
        {
            if (pawn == null) return null;
            IThingHolder holder = pawn.ParentHolder;
            while (holder != null)
            {
                if (holder is Thing t && t.Spawned)
                    return t;

                if (holder is ThingComp comp && comp.parent != null && comp.parent.Spawned)
                    return comp.parent;

                holder = holder.ParentHolder;
            }
            return null;
        }

        public static bool CanUseIt(this Pawn pawn, Thing thing)
        {
            if (!IsActive || Avatar?.pawn == null || pawn.IsAvatar()) return true;
            var avatar = Avatar.pawn;
            var job = avatar.CurJob;
            if (job?.def == JobDefOf.DoBill)
            {
                if (job.targetB.Thing == thing || job.targetC.Thing == thing ||
                    (job.targetQueueB != null && job.targetQueueB.Any(t => t.Thing == thing)) ||
                    (job.placedThings != null && job.placedThings.Any(pt => pt.thing == thing)))
                    return false;
            }
            return true;
        }

        public static void TryRecenterCameraAfterLoad()
        {
            if (cameraRecenterFrames <= 0) return;
            if (LongEventHandler.ShouldWaitForEvent) return;
            if (Find.CurrentMap == null) return;

            var pawn = Avatar?.pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map != Find.CurrentMap) return;

            cameraRecenterFrames--;

            if (CameraLockPosition.HasValue)
            {
                CameraLockPosition = null;
                cameraRecenterSettled = 0;
                cameraRecenterSnap = true;
                return;
            }

            if (cameraRecenterSettled >= 0 && ++cameraRecenterSettled >= 3) cameraRecenterFrames = 0;
        }

        public static bool ConsumeCameraRecenterSnap()
        {
            if (!cameraRecenterSnap) return false;
            cameraRecenterSnap = false;
            return true;
        }

        public static void Message(string message)
        {
            Log.ResetMessageCount();
            Log.Message($"[PS] {message}");
        }

        public static void Warning(string message)
        {
            Log.ResetMessageCount();
            Log.Warning($"[PS] {message}");
        }

        public static void Error(string message)
        {
            Log.ResetMessageCount();
            Log.Error($"[PS] {message}");
        }
    }
}
