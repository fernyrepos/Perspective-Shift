using HarmonyLib;
using RimWorld.Planet;
using System.Collections.Generic;
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
            Scribe_Values.Look(ref State.pendingDeathMenu, "PS_PendingDeathMenu", false);
            Scribe_Collections.Look(ref State.seekAtWillPawns, "PS_SeekAtWillPawns", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                State.seekAtWillPawns ??= new HashSet<int>();
            }
        }
    }
}
