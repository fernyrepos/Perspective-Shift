using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Pawn_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            if (State.IsActive && __instance == State.Avatar?.pawn && !State.DrawingTopRightGizmos)
            {
                yield break;
            }

            foreach (var g in values)
            {
                if (__instance.IsAvatar() && g is Command_Toggle toggle && toggle.tutorTag == "FireAtWillToggle")
                    continue;

                yield return g;
            }

            if (!__instance.IsColonist && !PerspectiveShiftMod.settings.allowNonHuman) yield break;

            if (!PerspectiveShiftMod.settings.totalFreedom)
            {
                if (State.CurrentMode == PlaystyleMode.Director) yield break;

                if (State.CurrentMode == PlaystyleMode.Authentic)
                {
                    if (__instance == State.Current?.pawn)
                    {
                        yield return new Command_Action
                        {
                            defaultLabel = "PS_AuthenticLocked".Translate(),
                            icon = ContentFinder<Texture2D>.Get("Gizmos/TakeControl"),
                            disabled = true,
                            disabledReason = "PS_AuthenticLockedReason".Translate()
                        };
                    }
                    yield break;
                }
            }

            if (__instance != State.Current?.pawn || State.Current == null)
            {
                yield return new Command_Action
                {
                    defaultLabel = State.CurrentMode == PlaystyleMode.Dynamic && State.Current == null
                        ? "PS_TakeControl".Translate()
                        : "PS_SwapCharacter".Translate(),
                    icon = ContentFinder<Texture2D>.Get(State.CurrentMode == PlaystyleMode.Dynamic && State.Current == null ? "Gizmos/TakeControl" : "Gizmos/SwapPOV"),
                    action = () => State.SetAvatar(__instance, showMessage: true),
                    disabled = __instance.InMentalState,
                    disabledReason = "PawnIsInMentalState".Translate(__instance)
                };
            }

            if (__instance == State.Current?.pawn && (State.CurrentMode == PlaystyleMode.Dynamic || PerspectiveShiftMod.settings.totalFreedom))
            {
                yield return new Command_Action
                {
                    defaultLabel = "PS_ReturnToDirector".Translate(),
                    icon = ContentFinder<Texture2D>.Get("Gizmos/DirectorMode"),
                    action = () => State.ClearAvatar()
                };
            }
        }
    }
}
