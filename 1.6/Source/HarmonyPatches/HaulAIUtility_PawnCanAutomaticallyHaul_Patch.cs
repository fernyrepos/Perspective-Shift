using HarmonyLib;
using Verse;
using Verse.AI;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaul))]
    public static class HaulAIUtility_PawnCanAutomaticallyHaul_Patch
    {
        public static bool Prefix(Pawn p, Thing t, ref bool __result)
        {
            if (p.IsAvatar())
            {
                float grabRange = PerspectiveShiftMod.settings.grabRange + 1.5f;
                bool inRange = p.Position.DistanceTo(t.Position) <= grabRange;
                __result = inRange;
                return false;
            }
            return true;
        }
    }
}
