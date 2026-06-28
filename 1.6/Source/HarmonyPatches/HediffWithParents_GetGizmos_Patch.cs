using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch]
    public static class HediffWithParents_GetGizmos_Patch
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod()
        {
            var nestedTypes = typeof(HediffWithParents).GetNestedTypes(AccessTools.all);
            foreach (var type in nestedTypes)
            {
                if (type.Name.Contains("<GetGizmos>"))
                {
                    var method = type.GetMethod("MoveNext", AccessTools.all);
                    if (method != null)
                    {
                        return method;
                    }
                }
            }
            Log.Error("[PerspectiveShift] Could not find compiler-generated MoveNext for HediffWithParents.GetGizmos");
            return null;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var getDraftedMethod = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Drafted));
            var isDraftedMethod = AccessTools.Method(typeof(HediffWithParents_GetGizmos_Patch), nameof(IsDrafted));
            var thisField = method.DeclaringType.GetField("<>4__this");

            var pawnField = AccessTools.Field(typeof(Hediff), nameof(Hediff.pawn));

            foreach (var instruction in instructions)
            {
                yield return instruction;

                if (instruction.opcode == OpCodes.Callvirt && (MethodInfo)instruction.operand == getDraftedMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, thisField);
                    yield return new CodeInstruction(OpCodes.Ldfld, pawnField);
                    yield return new CodeInstruction(OpCodes.Call, isDraftedMethod);
                }
            }
        }

        public static bool IsDrafted(bool original, Pawn pawn)
        {
            if (State.IsActive && pawn == State.Avatar?.pawn)
            {
                return false;
            }
            return original;
        }
    }
}
