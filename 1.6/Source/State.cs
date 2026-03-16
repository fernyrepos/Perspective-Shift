using RimWorld;
using System.Collections.Generic;
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
        public enum ForcedInteractionOutcome { None, ForceAccept, ForceReject }
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

        public static Avatar Avatar;
        public static PlaystyleMode CurrentMode = PlaystyleMode.Director;
        public static bool DrawingTopRightGizmos = false;
        private static CameraMapConfig _savedConfig;
        public static Vector3? CameraLockPosition;
        public static bool skipDialog = false;
        public static ForcedInteractionOutcome forcedInteraction = ForcedInteractionOutcome.None;
        public static bool pendingDeathMenu = false;
        public static bool permadeath = false;
        public static bool allowDirectorInAuthentic = false;
        public static bool IsActive => Avatar != null && Avatar.pawn != null && !Avatar.pawn.Dead
            && !WorldComponent_GravshipController.CutsceneInProgress;
        public static Avatar Current => Avatar;
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
            if (Avatar?.pawn != null && Avatar.pawn != pawn)
            {
                CleanupPawnState(Avatar.pawn);
            }

            Message($"PerspectiveState.SetAvatar - Setting avatar to {pawn.Name}, Mode: {CurrentMode}");
            Avatar = new Avatar(pawn);
            CameraLockPosition = null;
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

            if (showMessage)
                Messages.Message("PS_ControlTaken".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NeutralEvent);
        }

        public static void ClearAvatar()
        {
            Message($"PerspectiveState.ClearAvatar - Clearing avatar");

            if (Avatar?.pawn != null)
            {
                CleanupPawnState(Avatar.pawn);
            }

            if (_savedConfig != null && Find.CameraDriver != null)
            {
                Find.CameraDriver.config = _savedConfig;
                _savedConfig = null;
            }
            CameraLockPosition = null;
            Avatar = null;
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
                if (pawn.Dead)
                {
                    pendingDeathMenu = true;
                    Find.WindowStack.Add(new Dialog_YouDied(pawn, dinfo, hediff));
                }
                return;
            }
            Message($"EmergencyRevokeControl - revoking from {pawn.Name}");
            ClearAvatar();
            Messages.Message("PS_LostControl".Translate(), MessageTypeDefOf.NegativeEvent);
        }

        public static void Update()
        {
            if (!IsActive)
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
            if (!IsActive) return;

            Avatar.Tick();
        }

        public static void OnGUI()
        {
            if (!IsActive)
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
                    if (!(Find.CameraDriver.config is CameraMapConfig_Avatar))
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

            if (!(Find.CameraDriver.config is CameraMapConfig_Avatar))
            {
                _savedConfig = Find.CameraDriver.config;
                Find.CameraDriver.config = new CameraMapConfig_Avatar();
            }

            Avatar.OnGUI();
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

        public static bool IsAvatar(this Pawn pawn) =>
            IsActive && pawn == Avatar.pawn;

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
