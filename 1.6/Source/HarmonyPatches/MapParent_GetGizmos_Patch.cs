using System.Collections.Generic;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(MapParent), nameof(MapParent.GetGizmos))]
    public static class MapParent_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, MapParent __instance)
        {
            foreach (var gizmo in values)
            {
                if (gizmo is Command_Action command
                    && command.icon == MapParent.ShowMapCommand
                    && State.MapViewBlocked(__instance.Map))
                {
                    command.Disable("PS_CannotViewOtherMaps".Translate());
                }
                yield return gizmo;
            }
        }
    }
}
