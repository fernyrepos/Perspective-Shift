using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Collections;
using Verse.Sound;

namespace PerspectiveShift
{
    [StaticConstructorOnStartup]
    [HotSwappable]
    public static class ModCompatibility
    {
        public static readonly bool GiddyUpAvailable;
        public static readonly bool RunAndGunAvailable;
        public static readonly bool VehicleFrameworkAvailable;
        public static readonly bool SimpleCameraSettingAvailable;
        public static readonly bool VanillaVehiclesExpandedAvailable;
        public static readonly bool VanillaExpandedFrameworkAvailable;
        public static readonly bool AchtungAvailable;
        public static readonly bool ProcessorFrameworkAvailable;
        public static readonly bool RimbodyAvailable;
        public static readonly bool RimbodyChunkWorkoutsAvailable;
        public static readonly bool AsAboveSoBelowAvailable;
        public static readonly bool ProgressionAmmunitionAvailable;
        public static readonly bool DubsBadHygieneAvailable;
        public static readonly bool ThemingModAvailable;
        public static readonly bool AlphaBooksAvailable;
        public static readonly bool DiscoveriesAvailable;
        public static readonly bool IsoRimAvailable;

        private static Func<bool> isoRimViewReady;
        private static Func<float> isoRimViewYaw;

        private static Type vehiclePawnType;
        private static MethodInfo addOrTransferMethod;
        private static FieldInfo handlersField;
        private static FieldInfo thingOwnerField;
        private static FieldInfo roleField;
        private static PropertyInfo handlingTypesProperty;
        private static FieldInfo patherVehicleField;
        private static PropertyInfo slotsAvailableProperty;
        private static MethodInfo canOperateRoleMethod;
        private static MethodInfo promptToBoardMethod;
        private static MethodInfo ideoAllowsBoardingMethod;
        private static FieldInfo movementStatusField;

        private static PropertyInfo compVehicleTurretsProp;
        private static PropertyInfo turretsProp;
        private static FieldInfo keyField;
        private static PropertyInfo turretIdsProperty;
        private static MethodInfo setTargetMethod;

        private static FieldInfo vveCurrentSpeedField;
        private static PropertyInfo vveStatMoveSpeedProp;
        private static FieldInfo vveHandbrakeAppliedField;

        private static MethodInfo simpleCameraConfigPatchMethod;

        private static Type compAbilitiesType;
        private static FieldInfo currentlyCastingField;
        private static FieldInfo abilityDefField;
        private static PropertyInfo abilityLabelCapProperty;
        private static Type jobDriverCastAbilityOnceType;

        private static Type compRunAndGunType;
        private static PropertyInfo isEnabledProperty;
        public static Type stanceRunAndGunType;
        public static Type stanceRunAndGunCooldownType;
        private static FieldInfo effecterField;
        private static FieldInfo sustainerField;

        private static Type compProcessorType;
        private static FieldInfo enabledProcessesField;
        private static MethodInfo spaceLeftForMethod;
        private static MethodInfo addIngredientMethod;
        private static Type processFilterType;
        private static FieldInfo allowedIngredientsField;

        private static Type ammoRechargerType;
        private static Type ammoCompType;
        private static PropertyInfo rechargerAmmoTypeProperty;
        private static MethodInfo canRechargeMethod;

        private static Type abStairsType;
        private static PropertyInfo abCounterpartsProperty;

        private static Type alphaBookType;
        private static MethodInfo discoveryTryDiscoverMethod;
        private static MethodInfo discoveryTryShowNextMethod;
        public static JobDef AlphaBookReadingJob;

        private static Type rimbodyDBType;
        private static MethodInfo compPhysiqueMethod;
        private static PropertyInfo hasPhysiqueProperty;
        private static FieldInfo gainField;
        private static PropertyInfo gainMaxProperty;

        public class RimbodyChunkWorkout
        {
            public JobDef job;
            public string labelKey;
        }

        public static readonly List<RimbodyChunkWorkout> RimbodyChunkWorkouts = new List<RimbodyChunkWorkout>();

        private static readonly string[][] ChunkWorkoutDefs =
        {
            new[] { "Rimbody_DoChunkLifting", "PS_ChunkWorkoutLift" },
            new[] { "Rimbody_DoChunkOverheadPress", "PS_ChunkWorkoutPress" },
            new[] { "Rimbody_DoChunkSquats", "PS_ChunkWorkoutSquat" },
        };
        private static FieldInfo strengthTargetsField;
        private static FieldInfo cardioTargetsField;
        private static FieldInfo balanceTargetsField;
        private static MethodInfo doTryGiveJobStrengthMethod;
        private static MethodInfo doTryGiveJobCardioMethod;
        private static MethodInfo doTryGiveJobBalanceMethod;

        private static PropertyInfo thirstNeedProp;
        private static FieldInfo hygieneNeedField;
        private static FieldInfo bladderNeedField;
        private static FieldInfo dbhSettingsField;

        public static JobDef DBH_washAtCell;
        public static JobDef DBH_DBHDrinkFromGround;
        public static JobDef DBH_DBHDrinkFromBasin;
        public static JobDef DBH_haveWildPoo;

        private static readonly Dictionary<int, (Pawn vehicle, bool isDriver, bool isGunner, int lastTick)> vehicleRoleCache
            = new Dictionary<int, (Pawn, bool, bool, int)>();
        static ModCompatibility()
        {
            GiddyUpAvailable = ModsConfig.IsActive("MemeGoddess.GiddyUp");

            RunAndGunAvailable = ModsConfig.IsActive("MemeGoddess.RunAndGun") || ModsConfig.IsActive("roolo.RunAndGun");
            if (RunAndGunAvailable && !TryInit("RunAndGun", InitRunAndGunCompat))
                RunAndGunAvailable = false;

            VehicleFrameworkAvailable = ModsConfig.IsActive("SmashPhil.VehicleFramework");
            if (VehicleFrameworkAvailable && !TryInit("Vehicle", InitVehicleCompat))
                VehicleFrameworkAvailable = false;

            VanillaVehiclesExpandedAvailable = ModsConfig.IsActive("OskarPotocki.VanillaVehiclesExpanded");
            if (VanillaVehiclesExpandedAvailable && VehicleFrameworkAvailable && !TryInit("VVE", InitVVECompat))
                VanillaVehiclesExpandedAvailable = false;

            VanillaExpandedFrameworkAvailable = ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");
            if (VanillaExpandedFrameworkAvailable && !TryInit("VEF", InitVEFCompat))
                VanillaExpandedFrameworkAvailable = false;

            AchtungAvailable = ModsConfig.IsActive("brrainz.achtung");
            if (AchtungAvailable && !TryInit("Achtung", InitAchtungCompat))
                AchtungAvailable = false;

            SimpleCameraSettingAvailable = ModsConfig.IsActive("ray1203.SimpleCameraSetting");
            if (SimpleCameraSettingAvailable && !TryInit("SimpleCameraSetting", InitSimpleCameraSettingCompat))
                SimpleCameraSettingAvailable = false;

            ProcessorFrameworkAvailable = ModsConfig.IsActive("syrchalis.processor.framework");
            if (ProcessorFrameworkAvailable && !TryInit("ProcessorFramework", InitProcessorFrameworkCompat))
                ProcessorFrameworkAvailable = false;

            RimbodyAvailable = ModsConfig.IsActive("Maux36.Rimbody");
            if (RimbodyAvailable && !TryInit("Rimbody", InitRimbodyCompat))
                RimbodyAvailable = false;

            RimbodyChunkWorkoutsAvailable = ModsConfig.IsActive("Maux36.Rimbody") && TryInit("RimbodyChunk", InitRimbodyChunkCompat);

            AsAboveSoBelowAvailable = ModsConfig.IsActive("astryl.AsAboveSoBelow2");
            if (AsAboveSoBelowAvailable && !TryInit("AsAboveSoBelow", InitAsAboveSoBelowCompat))
                AsAboveSoBelowAvailable = false;

            ProgressionAmmunitionAvailable = ModsConfig.IsActive("ferny.ProgressionAmmunition");
            if (ProgressionAmmunitionAvailable && !TryInit("ProgressionAmmunition", InitProgressionAmmunitionCompat))
                ProgressionAmmunitionAvailable = false;

            DubsBadHygieneAvailable = ModsConfig.IsActive("Dubwise.DubsBadHygiene") || ModsConfig.IsActive("Dubwise.DubsBadHygiene.Lite");
            if (DubsBadHygieneAvailable && !TryInit("DBH", InitDBHCompat))
                DubsBadHygieneAvailable = false;

            AlphaBooksAvailable = ModsConfig.IsActive("sarg.alphabooks");
            if (AlphaBooksAvailable && !TryInit("AlphaBooks", InitAlphaBooksCompat))
                AlphaBooksAvailable = false;

            DiscoveriesAvailable = ModsConfig.IsActive("ferny.Discoveries");
            if (DiscoveriesAvailable && !TryInit("Discoveries", InitDiscoveriesCompat))
                DiscoveriesAvailable = false;

            IsoRimAvailable = ModsConfig.IsActive("Peren.IsoRim17") || ModsConfig.IsActive("connor.isorim");
            if (IsoRimAvailable && !TryInit("IsoRim", InitIsoRimCompat))
                IsoRimAvailable = false;

            ThemingModAvailable = ModsConfig.IsActive("ferny.themingformodpack");
        }

        private static bool TryInit(string name, Func<bool> init)
        {
            try
            {
                return init();
            }
            catch (Exception ex)
            {
                Log.Error($"[PS] {name} compatibility failed to initialize and has been disabled: {ex}");
                return false;
            }
        }

        public static void ClearCaches()
        {
            vehicleRoleCache.Clear();
            VehicleControl.ClearCaches();
        }

        private static bool Require<T>(ref T target, Func<T> lookup, string name, string prefix) where T : class
        {
            target = lookup();
            if (target != null) return true;
            Log.Error($"[PS] {prefix}: {name} not found");
            return false;
        }

        private static bool InitRunAndGunCompat()
        {
            if (!Require(ref compRunAndGunType, () => AccessTools.TypeByName("RunAndGun.CompRunAndGun"), "CompRunAndGun type", "RunAndGun")) return false;
            if (!Require(ref isEnabledProperty, () => AccessTools.Property(compRunAndGunType, "isEnabled"), "isEnabled property", "RunAndGun")) return false;
            if (!Require(ref stanceRunAndGunType, () => AccessTools.TypeByName("RunAndGun.Stance_RunAndGun"), "Stance_RunAndGun type", "RunAndGun")) return false;
            if (!Require(ref stanceRunAndGunCooldownType, () => AccessTools.TypeByName("RunAndGun.Stance_RunAndGun_Cooldown"), "Stance_RunAndGun_Cooldown type", "RunAndGun")) return false;
            if (!Require(ref effecterField, () => AccessTools.Field(typeof(Stance_Warmup), "effecter"), "Stance_Warmup.effecter field", "RunAndGun")) return false;
            if (!Require(ref sustainerField, () => AccessTools.Field(typeof(Stance_Warmup), "sustainer"), "Stance_Warmup.sustainer field", "RunAndGun")) return false;
            return true;
        }

        private static bool InitVehicleCompat()
        {
            Type vehiclePathFollowerType = null;
            Type vehicleRoleHandlerType = null;
            Type vehicleRoleType = null;
            Type compVehicleTurretsType = null;
            Type vehicleTurretType = null;
            MethodInfo patherDrawMethod = null;
            MethodInfo getGizmosMethod = null;
            MethodInfo allowDeconstructMethod = null;
            MethodInfo getFloatMenuOptionsMethod = null;

            if (!Require(ref vehiclePawnType, () => AccessTools.TypeByName("Vehicles.VehiclePawn"), "VehiclePawn", "VehicleFramework")) return false;
            if (!Require(ref vehiclePathFollowerType, () => AccessTools.TypeByName("Vehicles.VehiclePathFollower"), "VehiclePathFollower", "VehicleFramework")) return false;
            if (!Require(ref vehicleRoleHandlerType, () => AccessTools.TypeByName("Vehicles.VehicleRoleHandler"), "VehicleRoleHandler", "VehicleFramework")) return false;
            if (!Require(ref vehicleRoleType, () => AccessTools.TypeByName("Vehicles.VehicleRole"), "VehicleRole", "VehicleFramework")) return false;
            if (!Require(ref handlersField, () => AccessTools.Field(vehiclePawnType, "handlers"), "handlers", "VehicleFramework")) return false;
            if (!Require(ref thingOwnerField, () => AccessTools.Field(vehicleRoleHandlerType, "thingOwner"), "thingOwner", "VehicleFramework")) return false;
            if (!Require(ref roleField, () => AccessTools.Field(vehicleRoleHandlerType, "role"), "role", "VehicleFramework")) return false;
            if (!Require(ref handlingTypesProperty, () => AccessTools.Property(vehicleRoleType, "HandlingTypes"), "HandlingTypes", "VehicleFramework")) return false;
            if (!Require(ref patherVehicleField, () => AccessTools.Field(vehiclePathFollowerType, "vehicle"), "VehiclePathFollower.vehicle", "VehicleFramework")) return false;
            if (!Require(ref compVehicleTurretsType, () => AccessTools.TypeByName("Vehicles.CompVehicleTurrets"), "CompVehicleTurrets", "VehicleFramework")) return false;
            if (!Require(ref vehicleTurretType, () => AccessTools.TypeByName("Vehicles.VehicleTurret"), "VehicleTurret", "VehicleFramework")) return false;
            if (!Require(ref compVehicleTurretsProp, () => AccessTools.Property(vehiclePawnType, "CompVehicleTurrets"), "CompVehicleTurrets", "VehicleFramework")) return false;
            if (!Require(ref turretsProp, () => AccessTools.Property(compVehicleTurretsType, "Turrets"), "Turrets", "VehicleFramework")) return false;
            if (!Require(ref setTargetMethod, () => AccessTools.Method(vehicleTurretType, "SetTarget"), "SetTarget", "VehicleFramework")) return false;
            if (!Require(ref keyField, () => AccessTools.Field(vehicleTurretType, "key"), "key", "VehicleFramework")) return false;
            if (!Require(ref turretIdsProperty, () => AccessTools.Property(vehicleRoleType, "TurretIds"), "TurretIds", "VehicleFramework")) return false;
            if (!Require(ref addOrTransferMethod, () => AccessTools.Method(vehiclePawnType, "AddOrTransfer", new Type[] { typeof(Thing), typeof(int) }), "AddOrTransfer", "VehicleFramework")) return false;
            if (!Require(ref patherDrawMethod, () => AccessTools.Method("Vehicles.VehiclePathFollower:PatherDraw"), "PatherDraw", "VehicleFramework")) return false;
            if (!Require(ref getGizmosMethod, () => AccessTools.Method(vehiclePawnType, "GetGizmos"), "GetGizmos", "VehicleFramework")) return false;
            if (!Require(ref allowDeconstructMethod, () => AccessTools.Method("Vehicles.Patch_Construction:AllowDeconstructVehicle"), "AllowDeconstructVehicle", "VehicleFramework")) return false;
            if (!Require(ref getFloatMenuOptionsMethod, () => AccessTools.Method(vehiclePawnType, "GetFloatMenuOptions"), "GetFloatMenuOptions", "VehicleFramework")) return false;
            if (!Require(ref slotsAvailableProperty, () => AccessTools.Property(vehicleRoleHandlerType, "AreSlotsAvailableAndReservable"), "AreSlotsAvailableAndReservable", "VehicleFramework")) return false;
            if (!Require(ref canOperateRoleMethod, () => AccessTools.Method(vehicleRoleHandlerType, "CanOperateRole", new Type[] { typeof(Pawn) }), "CanOperateRole", "VehicleFramework")) return false;
            if (!Require(ref promptToBoardMethod, () => AccessTools.Method(vehiclePawnType, "PromptToBoardVehicle"), "PromptToBoardVehicle", "VehicleFramework")) return false;
            if (!Require(ref ideoAllowsBoardingMethod, () => AccessTools.Method(vehiclePawnType, "IdeoAllowsBoarding"), "IdeoAllowsBoarding", "VehicleFramework")) return false;
            if (!Require(ref movementStatusField, () => AccessTools.Field(vehiclePawnType, "movementStatus"), "movementStatus", "VehicleFramework")) return false;

            if (!VehicleControl.Init(vehiclePawnType, vehiclePathFollowerType)) return false;

            var vehicleCompatHarmony = new Harmony("PerspectiveShift.VehicleCompat");
            vehicleCompatHarmony.Patch(patherDrawMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(VehiclePathFollowerPatherDrawPrefix))));
            vehicleCompatHarmony.Patch(getGizmosMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(VehiclePawnGetGizmosPostfix))));
            vehicleCompatHarmony.Patch(allowDeconstructMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(AllowDeconstructVehiclePostfix))));
            vehicleCompatHarmony.Patch(getFloatMenuOptionsMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(VehiclePawnGetFloatMenuOptionsPostfix))));

            return true;
        }

        private static bool InitVVECompat()
        {
            Type vveCompType = null;
            MethodInfo slowdownMethod = null;

            if (!Require(ref vveCompType, () => AccessTools.TypeByName("VanillaVehiclesExpanded.CompVehicleMovementController"), "CompVehicleMovementController", "VVE")) return false;
            if (!Require(ref vveCurrentSpeedField, () => AccessTools.Field(vveCompType, "currentSpeed"), "currentSpeed", "VVE")) return false;
            if (!Require(ref vveStatMoveSpeedProp, () => AccessTools.Property(vveCompType, "StatMoveSpeed"), "StatMoveSpeed", "VVE")) return false;
            if (!Require(ref vveHandbrakeAppliedField, () => AccessTools.Field(vveCompType, "handbrakeApplied"), "handbrakeApplied", "VVE")) return false;
            if (!Require(ref slowdownMethod, () => AccessTools.Method(vveCompType, "Slowdown"), "Slowdown", "VVE")) return false;

            var vveHarmony = new Harmony("PerspectiveShift.VVECompat");
            vveHarmony.Patch(slowdownMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(VVESlowdownPrefix))));

            return true;
        }

        private static bool InitVEFCompat()
        {
            Type abilityType = null;
            Type abilityDefType = null;

            if (!Require(ref compAbilitiesType, () => AccessTools.TypeByName("VEF.Abilities.CompAbilities"), "CompAbilities", "VEF")) return false;
            if (!Require(ref currentlyCastingField, () => AccessTools.Field(compAbilitiesType, "currentlyCasting"), "currentlyCasting", "VEF")) return false;
            if (!Require(ref abilityType, () => AccessTools.TypeByName("VEF.Abilities.Ability"), "Ability", "VEF")) return false;
            if (!Require(ref abilityDefField, () => AccessTools.Field(abilityType, "def"), "def", "VEF")) return false;
            if (!Require(ref abilityDefType, () => AccessTools.TypeByName("VEF.Abilities.AbilityDef"), "AbilityDef", "VEF")) return false;
            if (!Require(ref abilityLabelCapProperty, () => AccessTools.Property(abilityDefType, "LabelCap"), "LabelCap", "VEF")) return false;
            if (!Require(ref jobDriverCastAbilityOnceType, () => AccessTools.TypeByName("VEF.Abilities.JobDriver_CastAbilityOnce"), "JobDriver_CastAbilityOnce", "VEF")) return false;
            return true;
        }

        private static bool InitAchtungCompat()
        {
            Type cleanRoomProviderType = null;

            if (!Require(ref cleanRoomProviderType, () => AccessTools.TypeByName("AchtungMod.FloatMenuOptionProvider_CleanRoom"), "FloatMenuOptionProvider_CleanRoom type", "Achtung")) return false;

            Avatar.FloatMenuProviderBlacklist.Add(cleanRoomProviderType);
            return true;
        }

        private static bool InitSimpleCameraSettingCompat()
        {
            Type cameraConfigPatchType = null;

            if (!Require(ref cameraConfigPatchType, () => AccessTools.TypeByName("SimpleCameraSetting.CameraConfigPatch"), "CameraConfigPatch type", "SimpleCameraSetting")) return false;
            if (!Require(ref simpleCameraConfigPatchMethod, () => AccessTools.Method(cameraConfigPatchType, "ConfigPatch"), "ConfigPatch method", "SimpleCameraSetting")) return false;
            return true;
        }

        private static bool InitProcessorFrameworkCompat()
        {
            if (!Require(ref compProcessorType, () => AccessTools.TypeByName("ProcessorFramework.CompProcessor"), "CompProcessor type", "ProcessorFramework")) return false;
            if (!Require(ref enabledProcessesField, () => AccessTools.Field(compProcessorType, "enabledProcesses"), "enabledProcesses field", "ProcessorFramework")) return false;
            if (!Require(ref spaceLeftForMethod, () => AccessTools.Method(compProcessorType, "SpaceLeftFor"), "SpaceLeftFor method", "ProcessorFramework")) return false;
            if (!Require(ref addIngredientMethod, () => AccessTools.Method(compProcessorType, "AddIngredient"), "AddIngredient method", "ProcessorFramework")) return false;
            if (!Require(ref processFilterType, () => AccessTools.TypeByName("ProcessorFramework.ProcessFilter"), "ProcessFilter type", "ProcessorFramework")) return false;
            if (!Require(ref allowedIngredientsField, () => AccessTools.Field(processFilterType, "allowedIngredients"), "allowedIngredients field", "ProcessorFramework")) return false;
            return true;
        }

        private static bool InitRimbodyCompat()
        {
            if (!Require(ref rimbodyDBType, () => AccessTools.TypeByName("Maux36.Rimbody.RimbodyDB"), "RimbodyDB type", "Rimbody")) return false;
            if (!Require(ref strengthTargetsField, () => AccessTools.Field(rimbodyDBType, "StrengthTargets"), "StrengthTargets field", "Rimbody")) return false;
            if (!Require(ref cardioTargetsField, () => AccessTools.Field(rimbodyDBType, "CardioTargets"), "CardioTargets field", "Rimbody")) return false;
            if (!Require(ref balanceTargetsField, () => AccessTools.Field(rimbodyDBType, "BalanceTargets"), "BalanceTargets field", "Rimbody")) return false;
            if (!Require(ref doTryGiveJobStrengthMethod, () => AccessTools.Method(AccessTools.TypeByName("Maux36.Rimbody.JobGiver_DoStrengthBuilding"), "DoTryGiveTargetJob"), "DoTryGiveTargetJob method", "Rimbody")) return false;
            if (!Require(ref doTryGiveJobCardioMethod, () => AccessTools.Method(AccessTools.TypeByName("Maux36.Rimbody.JobGiver_DoCardioBuilding"), "DoTryGiveJob"), "DoTryGiveJob method", "Rimbody")) return false;
            if (!Require(ref doTryGiveJobBalanceMethod, () => AccessTools.Method(AccessTools.TypeByName("Maux36.Rimbody.JobGiver_DoBalanceBuilding"), "DoTryGiveTargetJob"), "DoTryGiveTargetJob method", "Rimbody")) return false;
            return true;
        }

        private static bool InitProgressionAmmunitionCompat()
        {
            if (!Require(ref ammoRechargerType, () => AccessTools.TypeByName("ProgressionAmmunition.Building_AmmoRecharger"), "Building_AmmoRecharger type", "ProgressionAmmunition")) return false;
            if (!Require(ref ammoCompType, () => AccessTools.TypeByName("ProgressionAmmunition.CompAmmo"), "CompAmmo type", "ProgressionAmmunition")) return false;
            if (!Require(ref rechargerAmmoTypeProperty, () => AccessTools.Property(ammoRechargerType, "RechargerAmmoType"), "RechargerAmmoType property", "ProgressionAmmunition")) return false;
            if (!Require(ref canRechargeMethod, () => AccessTools.Method(ammoRechargerType, "CanRecharge"), "CanRecharge method", "ProgressionAmmunition")) return false;
            return true;
        }

        public static bool TryGetAmmoRechargerType(Thing thing, Pawn pawn, out string ammoType)
        {
            ammoType = null;
            if (!ProgressionAmmunitionAvailable || thing == null || pawn == null) return false;
            if (!ammoRechargerType.IsInstanceOfType(thing)) return false;

            var weapon = pawn.equipment?.Primary;
            if (weapon == null) return false;

            object ammoComp = null;
            var comps = weapon.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (!ammoCompType.IsInstanceOfType(comps[i])) continue;
                ammoComp = comps[i];
                break;
            }

            if (ammoComp == null) return false;
            if (!(bool)canRechargeMethod.Invoke(thing, new object[] { ammoComp })) return false;

            ammoType = rechargerAmmoTypeProperty.GetValue(thing, null)?.ToString();
            return !ammoType.NullOrEmpty();
        }

        private static bool InitAlphaBooksCompat()
        {
            if (!Require(ref alphaBookType, () => AccessTools.TypeByName("AlphaBooks.Book_NotAutoReadable"), "Book_NotAutoReadable type", "AlphaBooks")) return false;
            if (!Require(ref AlphaBookReadingJob, () => DefDatabase<JobDef>.GetNamedSilentFail("ABooks_Reading"), "ABooks_Reading job", "AlphaBooks")) return false;
            return true;
        }

        private static bool InitDiscoveriesCompat()
        {
            if (!Require(ref discoveryTryDiscoverMethod, () => AccessTools.Method("Discoveries.DiscoveryTracker:TryDiscover", new[] { typeof(Thing), typeof(bool) }), "DiscoveryTracker.TryDiscover", "Discoveries")) return false;
            if (!Require(ref discoveryTryShowNextMethod, () => AccessTools.Method("Discoveries.DiscoveryQueue:TryShowNext"), "DiscoveryQueue.TryShowNext", "Discoveries")) return false;
            return true;
        }

        private static bool InitIsoRimCompat()
        {
            Type apiType = null;
            MethodInfo readyGetter = null;
            MethodInfo yawGetter = null;

            if (!Require(ref apiType, () => AccessTools.TypeByName("IsoRim.Api"), "IsoRim.Api type", "IsoRim")) return false;
            if (!Require(ref readyGetter, () => AccessTools.PropertyGetter(apiType, "ViewReady"), "Api.ViewReady", "IsoRim")) return false;
            if (!Require(ref yawGetter, () => AccessTools.PropertyGetter(apiType, "ViewYaw"), "Api.ViewYaw", "IsoRim")) return false;

            isoRimViewReady = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), readyGetter);
            isoRimViewYaw = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), yawGetter);
            return true;
        }

        public static float IsoRimViewYaw()
        {
            if (!IsoRimAvailable || !isoRimViewReady()) return 0f;
            return isoRimViewYaw();
        }

        private static bool InitAsAboveSoBelowCompat()
        {
            if (!Require(ref abStairsType, () => AccessTools.TypeByName("AsAboveSoBelow.Building_ABStairs2"), "Building_ABStairs2 type", "AsAboveSoBelow")) return false;
            if (!Require(ref abCounterpartsProperty, () => AccessTools.Property(abStairsType, "Counterparts"), "Counterparts property", "AsAboveSoBelow")) return false;
            return true;
        }

        public static bool IsLevelLink(Thing thing)
        {
            if (!AsAboveSoBelowAvailable || thing == null) return false;
            if (!abStairsType.IsInstanceOfType(thing)) return false;

            return abCounterpartsProperty.GetValue(thing, null) is ICollection counterparts && counterparts.Count > 0;
        }

        public static bool IsAlphaBook(Thing thing) => AlphaBooksAvailable && alphaBookType.IsInstanceOfType(thing);

        public static void NotifyThingPickedUp(Thing thing)
        {
            if (!DiscoveriesAvailable || thing == null) return;

            if (discoveryTryDiscoverMethod.Invoke(null, new object[] { thing, false }) is true)
            {
                discoveryTryShowNextMethod.Invoke(null, null);
            }
        }

        private static bool InitRimbodyChunkCompat()
        {
            var compType = AccessTools.TypeByName("Maux36.Rimbody.CompPhysique");
            if (compType == null) return false;
            if (!Require(ref compPhysiqueMethod, () => AccessTools.Method("Maux36.Rimbody.PawnExtensions:compPhysique"), "compPhysique method", "Rimbody")) return false;
            if (!Require(ref hasPhysiqueProperty, () => AccessTools.Property(compType, "HasPhysique"), "HasPhysique property", "Rimbody")) return false;
            if (!Require(ref gainField, () => AccessTools.Field(compType, "gain"), "gain field", "Rimbody")) return false;
            if (!Require(ref gainMaxProperty, () => AccessTools.Property(compType, "gainMax"), "gainMax property", "Rimbody")) return false;

            foreach (var entry in ChunkWorkoutDefs)
            {
                var job = DefDatabase<JobDef>.GetNamedSilentFail(entry[0]);
                if (job != null)
                    RimbodyChunkWorkouts.Add(new RimbodyChunkWorkout { job = job, labelKey = entry[1] });
            }

            return RimbodyChunkWorkouts.Count > 0;
        }

        private static object RimbodyPhysique(Pawn pawn)
        {
            if (!RimbodyChunkWorkoutsAvailable || pawn == null) return null;
            var comp = compPhysiqueMethod.Invoke(null, new object[] { pawn });
            if (comp == null) return null;
            return (bool)hasPhysiqueProperty.GetValue(comp, null) ? comp : null;
        }

        public static bool RimbodyTracksPhysique(Pawn pawn) => RimbodyPhysique(pawn) != null;

        public static bool RimbodyWorkoutBlocked(Pawn pawn, out string reason)
        {
            reason = null;
            var comp = RimbodyPhysique(pawn);
            if (comp == null) return true;

            var rest = pawn.needs?.rest;
            if (rest != null && rest.CurLevel < 0.17f)
            {
                reason = "PS_ChunkWorkoutTooTired".Translate();
                return true;
            }

            if ((float)gainField.GetValue(comp) >= (float)gainMaxProperty.GetValue(comp, null))
            {
                reason = "PS_ChunkWorkoutExhausted".Translate();
                return true;
            }

            return false;
        }

        private static bool InitDBHCompat()
        {
            var dbhSettingsType = AccessTools.TypeByName("DubsBadHygiene.Settings");
            if (!Require(ref thirstNeedProp, () => AccessTools.Property(dbhSettingsType, "ThirstNeed"), "ThirstNeed property", "DubsBadHygiene")) return false;
            if (!Require(ref hygieneNeedField, () => AccessTools.Field(dbhSettingsType, "HygieneNeed"), "HygieneNeed field", "DubsBadHygiene")) return false;
            if (!Require(ref bladderNeedField, () => AccessTools.Field(dbhSettingsType, "BladderNeed"), "BladderNeed field", "DubsBadHygiene")) return false;
            if (!Require(ref dbhSettingsField, () => AccessTools.Field(AccessTools.TypeByName("DubsBadHygiene.DubsBadHygieneMod"), "Settings"), "Settings field", "DubsBadHygiene")) return false;

            var dubDefType = AccessTools.TypeByName("DubsBadHygiene.DubDef");
            DBH_washAtCell = AccessTools.Field(dubDefType, "washAtCell")?.GetValue(null) as JobDef;
            DBH_DBHDrinkFromGround = AccessTools.Field(dubDefType, "DBHDrinkFromGround")?.GetValue(null) as JobDef;
            DBH_DBHDrinkFromBasin = AccessTools.Field(dubDefType, "DBHDrinkFromBasin")?.GetValue(null) as JobDef;
            DBH_haveWildPoo = AccessTools.Field(dubDefType, "haveWildPoo")?.GetValue(null) as JobDef;
            return true;
        }

        public static bool IsVEFAbilityCast(Pawn pawn)
        {
            if (!VanillaExpandedFrameworkAvailable) return false;
            return pawn.jobs?.curDriver != null && jobDriverCastAbilityOnceType.IsAssignableFrom(pawn.jobs.curDriver.GetType());
        }

        public static string GetVEFAbilityName(Pawn pawn)
        {
            if (!VanillaExpandedFrameworkAvailable)
            {
                return null;
            }
            if (pawn.jobs.curDriver != null && !jobDriverCastAbilityOnceType.IsAssignableFrom(pawn.jobs.curDriver.GetType()))
            {
                return null;
            }
            try
            {
                var comp = pawn.AllComps.FirstOrDefault(c => compAbilitiesType.IsAssignableFrom(c.GetType()));
                if (comp != null)
                {
                    var ability = currentlyCastingField.GetValue(comp);
                    if (ability != null)
                    {
                        var def = abilityDefField.GetValue(ability);
                        if (def != null)
                        {
                            var label = abilityLabelCapProperty.GetValue(def);
                            var labelStr = label?.ToString();
                            return labelStr;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PS] Error getting VEF ability name: {ex}");
            }
            return null;
        }

        public static bool IsRunAndGunActiveFor(Pawn pawn, out string reason)
        {
            reason = "Unknown";
            if (!RunAndGunAvailable)
                return false;

            var comp = pawn.AllComps.FirstOrDefault(c => compRunAndGunType.IsAssignableFrom(c.GetType()));
            if (comp == null)
                return false;

            if (!(bool)isEnabledProperty.GetValue(comp, null))
            {
                reason = "isEnabled is false";
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
            var newStance = (Stance_Warmup)Activator.CreateInstance(stanceRunAndGunType, warmup.ticksLeft, warmup.focusTarg, warmup.verb);
            newStance.stanceTracker = pawn.stances;
            effecterField.SetValue(newStance, effecterField.GetValue(warmup));
            sustainerField.SetValue(newStance, sustainerField.GetValue(warmup));
            pawn.stances.curStance = newStance;
        }

        public static void ConvertToRunAndGunCooldownStance(Pawn pawn, Stance_Cooldown cooldown)
        {
            var newStance = (Stance_Cooldown)Activator.CreateInstance(stanceRunAndGunCooldownType, cooldown.ticksLeft, cooldown.focusTarg, cooldown.verb);
            newStance.stanceTracker = pawn.stances;
            pawn.stances.curStance = newStance;
        }

        public static void ConvertToVanillaWarmupStance(Pawn pawn, Stance_Warmup runAndGunStance)
        {
            var newStance = new Stance_Warmup(runAndGunStance.ticksLeft, runAndGunStance.focusTarg, runAndGunStance.verb);
            newStance.stanceTracker = pawn.stances;
            effecterField.SetValue(newStance, effecterField.GetValue(runAndGunStance));
            sustainerField.SetValue(newStance, sustainerField.GetValue(runAndGunStance));
            pawn.stances.curStance = newStance;
        }

        public static void ConvertToVanillaCooldownStance(Pawn pawn, Stance_Cooldown runAndGunCooldown)
        {
            var newStance = new Stance_Cooldown(runAndGunCooldown.ticksLeft, runAndGunCooldown.focusTarg, runAndGunCooldown.verb);
            newStance.stanceTracker = pawn.stances;
            pawn.stances.curStance = newStance;
        }

        public static bool IsPawnInVehicle(Pawn pawn, out Pawn vehicle, out bool isDriver, out bool isGunner)
        {
            vehicle = null;
            isDriver = false;
            isGunner = false;

            if (!VehicleFrameworkAvailable)
                return false;

            int currentTick = GenTicks.TicksGame;
            if (vehicleRoleCache.TryGetValue(pawn.thingIDNumber, out var cached) && currentTick - cached.lastTick < 60)
            {
                vehicle = cached.vehicle;
                isDriver = cached.isDriver;
                isGunner = cached.isGunner;
                return vehicle != null;
            }

            IThingHolder holder = pawn.ParentHolder;
            while (holder != null)
            {
                if (vehiclePawnType.IsAssignableFrom(holder.GetType()))
                {
                    vehicle = (Pawn)holder;
                    break;
                }
                holder = holder.ParentHolder;
            }

            if (vehicle == null)
            {
                vehicleRoleCache[pawn.thingIDNumber] = (null, false, false, currentTick);
                return false;
            }

            try
            {
                var handlers = handlersField.GetValue(vehicle) as IList;
                if (handlers != null)
                {
                    foreach (var handler in handlers)
                    {
                        if (handler == null)
                            continue;

                        var thingOwner = thingOwnerField.GetValue(handler) as ThingOwner<Pawn>;
                        if (thingOwner == null)
                            continue;

                        bool pawnInHandler = false;
                        foreach (var p in thingOwner)
                        {
                            if (p == pawn)
                            {
                                pawnInHandler = true;
                                break;
                            }
                        }

                        if (!pawnInHandler)
                            continue;

                        var role = roleField.GetValue(handler);
                        if (role == null)
                            continue;

                        int handling = (int)handlingTypesProperty.GetValue(role, null);
                        isDriver = (handling & 1) == 1;
                        isGunner = (handling & 2) == 2;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PS] Error checking vehicle roles for {pawn.Name}: {ex}");
            }

            vehicleRoleCache[pawn.thingIDNumber] = (vehicle, isDriver, isGunner, currentTick);
            return true;
        }

        public static void FireVehicleWeapons(Pawn vehicle, Pawn gunner, Vector3 targetMapPos)
        {
            var comp = compVehicleTurretsProp.GetValue(vehicle, null);
            if (comp == null)
                return;

            var turrets = turretsProp.GetValue(comp, null) as IEnumerable;
            if (turrets == null)
                return;

            var controlledIds = GetGunnerTurretIds(vehicle, gunner);
            var clickCell = targetMapPos.ToIntVec3();
            var things = clickCell.GetThingList(vehicle.Map);
            Thing bestTarget = things.FirstOrDefault(t => t is Pawn && t != vehicle)
                ?? things.FirstOrDefault(t => t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item);
            LocalTargetInfo target = bestTarget != null
                ? new LocalTargetInfo(bestTarget)
                : new LocalTargetInfo(clickCell);

            foreach (var turret in turrets)
            {
                if (controlledIds.Count > 0 && keyField.GetValue(turret) is string tKey && !controlledIds.Contains(tKey))
                    continue;
                setTargetMethod.Invoke(turret, new object[] { target });
            }
        }

        public static void ClearVehicleWeapons(Pawn vehicle, Pawn gunner)
        {
            var comp = compVehicleTurretsProp.GetValue(vehicle, null);
            if (comp == null)
                return;

            var turrets = turretsProp.GetValue(comp, null) as IEnumerable;
            if (turrets == null)
                return;

            var controlledIds = GetGunnerTurretIds(vehicle, gunner);
            foreach (var turret in turrets)
            {
                if (controlledIds.Count > 0 && keyField.GetValue(turret) is string tKey && !controlledIds.Contains(tKey))
                    continue;
                setTargetMethod.Invoke(turret, new object[] { LocalTargetInfo.Invalid });
            }
        }

        private static List<string> GetGunnerTurretIds(Pawn vehicle, Pawn gunner)
        {
            var ids = new List<string>();
            var handlers = handlersField.GetValue(vehicle) as IList;
            if (handlers == null)
                return ids;

            foreach (var handler in handlers)
            {
                var owner = thingOwnerField.GetValue(handler) as ThingOwner<Pawn>;
                if (owner == null || !owner.Contains(gunner))
                    continue;

                var role = roleField.GetValue(handler);
                if (role == null)
                    continue;

                if (turretIdsProperty.GetValue(role, null) is List<string> turretIds)
                    ids.AddRange(turretIds);
            }

            return ids;
        }

        private static bool VehiclePathFollowerPatherDrawPrefix(object __instance)
        {
            if (State.Avatar?.pawn == null)
                return true;

            var vehicle = patherVehicleField.GetValue(__instance) as Pawn;
            if (vehicle == null)
                return true;

            if (!IsPawnInVehicle(State.Avatar.pawn, out Pawn pVeh, out bool isDriver, out _))
                return true;

            if (pVeh == vehicle && isDriver)
                return false;

            return true;
        }

        public static IEnumerable<Gizmo> VehiclePawnGetGizmosPostfix(IEnumerable<Gizmo> __result, object __instance)
        {
            if (State.IsActive && !State.DrawingTopRightGizmos && !PerspectiveShiftMod.settings.disableCustomGizmos && IsPawnInVehicle(State.Avatar?.pawn, out Pawn vehicle, out bool isDriver, out _))
            {
                if (vehicle == __instance && isDriver)
                {
                    yield break;
                }
            }

            foreach (var g in __result)
            {
                yield return g;
            }
        }

        public static bool VVESlowdownPrefix(ThingComp __instance)
        {
            try
            {
                Pawn vehicle = __instance.parent as Pawn;
                if (!VehicleControl.RecentlyDriven(vehicle)) return true;

                var maxSpeed = (float)vveStatMoveSpeedProp.GetValue(__instance, null);
                var currentSpeed = (float)vveCurrentSpeedField.GetValue(__instance);
                if (currentSpeed >= maxSpeed * 0.95f)
                {
                    vveHandbrakeAppliedField.SetValue(__instance, false);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PS] VVESlowdownPrefix error: {ex}");
            }

            return true;
        }

        public static void AllowDeconstructVehiclePostfix(object __0, object __1, ref AcceptanceReport __2)
        {
            if (!__2.Accepted) return;
            if (!State.IsActive) return;
            if (__1 is Thing t && IsPawnInVehicle(State.Avatar.pawn, out Pawn vehicle, out _, out _) && vehicle == t)
                __2 = false;
        }

        public static IEnumerable<FloatMenuOption> VehiclePawnGetFloatMenuOptionsPostfix(IEnumerable<FloatMenuOption> options, Pawn selPawn)
        {
            if (Avatar.IsAvatarLeftClick)
            {
                yield break;
            }
            else
            {
                foreach (var option in options)
                {
                    yield return option;
                }
            }
        }

        public static bool PutCargoToVehicle(Thing vehicle, Thing thing)
        {
            if (!VehicleFrameworkAvailable)
                return false;

            try
            {
                int transferred = (int)addOrTransferMethod.Invoke(vehicle, new object[] { thing, thing.stackCount });
                return transferred > 0;
            }
            catch (Exception ex)
            {
                Log.Error($"[PS] Error putting cargo to vehicle: {ex}");
                return false;
            }
        }

        public static bool VehicleBoardingEligible(Thing vehicle, Pawn pawn)
        {
            if (!VehicleFrameworkAvailable || pawn == null || !IsVehiclePawn(vehicle)) return false;
            if (vehicle == pawn || !vehicle.Spawned || !pawn.Spawned || vehicle.Map != pawn.Map) return false;
            if (vehicle.Faction != pawn.Faction) return false;
            if (!pawn.RaceProps.ToolUser) return false;

            try
            {
                if ((int)movementStatusField.GetValue(vehicle) == 0) return false;
                if (!(bool)ideoAllowsBoardingMethod.Invoke(vehicle, new object[] { pawn })) return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[PS] Error checking vehicle boarding eligibility: {ex}");
                return false;
            }

            return pawn.CanReserveAndReach(vehicle, PathEndMode.ClosestTouch, Danger.Deadly);
        }

        public static bool TryBoardVehicle(Thing vehicle, Pawn pawn)
        {
            try
            {
                var handlers = handlersField.GetValue(vehicle) as IList;
                if (handlers == null) return false;

                object seat = FindOpenSeat(pawn, handlers, true) ?? FindOpenSeat(pawn, handlers, false);
                if (seat == null) return false;

                promptToBoardMethod.Invoke(vehicle, new object[] { pawn, seat });
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[PS] Error boarding vehicle: {ex}");
                return false;
            }
        }

        private static object FindOpenSeat(Pawn pawn, IList handlers, bool requireMovement)
        {
            foreach (var handler in handlers)
            {
                if (handler == null) continue;
                if (!(bool)slotsAvailableProperty.GetValue(handler, null)) continue;

                var role = roleField.GetValue(handler);
                if (role == null) continue;

                int handling = (int)handlingTypesProperty.GetValue(role, null);
                if (requireMovement && (handling & 1) != 1) continue;
                if (handling != 0 && !(bool)canOperateRoleMethod.Invoke(handler, new object[] { pawn })) continue;

                return handler;
            }
            return null;
        }

        public static bool IsVehiclePawn(Thing t)
        {
            if (!VehicleFrameworkAvailable || t == null)
                return false;
            return vehiclePawnType != null && vehiclePawnType.IsAssignableFrom(t.GetType());
        }

        public static void ResetSimpleCameraSetting()
        {
            if (!SimpleCameraSettingAvailable) return;
            try
            {
                simpleCameraConfigPatchMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Log.Error($"[PS] Error resetting SimpleCameraSetting: {ex}");
            }
        }

        public static bool TryHandleProcessorFramework(Thing t, Thing carriedThing, Pawn avatar)
        {
            if (!ProcessorFrameworkAvailable) return false;

            var comp = (t as ThingWithComps)?.AllComps?.FirstOrDefault(c => compProcessorType.IsAssignableFrom(c.GetType()));
            if (comp == null) return false;

            var enabledProcesses = enabledProcessesField.GetValue(comp) as IDictionary;
            if (enabledProcesses == null) return false;

            foreach (DictionaryEntry entry in enabledProcesses)
            {
                var processDef = entry.Key;
                var allowedIngredients = allowedIngredientsField.GetValue(entry.Value) as List<ThingDef>;

                if (allowedIngredients != null && allowedIngredients.Contains(carriedThing.def))
                {
                    var spaceLeft = (int)spaceLeftForMethod.Invoke(comp, new object[] { processDef, 1f });
                    if (spaceLeft > 0)
                    {
                        addIngredientMethod.Invoke(comp, new object[] { carriedThing, processDef });
                        carriedThing.def.soundDrop?.PlayOneShot(new TargetInfo(avatar.Position, avatar.Map));
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool TryHandleRimbody(Thing t, Pawn avatar)
        {
            if (!RimbodyAvailable) return false;

            var strengthTargets = strengthTargetsField.GetValue(null) as List<ThingDef>;
            var cardioTargets = cardioTargetsField.GetValue(null) as List<ThingDef>;
            var balanceTargets = balanceTargetsField.GetValue(null) as List<ThingDef>;

            Job job = null;
            if (strengthTargets != null && strengthTargets.Contains(t.def))
                job = doTryGiveJobStrengthMethod.Invoke(null, new object[] { avatar, t }) as Job;
            else if (cardioTargets != null && cardioTargets.Contains(t.def))
                job = doTryGiveJobCardioMethod.Invoke(null, new object[] { avatar, t }) as Job;
            else if (balanceTargets != null && balanceTargets.Contains(t.def))
                job = doTryGiveJobBalanceMethod.Invoke(null, new object[] { avatar, t }) as Job;

            if (job != null)
            {
                job.playerForced = true;
                avatar.jobs.TryTakeOrderedJob(job);
                return true;
            }
            return false;
        }

        public static ThinkNode_JobGiver MakeHygieneJobGiver(string typeName)
        {
            if (!DubsBadHygieneAvailable) return null;
            var type = AccessTools.TypeByName(typeName);
            if (type == null) return null;
            return Activator.CreateInstance(type) as ThinkNode_JobGiver;
        }

        public static bool IsDBHThirstEnabled() => DubsBadHygieneAvailable && (bool)thirstNeedProp.GetValue(null, null);

        public static bool IsDBHHygieneEnabled() => DubsBadHygieneAvailable && (bool)hygieneNeedField.GetValue(dbhSettingsField.GetValue(null));

        public static bool IsDBHBladderEnabled() => DubsBadHygieneAvailable && (bool)bladderNeedField.GetValue(dbhSettingsField.GetValue(null));

        public static bool IsDBHDrinkableItem(Thing t)
        {
            if (t?.def?.modExtensions == null) return false;
            foreach (var ext in t.def.modExtensions)
            {
                if (ext.GetType().Name == "WaterExt") return true;
            }
            return false;
        }

        public static bool IsDBHBasin(Thing t)
        {
            if (t == null) return false;
            var fixtureProp = AccessTools.Property(t.GetType(), "fixture");
            if (fixtureProp != null)
            {
                var val = fixtureProp.GetValue(t, null);
                if (val != null && val.ToString() == "Basin") return true;
            }
            return false;
        }
    }
}
