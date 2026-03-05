using HarmonyLib;
using RimWorld;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Scenario), nameof(Scenario.GetFirstConfigPage))]
    public static class Scenario_GetFirstConfigPage_Patch
    {
        public static void Postfix(ref Page __result)
        {
            if (__result == null) return;

            Page curr = __result;
            Page configPawns = null;
            while (curr != null)
            {
                if (curr is Page_ConfigureStartingPawns)
                {
                    configPawns = curr;
                    break;
                }
                curr = curr.next;
            }

            if (configPawns != null && !(configPawns.next is Page_ChoosePerspective))
            {
                var perspectivePage = new Page_ChoosePerspective();
                var characterPage = new Page_ChooseStartingCharacter();

                perspectivePage.next = characterPage;
                characterPage.prev = perspectivePage;

                characterPage.nextAct = configPawns.nextAct;
                configPawns.nextAct = null;

                Page oldNext = configPawns.next;
                configPawns.next = perspectivePage;
                perspectivePage.prev = configPawns;

                if (oldNext != null)
                {
                    characterPage.next = oldNext;
                    oldNext.prev = characterPage;
                }
            }
        }
    }
}
