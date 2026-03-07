using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    public static class Game_InitNewGame_Patch
    {
        public static void Postfix()
        {
            State.CameraLockPosition = null;
        }
    }
}
