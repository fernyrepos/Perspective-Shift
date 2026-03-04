using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    public static class Root_Play_Update_Patch
    {
        public static void Postfix()
        {
            State.Update();
        }
    }
}
