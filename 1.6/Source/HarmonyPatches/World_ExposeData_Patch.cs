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
            Scribe_Values.Look(ref State.permadeath, "PS_Permadeath", false);
            Scribe_Values.Look(ref State.allowDirectorInAuthentic, "PS_AllowDirectorInAuthentic", false);
            Scribe_Collections.Look(ref State.seekAtWillPawns, "PS_SeekAtWillPawns", LookMode.Value);
            Scribe_Collections.Look(ref FloatMenuOptionProvider_AvatarInteraction.lastInteractionByTarget, "PS_LastInteractionByTarget", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                State.seekAtWillPawns ??= new HashSet<int>();
                FloatMenuOptionProvider_AvatarInteraction.lastInteractionByTarget ??= new Dictionary<int, int>();
            }
        }
    }
}
