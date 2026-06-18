using HarmonyLib;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Pawn_PostApplyDamage_Patch
    {
        public static void Postfix(Pawn __instance, float totalDamageDealt)
        {
            if (State.IsActive && __instance == State.Avatar?.pawn && totalDamageDealt > 0f)
                State.lastDamageTime = Time.realtimeSinceStartup;
        }
    }
}
