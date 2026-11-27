using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public static class VehicleControl
    {
        private const float MaxLookAhead = 15f;
        private const float MinLookAhead = 3f;
        private const float LookAheadStep = 4f;
        private const float HeadingTolerance = 20f;
        private const float ReorderWithin = 6f;
        private const int ReorderIntervalTicks = 10;
        private const int DraftRetryTicks = 30;
        private const int DrivenMemoryTicks = 3;
        private const int StopGraceFrames = 6;

        private static FieldInfo patherField;
        private static FieldInfo ignitionField;
        private static PropertyInfo ignitionDraftedProp;
        private static PropertyInfo canMoveFinalProp;
        private static MethodInfo canDraftMethod;
        private static PropertyInfo patherMovingProp;
        private static PropertyInfo patherDestinationProp;
        private static MethodInfo patherFailedMethod;
        private static MethodInfo findStandableCellMethod;
        private static MethodInfo canReachVehicleMethod;

        private static readonly Dictionary<int, int> lastDrivenTick = new Dictionary<int, int>();

        private static int orderedVehicleId = -1;
        private static Vector3 orderedHeading;
        private static int lastOrderTick = -9999;
        private static int lastDraftTick = -9999;
        private static int idleFrames;

        public static bool Init(Type vehiclePawnType, Type vehiclePathFollowerType)
        {
            Type pathingHelperType = null;
            Type reachabilityType = null;

            if (!Need(ref patherField, () => AccessTools.Field(vehiclePawnType, "vehiclePather"), "VehiclePawn.vehiclePather")) return false;
            if (!Need(ref ignitionField, () => AccessTools.Field(vehiclePawnType, "ignition"), "VehiclePawn.ignition")) return false;
            if (!Need(ref ignitionDraftedProp, () => AccessTools.Property(ignitionField.FieldType, "Drafted"), "VehicleIgnitionController.Drafted")) return false;
            if (!Need(ref canMoveFinalProp, () => AccessTools.Property(vehiclePawnType, "CanMoveFinal"), "VehiclePawn.CanMoveFinal")) return false;
            if (!Need(ref canDraftMethod, () => AccessTools.Method(vehiclePawnType, "CanDraft"), "VehiclePawn.CanDraft")) return false;
            if (!Need(ref patherMovingProp, () => AccessTools.Property(vehiclePathFollowerType, "Moving"), "VehiclePathFollower.Moving")) return false;
            if (!Need(ref patherDestinationProp, () => AccessTools.Property(vehiclePathFollowerType, "Destination"), "VehiclePathFollower.Destination")) return false;
            if (!Need(ref patherFailedMethod, () => AccessTools.Method(vehiclePathFollowerType, "PatherFailed"), "VehiclePathFollower.PatherFailed")) return false;
            if (!Need(ref pathingHelperType, () => AccessTools.TypeByName("Vehicles.PathingHelper"), "Vehicles.PathingHelper")) return false;
            if (!Need(ref findStandableCellMethod, () => AccessTools.Method(pathingHelperType, "TryFindNearestStandableCell"), "PathingHelper.TryFindNearestStandableCell")) return false;
            if (!Need(ref reachabilityType, () => AccessTools.TypeByName("Vehicles.VehicleReachabilityUtility"), "Vehicles.VehicleReachabilityUtility")) return false;
            if (!Need(ref canReachVehicleMethod, () => AccessTools.Method(reachabilityType, "CanReachVehicle",
                new[] { vehiclePawnType, typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(TraverseMode) }), "VehicleReachabilityUtility.CanReachVehicle")) return false;

            return true;
        }

        private static bool Need<T>(ref T target, Func<T> lookup, string name) where T : class
        {
            target = lookup();
            if (target != null) return true;
            Log.Error($"[PS] VehicleControl: {name} not found");
            return false;
        }

        public static void ClearCaches()
        {
            lastDrivenTick.Clear();
            orderedVehicleId = -1;
            orderedHeading = Vector3.zero;
            idleFrames = 0;
        }

        public static bool RecentlyDriven(Pawn vehicle)
        {
            if (vehicle == null) return false;
            if (!lastDrivenTick.TryGetValue(vehicle.thingIDNumber, out int tick)) return false;
            return GenTicks.TicksGame - tick <= DrivenMemoryTicks;
        }

        public static void Drive(Pawn vehicle, Vector3 direction)
        {
            if (vehicle == null || !vehicle.Spawned || vehicle.Map == null) return;
            if (direction.sqrMagnitude < 0.0001f) return;
            if (!(bool)canMoveFinalProp.GetValue(vehicle, null)) return;
            if (!TryEnsureRunning(vehicle)) return;

            idleFrames = 0;
            lastDrivenTick[vehicle.thingIDNumber] = GenTicks.TicksGame;

            Vector3 heading = direction.normalized;
            if (!NeedsNewOrder(vehicle, heading)) return;

            orderedVehicleId = vehicle.thingIDNumber;
            orderedHeading = heading;
            lastOrderTick = GenTicks.TicksGame;
            TryOrderAlong(vehicle, heading);
        }

        public static void NotifyNoInput(Pawn vehicle)
        {
            if (vehicle == null || vehicle.thingIDNumber != orderedVehicleId) return;
            if (++idleFrames < StopGraceFrames) return;
            StopDriving(vehicle);
        }

        public static void StopDriving(Pawn vehicle)
        {
            if (vehicle == null || vehicle.thingIDNumber != orderedVehicleId) return;

            orderedVehicleId = -1;
            orderedHeading = Vector3.zero;
            idleFrames = 0;

            var pather = patherField.GetValue(vehicle);
            if (pather == null) return;
            if (!(bool)patherMovingProp.GetValue(pather, null)) return;
            patherFailedMethod.Invoke(pather, null);
        }

        private static bool TryEnsureRunning(Pawn vehicle)
        {
            var ignition = ignitionField.GetValue(vehicle);
            if (ignition == null) return false;
            if ((bool)ignitionDraftedProp.GetValue(ignition, null)) return true;

            if (GenTicks.TicksGame - lastDraftTick < DraftRetryTicks) return false;
            lastDraftTick = GenTicks.TicksGame;

            var report = (AcceptanceReport)canDraftMethod.Invoke(vehicle, null);
            if (!report.Accepted) return false;

            ignitionDraftedProp.SetValue(ignition, true, null);
            return (bool)ignitionDraftedProp.GetValue(ignition, null);
        }

        private static bool NeedsNewOrder(Pawn vehicle, Vector3 heading)
        {
            if (vehicle.thingIDNumber != orderedVehicleId) return true;
            if (GenTicks.TicksGame - lastOrderTick < ReorderIntervalTicks) return false;
            if (Vector3.Angle(heading, orderedHeading) > HeadingTolerance) return true;

            var pather = patherField.GetValue(vehicle);
            if (pather == null) return false;
            if (!(bool)patherMovingProp.GetValue(pather, null)) return true;

            var destination = (LocalTargetInfo)patherDestinationProp.GetValue(pather, null);
            if (!destination.IsValid) return true;

            return (destination.Cell - vehicle.Position).LengthHorizontal <= ReorderWithin;
        }

        private static bool TryOrderAlong(Pawn vehicle, Vector3 heading)
        {
            if (vehicle.jobs == null) return false;

            Map map = vehicle.Map;
            var origin = vehicle.DrawPos;

            for (float distance = MaxLookAhead; distance >= MinLookAhead; distance -= LookAheadStep)
            {
                IntVec3 aim = (origin + heading * distance).ToIntVec3();
                aim.x = Mathf.Clamp(aim.x, 0, map.Size.x - 1);
                aim.z = Mathf.Clamp(aim.z, 0, map.Size.z - 1);
                if (aim == vehicle.Position) continue;

                if (!TryFindStandableCell(vehicle, aim, out IntVec3 target)) continue;
                if (target == vehicle.Position) continue;
                if (!CanReach(vehicle, target)) continue;

                var job = JobMaker.MakeJob(JobDefOf.Goto, target);
                if (vehicle.jobs.TryTakeOrderedJob(job, JobTag.Misc, false)) return true;
            }

            return false;
        }

        private static bool TryFindStandableCell(Pawn vehicle, IntVec3 aim, out IntVec3 target)
        {
            var args = new object[] { vehicle, aim, null, -1f };
            bool found = (bool)findStandableCellMethod.Invoke(null, args);
            target = found ? (IntVec3)args[2] : IntVec3.Invalid;
            return found;
        }

        private static bool CanReach(Pawn vehicle, IntVec3 target)
        {
            return (bool)canReachVehicleMethod.Invoke(null, new object[]
            {
                vehicle, (LocalTargetInfo)target, PathEndMode.OnCell, Danger.Deadly, TraverseMode.ByPawn
            });
        }
    }
}
