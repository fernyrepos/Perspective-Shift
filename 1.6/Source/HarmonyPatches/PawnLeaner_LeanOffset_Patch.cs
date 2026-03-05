using HarmonyLib;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(PawnLeaner), "LeanOffset", MethodType.Getter)]
    public static class PawnLeaner_LeanOffset_Patch
    {
        public static bool Prefix(PawnLeaner __instance, ref Vector3 __result)
        {
            if (!__instance.pawn.IsAvatar()) return true;
            __result = State.Avatar.LeanSmoothed * 0.5f;
            return false;
        }
    }
}
