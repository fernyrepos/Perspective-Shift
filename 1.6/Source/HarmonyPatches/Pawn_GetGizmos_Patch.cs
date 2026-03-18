using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HotSwappable]
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Pawn_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            if (State.IsActive && __instance == State.Avatar?.pawn && !State.DrawingTopRightGizmos && !PerspectiveShiftMod.settings.disableCustomGizmos)
            {
                yield break;
            }

            if (__instance.IsAvatar())
            {
                if (__instance.drafter == null)
                    __instance.drafter = new Pawn_DraftController(__instance);

                if (__instance.equipment == null) __instance.equipment = new Pawn_EquipmentTracker(__instance);
                if (__instance.story == null) __instance.story = new Pawn_StoryTracker(__instance);
                if (__instance.skills == null) __instance.skills = new Pawn_SkillTracker(__instance);
                if (__instance.playerSettings == null) __instance.playerSettings = new Pawn_PlayerSettings(__instance);

                bool nativelyYieldsDraft = __instance.IsColonistPlayerControlled || __instance.IsColonyMech || __instance.IsColonySubhumanPlayerControlled;
                if (!nativelyYieldsDraft)
                {
                    foreach (var g in __instance.drafter.GetGizmos())
                    {
                        if (g is Command_Toggle toggle && toggle.tutorTag == "FireAtWillToggle")
                            continue;
                        yield return g;
                    }
                }
            }

            foreach (var g in values)
            {
                if (__instance.IsAvatar() && g is Command_Toggle toggle && toggle.tutorTag == "FireAtWillToggle")
                    continue;

                yield return g;
            }

            if (__instance != State.Avatar?.pawn && __instance.Faction == Faction.OfPlayer)
            {
                State.seekAtWillPawns ??= new HashSet<int>();
                bool isRanged = __instance.equipment?.Primary?.def?.IsRangedWeapon ?? false;
                var seekAtWill = new Command_Toggle
                {
                    defaultLabel = "PS_SeekAtWill".Translate(),
                    defaultDesc = "PS_SeekAtWillDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get(isRanged ? "Gizmos/SeekAtWill_Ranged" : "Gizmos/SeekAtWill_Melee"),
                    isActive = () => State.seekAtWillPawns.Contains(__instance.thingIDNumber),
                    toggleAction = () =>
                    {
                        if (State.seekAtWillPawns.Contains(__instance.thingIDNumber))
                        {
                            State.seekAtWillPawns.Remove(__instance.thingIDNumber);
                        }
                        else
                        {
                            State.seekAtWillPawns.Add(__instance.thingIDNumber);
                        }
                    },
                };

                if (__instance.Drafted)
                {
                    seekAtWill.Disable("PS_SeekAtWillDraftedReason".Translate());
                }
                yield return seekAtWill;
            }

            if (!__instance.IsColonist && !PerspectiveShiftMod.settings.allowNonHuman) yield break;
            if (!PerspectiveShiftMod.settings.totalFreedom && PerspectiveShiftMod.settings.requirePawnInFaction && __instance.Faction != Faction.OfPlayer) yield break;

            if (!PerspectiveShiftMod.settings.totalFreedom)
            {
                if (State.CurrentMode == PlaystyleMode.Director) yield break;

                if (State.CurrentMode == PlaystyleMode.Authentic)
                {
                    if (__instance == State.Avatar?.pawn)
                    {
                        if (State.allowDirectorInAuthentic)
                        {
                            yield return new Command_Action
                            {
                                defaultLabel = "PS_ReturnToDirector".Translate(),
                                icon = ContentFinder<Texture2D>.Get("Gizmos/DirectorMode"),
                                action = () => State.ClearAvatar()
                            };
                        }
                        else
                        {
                            yield return new Command_Action
                            {
                                defaultLabel = "PS_AuthenticLocked".Translate(),
                                icon = ContentFinder<Texture2D>.Get("Gizmos/TakeControl"),
                                disabled = true,
                                disabledReason = "PS_AuthenticLockedReason".Translate()
                            };
                        }
                    }
                    else if (State.allowDirectorInAuthentic
                        && State.Avatar == null
                        && __instance.thingIDNumber == State.authenticPawnId)
                    {
                        yield return new Command_Action
                        {
                            defaultLabel = "PS_TakeControl".Translate(),
                            icon = ContentFinder<Texture2D>.Get("Gizmos/TakeControl"),
                            action = () => State.SetAvatar(__instance, showMessage: true)
                        };
                    }
                    yield break;
                }
            }

            if (__instance != State.Avatar?.pawn || State.Avatar == null)
            {
                yield return new Command_Action
                {
                    defaultLabel = State.CurrentMode == PlaystyleMode.Dynamic && State.Avatar == null
                        ? "PS_TakeControl".Translate()
                        : "PS_SwapCharacter".Translate(),
                    icon = ContentFinder<Texture2D>.Get(State.CurrentMode == PlaystyleMode.Dynamic && State.Avatar == null ? "Gizmos/TakeControl" : "Gizmos/SwapPOV"),
                    action = () => State.SetAvatar(__instance, showMessage: true),
                    disabled = __instance.InMentalState,
                    disabledReason = "PawnIsInMentalState".Translate(__instance)
                };
            }

            if (__instance == State.Avatar?.pawn && (State.CurrentMode == PlaystyleMode.Dynamic || PerspectiveShiftMod.settings.totalFreedom))
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
