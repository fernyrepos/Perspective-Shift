using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Collections;

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

        private static Type vehiclePawnType;
        private static MethodInfo addOrTransferMethod;
        private static FieldInfo vehiclePatherField;
        private static FieldInfo handlersField;
        private static FieldInfo thingOwnerField;
        private static FieldInfo roleField;
        private static FieldInfo handlingTypesField;
        private static MethodInfo startPathMethod;
        private static MethodInfo stopDeadMethod;
        private static PropertyInfo movingProperty;
        private static FieldInfo destinationField;
        private static FieldInfo patherVehicleField;

        private static PropertyInfo vehicleDraftedProp;
        private static FieldInfo vehicleIgnitionField;
        private static PropertyInfo ignitionDraftedProp;
        private static MethodInfo canReachMethod;
        private static PropertyInfo compVehicleTurretsProp;
        private static PropertyInfo turretsProp;
        private static FieldInfo keyField;
        private static PropertyInfo turretIdsProperty;
        private static MethodInfo setTargetMethod;

        private static FieldInfo vveCurrentSpeedField;
        private static PropertyInfo vveStatMoveSpeedProp;
        private static FieldInfo vveHandbrakeAppliedField;

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

        private static readonly Dictionary<int, (Pawn vehicle, bool isDriver, bool isGunner, int lastTick)> vehicleRoleCache
            = new Dictionary<int, (Pawn, bool, bool, int)>();
        private static readonly Dictionary<int, int> wasdActiveVehicles
            = new Dictionary<int, int>();

        static ModCompatibility()
        {
            GiddyUpAvailable = ModsConfig.IsActive("MemeGoddess.GiddyUp");
            SimpleCameraSettingAvailable = ModsConfig.IsActive("ray1203.SimpleCameraSetting");

            RunAndGunAvailable = ModsConfig.IsActive("MemeGoddess.RunAndGun") || ModsConfig.IsActive("roolo.RunAndGun");
            if (RunAndGunAvailable && !InitRunAndGunCompat())
                RunAndGunAvailable = false;

            VehicleFrameworkAvailable = ModsConfig.IsActive("SmashPhil.VehicleFramework");
            if (VehicleFrameworkAvailable && !InitVehicleCompat())
                VehicleFrameworkAvailable = false;

            VanillaVehiclesExpandedAvailable = ModsConfig.IsActive("OskarPotocki.VanillaVehiclesExpanded");
            if (VanillaVehiclesExpandedAvailable && VehicleFrameworkAvailable && !InitVVECompat())
                VanillaVehiclesExpandedAvailable = false;

            VanillaExpandedFrameworkAvailable = ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");
            if (VanillaExpandedFrameworkAvailable && !InitVEFCompat())
                VanillaExpandedFrameworkAvailable = false;

            AchtungAvailable = ModsConfig.IsActive("brrainz.achtung");
            if (AchtungAvailable && !InitAchtungCompat())
                AchtungAvailable = false;
        }

        public static void ClearCaches()
        {
            vehicleRoleCache.Clear();
            wasdActiveVehicles.Clear();
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
            Type utilityType = null;
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
            if (!Require(ref vehiclePatherField, () => AccessTools.Field(vehiclePawnType, "vehiclePather"), "vehiclePather", "VehicleFramework")) return false;
            if (!Require(ref handlersField, () => AccessTools.Field(vehiclePawnType, "handlers"), "handlers", "VehicleFramework")) return false;
            if (!Require(ref thingOwnerField, () => AccessTools.Field(vehicleRoleHandlerType, "thingOwner"), "thingOwner", "VehicleFramework")) return false;
            if (!Require(ref roleField, () => AccessTools.Field(vehicleRoleHandlerType, "role"), "role", "VehicleFramework")) return false;
            if (!Require(ref handlingTypesField, () => AccessTools.Field(vehicleRoleType, "handlingTypes"), "handlingTypes", "VehicleFramework")) return false;
            if (!Require(ref startPathMethod, () => AccessTools.Method(vehiclePathFollowerType, "StartPath"), "StartPath", "VehicleFramework")) return false;
            if (!Require(ref stopDeadMethod, () => AccessTools.Method(vehiclePathFollowerType, "StopDead"), "StopDead", "VehicleFramework")) return false;
            if (!Require(ref movingProperty, () => AccessTools.Property(vehiclePathFollowerType, "Moving"), "Moving", "VehicleFramework")) return false;
            if (!Require(ref destinationField, () => AccessTools.Field(vehiclePathFollowerType, "destination"), "destination", "VehicleFramework")) return false;
            if (!Require(ref patherVehicleField, () => AccessTools.Field(vehiclePathFollowerType, "vehicle"), "VehiclePathFollower.vehicle", "VehicleFramework")) return false;
            if (!Require(ref vehicleDraftedProp, () => AccessTools.Property(vehiclePawnType, "Drafted"), "Drafted", "VehicleFramework")) return false;
            if (!Require(ref vehicleIgnitionField, () => AccessTools.Field(vehiclePawnType, "ignition"), "ignition", "VehicleFramework")) return false;
            if (!Require(ref ignitionDraftedProp, () => AccessTools.Property(vehicleIgnitionField.FieldType, "Drafted"), "ignition.Drafted", "VehicleFramework")) return false;
            if (!Require(ref utilityType, () => AccessTools.TypeByName("Vehicles.VehicleReachabilityUtility"), "VehicleReachabilityUtility", "VehicleFramework")) return false;
            if (!Require(ref canReachMethod, () => AccessTools.Method(utilityType, "CanReachVehicle", new Type[] { vehiclePawnType, typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(TraverseMode) }), "CanReachVehicle", "VehicleFramework")) return false;
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

                        int handlingTypesValue = (int)handlingTypesField.GetValue(role);
                        isDriver = (handlingTypesValue & 1) == 1;
                        isGunner = (handlingTypesValue & 2) == 2;
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

        public static void StopVehicle(Pawn vehicle)
        {
            var vPather = vehiclePatherField.GetValue(vehicle);
            if (vPather != null)
                stopDeadMethod.Invoke(vPather, null);
        }

        public static bool ProcessVehicleMovement(Pawn vehicle, Vector3 inputDir)
        {
            wasdActiveVehicles[vehicle.thingIDNumber] = GenTicks.TicksGame;

            var vPather = vehiclePatherField.GetValue(vehicle);
            if (vPather == null)
                return false;

            if (!(bool)vehicleDraftedProp.GetValue(vehicle, null))
            {
                var ignition = vehicleIgnitionField.GetValue(vehicle);
                ignitionDraftedProp.SetValue(ignition, true, null);
            }

            var isMoving = (bool)movingProperty.GetValue(vPather, null);
            float currentDistToDest = 0f;
            Vector3 currentDestVec = Vector3.zero;

            if (isMoving)
            {
                var dest = (LocalTargetInfo)destinationField.GetValue(vPather);
                if (dest.IsValid)
                {
                    currentDestVec = (dest.Cell - vehicle.Position).ToVector3();
                    currentDestVec.y = 0;
                    currentDistToDest = currentDestVec.magnitude;
                    if (currentDistToDest > 5f && Vector3.Angle(inputDir, currentDestVec) < 45f)
                        return true;
                }
            }

            float[] distances = { 20f, 10f, 5f, 2f, 30f, 40f, 50f, 60f, 70f };
            foreach (var dist in distances)
            {
                if (isMoving && dist <= currentDistToDest && Vector3.Angle(inputDir.normalized * dist, currentDestVec) < 25f)
                    continue;

                var targetCell = (vehicle.DrawPos + inputDir.normalized * dist).ToIntVec3();
                targetCell.x = Mathf.Clamp(targetCell.x, 0, vehicle.Map.Size.x - 1);
                targetCell.z = Mathf.Clamp(targetCell.z, 0, vehicle.Map.Size.z - 1);

                if (targetCell.InBounds(vehicle.Map) is false || !targetCell.Walkable(vehicle.Map))
                    continue;

                try
                {
                    bool reachable = (bool)canReachMethod.Invoke(null, new object[] {
                        vehicle, (LocalTargetInfo)targetCell, PathEndMode.OnCell, Danger.Deadly, TraverseMode.ByPawn
                    });
                    if (!reachable)
                        continue;
                }
                catch { }

                var currentDest = (LocalTargetInfo)destinationField.GetValue(vPather);
                if (currentDest.IsValid && currentDest.Cell == targetCell && isMoving)
                    return true;

                startPathMethod.Invoke(vPather, new object[] { (LocalTargetInfo)targetCell, PathEndMode.OnCell, false });

                if ((bool)movingProperty.GetValue(vPather, null))
                {
                    return true;
                }
            }

            return false;
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
                if (!wasdActiveVehicles.TryGetValue(vehicle.thingIDNumber, out int lastWasdTick))
                    return true;

                if (GenTicks.TicksGame - lastWasdTick > 3)
                    return true;

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

        public static bool IsVehiclePawn(Thing t)
        {
            if (!VehicleFrameworkAvailable || t == null)
                return false;
            return vehiclePawnType != null && vehiclePawnType.IsAssignableFrom(t.GetType());
        }
    }
}
