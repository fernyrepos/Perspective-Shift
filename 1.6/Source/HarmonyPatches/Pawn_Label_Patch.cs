using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Thing))]
    public static class Pawn_Label_Patch
    {
        [HarmonyPatch(nameof(Thing.Label), MethodType.Getter)]
        [HarmonyPostfix]
        public static void LabelPostfix(Thing __instance, ref string __result)
        {
            if (__instance is Pawn pawn && pawn.IsAvatar())
                __result += " " + "PS_You".Translate();
        }

        [HarmonyPatch(nameof(Thing.LabelCap), MethodType.Getter)]
        [HarmonyPostfix]
        public static void LabelCapPostfix(Thing __instance, ref string __result)
        {
            if (__instance is Pawn pawn && pawn.IsAvatar())
                __result += " " + "PS_You".Translate();
        }
    }

    [HarmonyPatch(typeof(Pawn))]
    public static class Pawn_LabelShort_Patch
    {
        [HarmonyPatch(nameof(Pawn.LabelShort), MethodType.Getter)]
        [HarmonyPostfix]
        public static void LabelShortPostfix(Pawn __instance, ref string __result)
        {
            if (__instance.IsAvatar())
                __result += " " + "PS_You".Translate();
        }
    }
}
