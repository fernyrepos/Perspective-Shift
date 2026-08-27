using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Game), nameof(Game.CurrentMap), MethodType.Setter)]
    public static class Game_CurrentMap_Patch
    {
        public static bool Prefix(Map value)
        {
            return !State.MapSwitchBlocked(value);
        }
    }
}
