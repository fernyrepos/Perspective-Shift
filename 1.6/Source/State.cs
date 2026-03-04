using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [StaticConstructorOnStartup]
    [HotSwappable]
    public static class State
    {
        public static Avatar Avatar;
        public static PlaystyleMode CurrentMode = PlaystyleMode.Director;
        public static bool DrawingTopRightGizmos = false;
        private static CameraMapConfig _savedConfig;
        public static bool IsActive => Avatar != null && Avatar.pawn != null && !Avatar.pawn.Dead;
        public static Avatar Current => Avatar;
        public static bool ControlsFrozen
        {
            get
            {
                if (Find.WindowStack.WindowsPreventCameraMotion) return true;
                if (GUI.GetNameOfFocusedControl() != "") return true;
                var openTab = Find.MainTabsRoot.OpenTab;
                if (openTab != null && openTab != MainButtonDefOf.Inspect) return true;
                return false;
            }
        }

        public static void SetAvatar(Pawn pawn, bool showMessage = false)
        {
            if (pawn == null) return;

            Message($"PerspectiveState.SetAvatar - Setting avatar to {pawn.Name}, Mode: {CurrentMode}");
            Avatar = new Avatar(pawn);

            if (showMessage)
                Messages.Message("PS_ControlTaken".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NeutralEvent);
        }

        public static void ClearAvatar()
        {
            Message($"PerspectiveState.ClearAvatar - Clearing avatar");
            if (_savedConfig != null && Find.CameraDriver != null)
            {
                Find.CameraDriver.config = _savedConfig;
                _savedConfig = null;
            }
            Avatar = null;
            Cursor.visible = true;
        }

        public static void RevokeControl(Pawn pawn)
        {
            if (Avatar?.pawn != pawn)
            {
                return;
            }
            Message($"EmergencyRevokeControl - revoking from {pawn.Name}");
            ClearAvatar();
            Messages.Message("PS_LostControl".Translate(), MessageTypeDefOf.NegativeEvent);
        }

        public static void Update()
        {
            if (!IsActive) return;
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
            if (!IsActive) return;
            if (Find.CameraDriver == null) return;

            bool pawnSpawned = Avatar.pawn.Spawned;
            bool mapMatch = Avatar.pawn.Map != null && Avatar.pawn.Map == Find.CurrentMap;
            bool pawnReady = pawnSpawned && mapMatch;

            if (!pawnReady)
            {
                var container = TryGetSpawnedContainer(Avatar.pawn);
                if (container != null)
                {
                    if (!(Find.CameraDriver.config is CameraMapConfig_Avatar))
                    {
                        _savedConfig = Find.CameraDriver.config;
                        Find.CameraDriver.config = new CameraMapConfig_Avatar();
                    }
                    Find.CameraDriver.rootPos = container.DrawPos;
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

        private static Thing TryGetSpawnedContainer(Pawn pawn)
        {
            if (pawn == null) return null;
            IThingHolder holder = pawn.ParentHolder;
            while (holder != null)
            {
                if (holder is Thing t && t.Spawned && t.Map == Find.CurrentMap)
                    return t;
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
