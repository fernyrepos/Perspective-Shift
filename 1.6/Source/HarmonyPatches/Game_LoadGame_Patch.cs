using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class Game_LoadGame_Patch
    {
        public static void Postfix()
        {
            State.CameraLockPosition = null;
        }
    }
}
