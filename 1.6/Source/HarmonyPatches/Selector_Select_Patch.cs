using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Selector), nameof(Selector.Select))]
    public static class Selector_Select_Patch
    {
        public static bool Prefix(object obj)
        {
            Map map = obj switch
            {
                Thing thing => thing.MapHeld,
                Zone zone => zone.Map,
                _ => null
            };

            if (map == null || !State.MapViewBlocked(map)) return true;
            State.NotifyMapViewBlocked();
            return false;
        }
    }
}
