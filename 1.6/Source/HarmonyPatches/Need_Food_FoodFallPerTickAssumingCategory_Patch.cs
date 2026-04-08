using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.FoodFallPerTickAssumingCategory))]
    public static class Need_Food_FoodFallPerTickAssumingCategory_Patch
    {
        public static void Postfix(Need_Food __instance, ref float __result)
        {
            if (State.IsActive && State.Avatar != null && State.Avatar.pawn == __instance.pawn && State.Avatar.isSprinting && State.Avatar.IsMoving)
            {
                __result *= PerspectiveShiftMod.settings.sprintFoodDrain;
            }
        }
    }
}
