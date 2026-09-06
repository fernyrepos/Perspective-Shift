using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    public static class VanillaFishingSuppression
    {
        public static bool SuppressFor(Pawn pawn)
        {
            if (pawn == null) return false;
            if (!ModsConfig.OdysseyActive) return false;
            if (PerspectiveShiftMod.settings == null || !PerspectiveShiftMod.settings.fishingMinigame) return false;
            if (!State.IsActive || State.Avatar == null) return false;
            return pawn == State.Avatar.pawn;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Fish), nameof(WorkGiver_Fish.NonScanJob))]
    public static class WorkGiver_Fish_NonScanJob_Patch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!VanillaFishingSuppression.SuppressFor(pawn)) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Zone_Fishing), nameof(Zone_Fishing.GetFloatMenuOptions))]
    public static class Zone_Fishing_GetFloatMenuOptions_Patch
    {
        public static bool Prefix(Pawn selPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            if (!VanillaFishingSuppression.SuppressFor(selPawn)) return true;
            __result = Enumerable.Empty<FloatMenuOption>();
            return false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Fish), nameof(WorkGiver_Fish.HasJobOnCell))]
    public static class WorkGiver_Fish_HasJobOnCell_Patch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!VanillaFishingSuppression.SuppressFor(pawn)) return true;
            __result = false;
            return false;
        }
    }
}
