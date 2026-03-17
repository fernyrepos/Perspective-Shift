using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Dialog_Options), "DoGameplayOptions")]
    public static class Dialog_Options_DoGameplayOptions_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool injected = false;
            foreach (var inst in instructions)
            {
                if (!injected && inst.opcode == OpCodes.Ldstr && (string)inst.operand == "MaxNumberOfPlayerSettlements")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(Dialog_Options_DoGameplayOptions_Patch), nameof(InjectButton)));
                    injected = true;
                }
                yield return inst;
            }
        }

        private static void InjectButton(Listing_Standard listing)
        {
            if (!State.permadeath && Current.ProgramState == ProgramState.Playing)
            {
                if (listing.ButtonTextLabeledPct("PS_PerspectiveSettings".Translate(), "PS_Modify".Translate(), 0.6f, TextAnchor.UpperLeft))
                {
                    Find.WindowStack.Add(new Page_ChoosePerspective());
                }
            }
        }
    }
}
