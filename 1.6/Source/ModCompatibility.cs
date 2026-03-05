using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace PerspectiveShift
{
    [StaticConstructorOnStartup]
    public static class ModCompatibility
    {
        public static readonly bool GiddyUpAvailable;
        public static readonly bool RunAndGunAvailable;
        public static readonly bool VehicleFrameworkAvailable;
        public static readonly bool SimpleCameraSettingAvailable;

        private static Type scsModSettingType;
        private static FieldInfo scsModSettingField;
        private static Type vehiclePawnType;
        private static Type vehiclePathFollowerType;
        private static MethodInfo startPathMethod;
        private static FieldInfo vehiclePatherField;
        private static bool vehicleCompatInitialized;
        private static MethodInfo getExtendedPawnDataMethod;
        private static PropertyInfo mountProperty;
        private static PropertyInfo reservedByProperty;
        private static Type compRunAndGunType;
        private static PropertyInfo isEnabledProperty;
        public static Type stanceRunAndGunType;
        public static Type stanceRunAndGunCooldownType;

        private static FieldInfo effecterField;
        private static FieldInfo sustainerField;

        static ModCompatibility()
        {
            GiddyUpAvailable = ModsConfig.IsActive("MemeGoddess.GiddyUp");
            RunAndGunAvailable = ModsConfig.IsActive("MemeGoddess.RunAndGun") || ModsConfig.IsActive("roolo.RunAndGun");
            VehicleFrameworkAvailable = ModsConfig.IsActive("SmashPhil.VehicleFramework");
            SimpleCameraSettingAvailable = ModsConfig.IsActive("ray1203.SimpleCameraSetting");

            InitGiddyUpCompat();
            InitVehicleCompat();
            InitRunAndGunCompat();
            InitSCSCompat();
        }

        public static void InitSCSCompat()
        {
            if (!SimpleCameraSettingAvailable) return;

            scsModSettingType = AccessTools.TypeByName("SimpleCameraSetting.SimpleCameraModSetting");
            if (scsModSettingType == null)
            {
                Log.Error("[PerspectiveShift] Failed to find SimpleCameraSetting.SimpleCameraModSetting type");
                return;
            }
            scsModSettingField = AccessTools.Field(scsModSettingType, "modSetting");
            if (scsModSettingField == null)
            {
                Log.Error("[PerspectiveShift] Failed to find SimpleCameraSetting.SimpleCameraModSetting.modSetting field");
            }
        }

        public static void InitRunAndGunCompat()
        {
            if (!RunAndGunAvailable) return;

            compRunAndGunType = AccessTools.TypeByName("RunAndGun.CompRunAndGun");
            if (compRunAndGunType != null)
                isEnabledProperty = AccessTools.Property(compRunAndGunType, "isEnabled");
            else
                Log.Error("[PerspectiveShift] Failed to find RunAndGun.CompRunAndGun type");

            stanceRunAndGunType = AccessTools.TypeByName("RunAndGun.Stance_RunAndGun");
            stanceRunAndGunCooldownType = AccessTools.TypeByName("RunAndGun.Stance_RunAndGun_Cooldown");

            effecterField = AccessTools.Field(typeof(Stance_Warmup), "effecter");
            sustainerField = AccessTools.Field(typeof(Stance_Warmup), "sustainer");
        }

        public static bool IsRunAndGunActiveFor(Pawn pawn, out string reason)
        {
            reason = "Unknown";
            if (!RunAndGunAvailable)
            {
                reason = "RunAndGun mod not active in ModsConfig";
                return false;
            }
            if (compRunAndGunType == null || isEnabledProperty == null)
            {
                reason = "RunAndGun reflection failed (comp type or isEnabled property is null)";
                return false;
            }

            var comp = pawn.AllComps.FirstOrDefault(c => compRunAndGunType.IsAssignableFrom(c.GetType()));
            if (comp == null)
            {
                reason = "Pawn lacks CompRunAndGun";
                return false;
            }

            var enabled = (bool)isEnabledProperty.GetValue(comp, null);
            if (!enabled)
            {
                var disabledField = AccessTools.Field(compRunAndGunType, "_disabled");
                var isEnabledField = AccessTools.Field(compRunAndGunType, "_isEnabled");
                bool _disabled = disabledField != null && (bool)disabledField.GetValue(comp);
                bool _isEnabled = isEnabledField != null && (bool)isEnabledField.GetValue(comp);

                reason = $"isEnabled is false. Details: _disabled={_disabled} (e.g. invalid weapon), _isEnabled={_isEnabled} (gizmo toggled)";
                return false;
            }

            reason = "Active";
            return true;
        }

        public static bool IsRunAndGunActiveFor(Pawn pawn)
        {
            return IsRunAndGunActiveFor(pawn, out _);
        }

        public static void ConvertToRunAndGunStance(Pawn pawn, Stance_Warmup warmup)
        {
            if (stanceRunAndGunType == null) return;
            var newStance = (Stance_Warmup)Activator.CreateInstance(stanceRunAndGunType, new object[] { warmup.ticksLeft, warmup.focusTarg, warmup.verb });
            newStance.stanceTracker = pawn.stances;
            if (effecterField != null) effecterField.SetValue(newStance, effecterField.GetValue(warmup));
            if (sustainerField != null) sustainerField.SetValue(newStance, sustainerField.GetValue(warmup));

            pawn.stances.curStance = newStance;
        }

        public static void ConvertToRunAndGunCooldownStance(Pawn pawn, Stance_Cooldown cooldown)
        {
            if (stanceRunAndGunCooldownType == null) return;
            var newStance = (Stance_Cooldown)Activator.CreateInstance(stanceRunAndGunCooldownType, new object[] { cooldown.ticksLeft, cooldown.focusTarg, cooldown.verb });
            newStance.stanceTracker = pawn.stances;
            pawn.stances.curStance = newStance;
        }

        public static void ConvertToVanillaWarmupStance(Pawn pawn, Stance_Warmup runAndGunStance)
        {
            var newStance = new Stance_Warmup(runAndGunStance.ticksLeft, runAndGunStance.focusTarg, runAndGunStance.verb);
            newStance.stanceTracker = pawn.stances;
            if (effecterField != null) effecterField.SetValue(newStance, effecterField.GetValue(runAndGunStance));
            if (sustainerField != null) sustainerField.SetValue(newStance, sustainerField.GetValue(runAndGunStance));
            pawn.stances.curStance = newStance;
        }

        public static void ConvertToVanillaCooldownStance(Pawn pawn, Stance_Cooldown runAndGunCooldown)
        {
            var newStance = new Stance_Cooldown(runAndGunCooldown.ticksLeft, runAndGunCooldown.focusTarg, runAndGunCooldown.verb);
            newStance.stanceTracker = pawn.stances;
            pawn.stances.curStance = newStance;
        }

        public static void InitGiddyUpCompat()
        {
            if (!GiddyUpAvailable) return;

            getExtendedPawnDataMethod = AccessTools.Method("GiddyUp.StorageUtility:GetExtendedPawnData");
            if (getExtendedPawnDataMethod == null)
            {
                Log.Error("[PerspectiveShift] Failed to find GiddyUp.StorageUtility:GetExtendedPawnData method");
                return;
            }

            var extendedDataType = AccessTools.TypeByName("GiddyUp.ExtendedPawnData");
            if (extendedDataType != null)
            {
                mountProperty = AccessTools.Property(extendedDataType, "Mount");
                if (mountProperty == null)
                {
                    Log.Error("[PerspectiveShift] Failed to find GiddyUp.ExtendedPawnData.Mount property");
                }

                reservedByProperty = AccessTools.Property(extendedDataType, "ReservedBy");
                if (reservedByProperty == null)
                {
                    Log.Error("[PerspectiveShift] Failed to find GiddyUp.ExtendedPawnData.ReservedBy property");
                }
            }
            else
            {
                Log.Error("[PerspectiveShift] Failed to find GiddyUp.ExtendedPawnData type");
            }
        }

        public static void InitVehicleCompat()
        {
            if (vehicleCompatInitialized) return;

            if (VehicleFrameworkAvailable)
            {
                vehiclePawnType = AccessTools.TypeByName("Vehicles.VehiclePawn");
                if (vehiclePawnType == null)
                {
                    Log.Error("[PerspectiveShift] Failed to find Vehicles.VehiclePawn type");
                }

                vehiclePathFollowerType = AccessTools.TypeByName("Vehicles.VehiclePathFollower");
                if (vehiclePathFollowerType == null)
                {
                    Log.Error("[PerspectiveShift] Failed to find Vehicles.VehiclePathFollower type");
                }

                if (vehiclePawnType != null && vehiclePathFollowerType != null)
                {
                    startPathMethod = AccessTools.Method(vehiclePathFollowerType, "StartPath");
                    if (startPathMethod == null)
                    {
                        Log.Error("[PerspectiveShift] Failed to find Vehicles.VehiclePathFollower.StartPath method");
                    }

                    vehiclePatherField = AccessTools.Field(vehiclePawnType, "vehiclePather");
                    if (vehiclePatherField == null)
                    {
                        Log.Error("[PerspectiveShift] Failed to find Vehicles.VehiclePawn.vehiclePather field");
                    }
                }
            }

            vehicleCompatInitialized = true;
        }

        public static bool TryGetMount(Pawn rider, out Pawn mount)
        {
            mount = null;
            if (!GiddyUpAvailable) return false;

            var extendedData = getExtendedPawnDataMethod.Invoke(null, new object[] { rider });
            if (extendedData == null) return false;

            mount = mountProperty.GetValue(extendedData) as Pawn;
            return mount != null;
        }

        public static void MoveVehicle(Thing vehicle, IntVec3 dest)
        {
            if (!VehicleFrameworkAvailable) return;
            if (vehicle.GetType() != vehiclePawnType) return;

            var vPather = vehiclePatherField.GetValue(vehicle);
            if (vPather != null)
            {
                LocalTargetInfo targetInfo = dest;
                startPathMethod.Invoke(vPather, new object[] { targetInfo, PathEndMode.OnCell, false });
            }
        }

        public static void ProcessMountMovement(Pawn mount, Vector3 inputDir)
        {
            if (!GiddyUpAvailable) return;

            var extendedData = getExtendedPawnDataMethod.Invoke(null, new object[] { mount });
            if (extendedData == null) return;

            Pawn rider = reservedByProperty.GetValue(extendedData) as Pawn;
            if (rider == null || !rider.Spawned) return;

            var speed = rider.GetStatValue(StatDefOf.MoveSpeed);
            var targetCell = (rider.DrawPos + inputDir * speed * 0.08f).ToIntVec3();
            if (targetCell.Walkable(rider.Map))
                rider.pather.StartPath(targetCell, PathEndMode.OnCell);
        }
    }
}
