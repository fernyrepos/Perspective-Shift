using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Page_ConfigureStartingPawns), nameof(Page_ConfigureStartingPawns.DoWindowContents))]
    public static class Page_ConfigureStartingPawns_DoWindowContents_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var inst in instructions)
            {
                if (inst.opcode == OpCodes.Ldstr && (string)inst.operand == "Start")
                {
                    inst.operand = "Next";
                }
                yield return inst;
            }
        }
    }
}
