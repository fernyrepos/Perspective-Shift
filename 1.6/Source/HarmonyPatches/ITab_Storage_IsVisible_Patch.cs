using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(ITab_Storage), "IsVisible", MethodType.Getter)]
    public static class ITab_Storage_IsVisible_Patch
    {
        public static void Postfix(ITab_Storage __instance, ref bool __result)
        {
            if (!__result) return;
            var sel = __instance.SelObject as ThingWithComps;
            var comp = sel?.GetComp<CompPlayerOnly>();
            if (comp != null && comp.mode != PlayerOnlyMode.None)
            {
                __result = false;
            }
        }
    }
}
