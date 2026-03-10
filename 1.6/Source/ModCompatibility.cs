using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

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

        private static Type vehiclePawnType;
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
        }

        public static void ClearCaches()
        {
            vehicleRoleCache.Clear();
            wasdActiveVehicles.Clear();
        }

        private static bool InitRunAndGunCompat()
        {
            compRunAndGunType = AccessTools.TypeByName("RunAndGun.CompRunAndGun");
            if (compRunAndGunType == null)
            {
                Log.Error("[PS] RunAndGun: CompRunAndGun type not found");
                return false;
            }

            isEnabledProperty = AccessTools.Property(compRunAndGunType, "isEnabled");
            if (isEnabledProperty == null)
            {
                Log.Error("[PS] RunAndGun: isEnabled property not found");
                return false;
            }

            stanceRunAndGunType = AccessTools.TypeByName("RunAndGun.Stance_RunAndGun");
            if (stanceRunAndGunType == null)
            {
                Log.Error("[PS] RunAndGun: Stance_RunAndGun type not found");
                return false;
            }

            stanceRunAndGunCooldownType = AccessTools.TypeByName("RunAndGun.Stance_RunAndGun_Cooldown");
            if (stanceRunAndGunCooldownType == null)
            {
                Log.Error("[PS] RunAndGun: Stance_RunAndGun_Cooldown type not found");
                return false;
            }

            effecterField = AccessTools.Field(typeof(Stance_Warmup), "effecter");
            if (effecterField == null)
            {
                Log.Error("[PS] RunAndGun: Stance_Warmup.effecter field not found");
                return false;
            }

            sustainerField = AccessTools.Field(typeof(Stance_Warmup), "sustainer");
            if (sustainerField == null)
            {
                Log.Error("[PS] RunAndGun: Stance_Warmup.sustainer field not found");
                return false;
            }

            return true;
        }

        private static bool InitVehicleCompat()
        {
            vehiclePawnType = AccessTools.TypeByName("Vehicles.VehiclePawn");
            if (vehiclePawnType == null)
            {
                Log.Error("[PS] VehicleFramework: VehiclePawn type not found");
                return false;
            }

            var vehiclePathFollowerType = AccessTools.TypeByName("Vehicles.VehiclePathFollower");
            if (vehiclePathFollowerType == null)
            {
                Log.Error("[PS] VehicleFramework: VehiclePathFollower type not found");
                return false;
            }

            var vehicleRoleHandlerType = AccessTools.TypeByName("Vehicles.VehicleRoleHandler");
            if (vehicleRoleHandlerType == null)
            {
                Log.Error("[PS] VehicleFramework: VehicleRoleHandler type not found");
                return false;
            }

            var vehicleRoleType = AccessTools.TypeByName("Vehicles.VehicleRole");
            if (vehicleRoleType == null)
            {
                Log.Error("[PS] VehicleFramework: VehicleRole type not found");
                return false;
            }

            vehiclePatherField = AccessTools.Field(vehiclePawnType, "vehiclePather");
            if (vehiclePatherField == null)
            {
                Log.Error("[PS] VehicleFramework: vehiclePather field not found");
                return false;
            }

            handlersField = AccessTools.Field(vehiclePawnType, "handlers");
            if (handlersField == null)
            {
                Log.Error("[PS] VehicleFramework: handlers field not found");
                return false;
            }

            thingOwnerField = AccessTools.Field(vehicleRoleHandlerType, "thingOwner");
            if (thingOwnerField == null)
            {
                Log.Error("[PS] VehicleFramework: thingOwner field not found");
                return false;
            }

            roleField = AccessTools.Field(vehicleRoleHandlerType, "role");
            if (roleField == null)
            {
                Log.Error("[PS] VehicleFramework: role field not found");
                return false;
            }

            handlingTypesField = AccessTools.Field(vehicleRoleType, "handlingTypes");
            if (handlingTypesField == null)
            {
                Log.Error("[PS] VehicleFramework: handlingTypes field not found");
                return false;
            }

            startPathMethod = AccessTools.Method(vehiclePathFollowerType, "StartPath");
            if (startPathMethod == null)
            {
                Log.Error("[PS] VehicleFramework: StartPath method not found");
                return false;
            }

            stopDeadMethod = AccessTools.Method(vehiclePathFollowerType, "StopDead");
            if (stopDeadMethod == null)
            {
                Log.Error("[PS] VehicleFramework: StopDead method not found");
                return false;
            }

            movingProperty = AccessTools.Property(vehiclePathFollowerType, "Moving");
            if (movingProperty == null)
            {
                Log.Error("[PS] VehicleFramework: Moving property not found");
                return false;
            }

            destinationField = AccessTools.Field(vehiclePathFollowerType, "destination");
            if (destinationField == null)
            {
                Log.Error("[PS] VehicleFramework: destination field not found");
                return false;
            }

            patherVehicleField = AccessTools.Field(vehiclePathFollowerType, "vehicle");
            if (patherVehicleField == null)
            {
                Log.Error("[PS] VehicleFramework: VehiclePathFollower.vehicle field not found");
                return false;
            }

            vehicleDraftedProp = AccessTools.Property(vehiclePawnType, "Drafted");
            if (vehicleDraftedProp == null)
            {
                Log.Error("[PS] VehicleFramework: Drafted property not found");
                return false;
            }
            vehicleIgnitionField = AccessTools.Field(vehiclePawnType, "ignition");
            if (vehicleIgnitionField == null)
            {
                Log.Error("[PS] VehicleFramework: ignition field not found");
                return false;
            }
            ignitionDraftedProp = AccessTools.Property(vehicleIgnitionField.FieldType, "Drafted");
            if (ignitionDraftedProp == null)
            {
                Log.Error("[PS] VehicleFramework: ignition.Drafted property not found");
                return false;
            }

            var utilityType = AccessTools.TypeByName("Vehicles.VehicleReachabilityUtility");
            if (utilityType == null)
            {
                Log.Error("[PS] VehicleFramework: VehicleReachabilityUtility type not found");
                return false;
            }
            canReachMethod = AccessTools.Method(utilityType, "CanReachVehicle",
                new Type[] { vehiclePawnType, typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(TraverseMode) });
            if (canReachMethod == null)
            {
                Log.Error("[PS] VehicleFramework: CanReachVehicle method not found");
                return false;
            }

            var compVehicleTurretsType = AccessTools.TypeByName("Vehicles.CompVehicleTurrets");
            if (compVehicleTurretsType == null)
            {
                Log.Error("[PS] VehicleFramework: CompVehicleTurrets type not found");
                return false;
            }
            var vehicleTurretType = AccessTools.TypeByName("Vehicles.VehicleTurret");
            if (vehicleTurretType == null)
            {
                Log.Error("[PS] VehicleFramework: VehicleTurret type not found");
                return false;
            }
            compVehicleTurretsProp = AccessTools.Property(vehiclePawnType, "CompVehicleTurrets");
            if (compVehicleTurretsProp == null)
            {
                Log.Error("[PS] VehicleFramework: CompVehicleTurrets property not found");
                return false;
            }
            turretsProp = AccessTools.Property(compVehicleTurretsType, "Turrets");
            if (turretsProp == null)
            {
                Log.Error("[PS] VehicleFramework: Turrets property not found");
                return false;
            }
            setTargetMethod = AccessTools.Method(vehicleTurretType, "SetTarget");
            if (setTargetMethod == null)
            {
                Log.Error("[PS] VehicleFramework: SetTarget method not found");
                return false;
            }
            keyField = AccessTools.Field(vehicleTurretType, "key");
            if (keyField == null)
            {
                Log.Error("[PS] VehicleFramework: key field not found");
                return false;
            }
            turretIdsProperty = AccessTools.Property(vehicleRoleType, "TurretIds");
            if (turretIdsProperty == null)
            {
                Log.Error("[PS] VehicleFramework: TurretIds property not found");
                return false;
            }

            var patherDrawMethod = AccessTools.Method("Vehicles.VehiclePathFollower:PatherDraw");
            if (patherDrawMethod == null)
            {
                Log.Error("[PS] VehicleFramework: PatherDraw method not found");
                return false;
            }

            var getGizmosMethod = AccessTools.Method(vehiclePawnType, "GetGizmos");
            if (getGizmosMethod == null)
            {
                Log.Error("[PS] VehicleFramework: GetGizmos method not found");
                return false;
            }

            var allowDeconstructMethod = AccessTools.Method("Vehicles.Patch_Construction:AllowDeconstructVehicle");
            if (allowDeconstructMethod == null)
            {
                Log.Error("[PS] VehicleFramework: AllowDeconstructVehicle method not found");
                return false;
            }

            var vehicleCompatHarmony = new Harmony("PerspectiveShift.VehicleCompat");
            vehicleCompatHarmony.Patch(patherDrawMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(VehiclePathFollowerPatherDrawPrefix))));
            vehicleCompatHarmony.Patch(getGizmosMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(VehiclePawnGetGizmosPostfix))));
            vehicleCompatHarmony.Patch(allowDeconstructMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(AllowDeconstructVehiclePostfix))));

            return true;
        }

        private static bool InitVVECompat()
        {
            var vveCompType = AccessTools.TypeByName("VanillaVehiclesExpanded.CompVehicleMovementController");
            if (vveCompType == null)
            {
                Log.Error("[PS] VVE: CompVehicleMovementController type not found");
                return false;
            }

            vveCurrentSpeedField = AccessTools.Field(vveCompType, "currentSpeed");
            if (vveCurrentSpeedField == null)
            {
                Log.Error("[PS] VVE: currentSpeed field not found");
                return false;
            }

            vveStatMoveSpeedProp = AccessTools.Property(vveCompType, "StatMoveSpeed");
            if (vveStatMoveSpeedProp == null)
            {
                Log.Error("[PS] VVE: StatMoveSpeed property not found");
                return false;
            }

            vveHandbrakeAppliedField = AccessTools.Field(vveCompType, "handbrakeApplied");
            if (vveHandbrakeAppliedField == null)
            {
                Log.Error("[PS] VVE: handbrakeApplied field not found");
                return false;
            }

            var slowdownMethod = AccessTools.Method(vveCompType, "Slowdown");
            if (slowdownMethod == null)
            {
                Log.Error("[PS] VVE: Slowdown method not found");
                return false;
            }

            var vveHarmony = new Harmony("PerspectiveShift.VVECompat");
            vveHarmony.Patch(slowdownMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModCompatibility), nameof(VVESlowdownPrefix))));

            return true;
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
                var handlers = handlersField.GetValue(vehicle) as System.Collections.IList;
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

            bool isMoving = (bool)movingProperty.GetValue(vPather, null);
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

            var turrets = turretsProp.GetValue(comp, null) as System.Collections.IEnumerable;
            if (turrets == null)
                return;

            var controlledIds = GetGunnerTurretIds(vehicle, gunner);
            var clickCell = targetMapPos.ToIntVec3();
            var things = clickCell.GetThingList(vehicle.Map);
            Thing bestTarget = things?.FirstOrDefault(t => t is Pawn && t != vehicle)
                ?? things?.FirstOrDefault(t => t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item);
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

            var turrets = turretsProp.GetValue(comp, null) as System.Collections.IEnumerable;
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
            var handlers = handlersField.GetValue(vehicle) as System.Collections.IList;
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
            if (State.IsActive && !State.DrawingTopRightGizmos && IsPawnInVehicle(State.Avatar?.pawn, out Pawn vehicle, out bool isDriver, out _))
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
    }
}
