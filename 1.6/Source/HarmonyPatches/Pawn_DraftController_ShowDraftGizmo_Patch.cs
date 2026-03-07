using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn_DraftController), "ShowDraftGizmo", MethodType.Getter)]
    public static class Pawn_DraftController_ShowDraftGizmo_Patch
    {
        public static void Postfix(Pawn_DraftController __instance, ref bool __result)
        {
            if (!__result && __instance.pawn.IsAvatar())
                __result = true;
        }
    }
}
