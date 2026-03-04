using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(World), nameof(World.ExposeData))]
    public static class World_ExposeData_Patch
    {
        public static void Postfix()
        {
            Scribe_Values.Look(ref State.CurrentMode, "PS_Mode", PlaystyleMode.Director);
            Scribe_Deep.Look(ref State.Avatar, "PS_Avatar");
        }
    }
}
