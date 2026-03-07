using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn), "CanTakeOrder", MethodType.Getter)]
    public static class Pawn_CanTakeOrder_Patch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (!__result && __instance.IsAvatar())
                __result = true;
        }
    }
}
