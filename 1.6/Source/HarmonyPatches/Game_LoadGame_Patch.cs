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
            if (State.pendingDeathMenu && State.Avatar?.pawn is { Dead: true } deadPawn)
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Find.WindowStack.Add(new Dialog_YouDied(deadPawn, null, null));
                    Find.TickManager.Pause();
                });
            }
        }
    }
}
