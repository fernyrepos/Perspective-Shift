using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShift
{
	public static class FloatMenuMap_AvatarTracker
	{
		public static ConditionalWeakTable<FloatMenuMap, Pawn> menuOwners = new ConditionalWeakTable<FloatMenuMap, Pawn>();
		public static Pawn currentMenuSubject;
		public static FloatMenuMap currentlyUpdating;
		private static Pawn _oldMakingFor;
		public static void SetContext(FloatMenuMap instance)
		{
			currentlyUpdating = instance;
			_oldMakingFor = FloatMenuMakerMap.makingFor;
			if (menuOwners.TryGetValue(instance, out Pawn owner))
			{
				FloatMenuMakerMap.makingFor = owner;
			}
		}

		public static void RestoreContext()
		{
			FloatMenuMakerMap.makingFor = _oldMakingFor;
			currentlyUpdating = null;
		}

		public static List<Pawn> GetEffectiveSelectedPawns(Selector instance)
		{
			var actual = instance.SelectedPawns;
			if (actual.Any() || currentlyUpdating == null) return actual;

			if (menuOwners.TryGetValue(currentlyUpdating, out Pawn owner))
				return new List<Pawn> { owner };

			return actual;
		}

		public static bool GetEffectiveAnyPawnSelected(Selector instance)
		{
			if (instance.AnyPawnSelected) return true;
			if (currentlyUpdating == null) return false;
			return menuOwners.TryGetValue(currentlyUpdating, out _);
		}
	}

	[HarmonyPatch]
	public static class FloatMenuContext_Constructor_Patch
	{
		public static MethodBase TargetMethod() => 
			AccessTools.Constructor(typeof(FloatMenuContext), new[] { typeof(List<Pawn>), typeof(UnityEngine.Vector3), typeof(Map) });

		public static void Prefix(ref List<Pawn> selectedPawns)
		{
			selectedPawns = [.. selectedPawns];
		}
	}

	[HarmonyPatch]
	public static class FloatMenuMap_Constructor_Patch
	{
		public static MethodBase TargetMethod() => 
			AccessTools.Constructor(typeof(FloatMenuMap), new[] { typeof(List<FloatMenuOption>), typeof(string), typeof(UnityEngine.Vector3) });

		public static void Postfix(FloatMenuMap __instance)
		{
			if (FloatMenuMap_AvatarTracker.currentMenuSubject != null)
				FloatMenuMap_AvatarTracker.menuOwners.Add(__instance, FloatMenuMap_AvatarTracker.currentMenuSubject);
		}
	}

	[HarmonyPatch]
	public static class FloatMenuMap_Context_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(FloatMenuMap), nameof(FloatMenuMap.DoWindowContents));
			yield return AccessTools.Method(typeof(FloatMenuMap), nameof(FloatMenuMap.PreOptionChosen));
		}

		public static void Prefix(FloatMenuMap __instance) => FloatMenuMap_AvatarTracker.SetContext(__instance);
		public static void Postfix() => FloatMenuMap_AvatarTracker.RestoreContext();
	}

	[HarmonyPatch]
	public static class FloatMenuMap_Transpiler_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(FloatMenuMap), nameof(FloatMenuMap.DoWindowContents));
			yield return AccessTools.Method(typeof(FloatMenuMap), nameof(FloatMenuMap.PreOptionChosen));
			yield return AccessTools.Method(typeof(FloatMenuMap), "StillValid");
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var anyPawnSelectedGetter = AccessTools.PropertyGetter(typeof(Selector), nameof(Selector.AnyPawnSelected));
			var selectedPawnsGetter = AccessTools.PropertyGetter(typeof(Selector), nameof(Selector.SelectedPawns));

			var replacementAny = AccessTools.Method(typeof(FloatMenuMap_AvatarTracker), nameof(FloatMenuMap_AvatarTracker.GetEffectiveAnyPawnSelected));
			var replacementPawns = AccessTools.Method(typeof(FloatMenuMap_AvatarTracker), nameof(FloatMenuMap_AvatarTracker.GetEffectiveSelectedPawns));

			foreach (var inst in instructions)
			{
				if (inst.Calls(anyPawnSelectedGetter))
					yield return new CodeInstruction(OpCodes.Call, replacementAny);
				else if (inst.Calls(selectedPawnsGetter))
					yield return new CodeInstruction(OpCodes.Call, replacementPawns);
				else
					yield return inst;
			}
		}
	}
}
