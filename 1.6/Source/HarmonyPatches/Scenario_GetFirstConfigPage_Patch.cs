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
            while (curr != null)
            {
                if (curr is Page_SelectStoryteller)
                {
                    if (!(curr.next is Page_ChoosePerspective))
                    {
                        Page next = curr.next;
                        var perspectivePage = new Page_ChoosePerspective();
                        curr.next = perspectivePage;
                        perspectivePage.prev = curr;
                        perspectivePage.next = next;
                        if (next != null) next.prev = perspectivePage;
                    }
                }
                curr = curr.next;
            }

            Page last = __result;
            while (last.next != null) last = last.next;

            if (!(last is Page_ChooseStartingCharacter))
            {
                var characterPage = new Page_ChooseStartingCharacter();
                characterPage.nextAct = last.nextAct;
                last.nextAct = null;
                last.next = characterPage;
                characterPage.prev = last;
            }
        }
    }
}
