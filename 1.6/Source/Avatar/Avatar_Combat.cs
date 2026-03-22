using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PerspectiveShift
{
    public partial class Avatar
    {
        public static bool IsAvatarLeftClick = false;
        private static Texture2D _reticleTex;
        public static Texture2D ReticleTex => _reticleTex ??= ContentFinder<Texture2D>.Get("UI/Reticle");
        private static Texture2D _reticleCooldownTex;
        public static Texture2D ReticleCooldownTex => _reticleCooldownTex ??= ContentFinder<Texture2D>.Get("UI/ReticleCooldown");
        private static Texture2D _reticleNoLOSTex;
        public static Texture2D ReticleNoLOSTex => _reticleNoLOSTex ??= ContentFinder<Texture2D>.Get("UI/ReticleNoLOS");

        public bool HandleSelectorClick()
        {
            if (Find.Targeter.IsTargeting) return false;
            if (pawn.Downed) return false;
            if (pawn.InMentalState || passedOut) return false;

            if (ModCompatibility.IsPawnInVehicle(pawn, out Pawn veh, out bool isDriver, out bool isGunner))
            {
                if (isGunner && Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0)
                    {
                        ModCompatibility.FireVehicleWeapons(veh, pawn, UI.MouseMapPosition());
                        Event.current.Use();
                        return true;
                    }
                    else if (Event.current.button == 1)
                    {
                        ModCompatibility.ClearVehicleWeapons(veh, pawn);
                        Event.current.Use();
                        return true;
                    }
                }
                return false;
            }

            if (Find.TickManager.Paused) return false;
            if (State.CameraLockPosition.HasValue) return false;

            if (IsMouseOverUI() || IsMouseOverColonistBar()) return false;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (pawn.Drafted)
                {
                    if (MouseIsOverPawn()) return false;

                    HandleFiring();
                    return true;
                }
                else
                {
                    return HandleLeftClick();
                }
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                if (pawn.carryTracker?.CarriedThing != null && pawn.inventory != null && pawn.carryTracker.CarriedThing is not (Pawn or Corpse))
                {
                    var carried = pawn.carryTracker.CarriedThing;
                    int count = carried.stackCount;

                    if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, carried, count))
                    {
                        var maxCount = MassUtility.CountToPickUpUntilOverEncumbered(pawn, carried);
                        if (maxCount <= 0)
                        {
                            Messages.Message("PS_CannotCarryMoreWeight".Translate(), MessageTypeDefOf.RejectInput, false);
                            Event.current.Use();
                            return true;
                        }
                        count = maxCount;
                    }

                    var transferred = pawn.carryTracker.innerContainer.TryTransferToContainer(carried, pawn.inventory.innerContainer, count);
                    if (transferred > 0)
                    {
                        DefsOf.PS_PackInventory.PlayOneShotOnCamera();
                        Event.current.Use();
                        return true;
                    }
                }

                if (pawn.Drafted)
                {
                    var otherPawnsSelected = Find.Selector.SelectedObjects
                        .Any(o => o is Pawn p && p != pawn);
                    if (otherPawnsSelected)
                        return false;

                    if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def.playerInterruptible)
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);

                    Event.current.Use();
                    return true;
                }

                if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def.playerInterruptible)
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            return false;
        }

        private void HandleFiring()
        {
            if (pawn.stances.curStance is Stance_Busy) return;
            if (IsAbilityCastJob()) return;
            var verb = GetActiveVerb();
            if (verb == null) return;

            var targetCell = UI.MouseCell();
            if (!targetCell.InBounds(pawn.Map)) return;

            var target = GetBestTarget(targetCell);
            Vector3 targetPos = target.Thing != null ? target.Thing.DrawPos : targetCell.ToVector3Shifted();
            Vector3 toTarget = targetPos - pawn.DrawPos;
            if (toTarget.sqrMagnitude > 0.01f)
                pawn.Rotation = Rot4.FromAngleFlat(NormAngle(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg));

            if (verb.verbProps.IsMeleeAttack)
            {
                if (pawn.Position.AdjacentTo8WayOrInside(targetCell) && target.Thing != null)
                    pawn.meleeVerbs.TryMeleeAttack(target.Thing);
            }
            else
            {
                if (verb.CanHitTarget(target))
                    verb.TryStartCastOn(target, false, true);
            }
        }

        private void HandleCombatStance()
        {
            if (!pawn.Drafted || pawn.stances.curStance == null) return;

            bool isMoving = IsMoving;

            if (isMoving)
            {
                var rgActive = ModCompatibility.IsRunAndGunActiveFor(pawn, out string rgReason);
                if (rgActive)
                {
                    var stance = pawn.stances.curStance;
                    if (stance is Stance_Warmup warmup && stance.GetType() != ModCompatibility.stanceRunAndGunType)
                    {
                        ModCompatibility.ConvertToRunAndGunStance(pawn, warmup);
                    }
                    else if (stance is Stance_Cooldown cooldown && stance.GetType() != ModCompatibility.stanceRunAndGunCooldownType)
                    {
                        ModCompatibility.ConvertToRunAndGunCooldownStance(pawn, cooldown);
                    }
                    return;
                }

            }
            else
            {
                if (ModCompatibility.IsRunAndGunActiveFor(pawn))
                {
                    var stance = pawn.stances.curStance;
                    if (ModCompatibility.stanceRunAndGunType != null && stance.GetType() == ModCompatibility.stanceRunAndGunType && stance is Stance_Warmup warmup)
                    {
                        ModCompatibility.ConvertToVanillaWarmupStance(pawn, warmup);
                    }
                    else if (ModCompatibility.stanceRunAndGunCooldownType != null && stance.GetType() == ModCompatibility.stanceRunAndGunCooldownType && stance is Stance_Cooldown cooldown)
                    {
                        ModCompatibility.ConvertToVanillaCooldownStance(pawn, cooldown);
                    }
                }
            }
        }

        private void DrawReticle(Vector2 center)
        {
            if (pawn.stances.curStance is Stance_Busy)
            {
                bool isCooldown = pawn.stances.curStance is Stance_Cooldown;
                Color prev = GUI.color;
                GUI.color = isCooldown ? new Color(1f, 0.65f, 0f) : Color.white;
                float size = 32f;
                var rect = new Rect(center.x - size / 2f, center.y - size / 2f, size, size);
                var tex = isCooldown ? ReticleCooldownTex : ReticleTex;
                if (tex != null) GUI.DrawTexture(rect, tex);
                GUI.color = prev;
                return;
            }

            Color color = Color.green;
            Texture2D reticleTex = ReticleTex;

            var verb = GetActiveVerb();
            if (verb != null)
            {
                var targetCell = UI.MouseCell();
                if (!targetCell.InBounds(pawn.Map)) { LeanTarget = Vector3.zero; return; }

                var target = GetBestTarget(targetCell);
                if (!verb.CanHitTarget(target))
                {
                    color = Color.red;
                    reticleTex = ReticleNoLOSTex;
                    if (!IsMoving) LeanTarget = Vector3.zero;
                }
                else if (!IsMoving)
                {
                    UpdateLeanTarget(targetCell);
                }
            }

            Color prevColor = GUI.color;
            GUI.color = color;
            float sz = 32f;
            var r = new Rect(center.x - sz / 2f, center.y - sz / 2f, sz, sz);
            if (reticleTex != null) GUI.DrawTexture(r, reticleTex);
            GUI.color = prevColor;
        }

        private void UpdateLeanTarget(IntVec3 targetCell)
        {
            var leanSources = new List<IntVec3>();
            ShootLeanUtility.LeanShootingSourcesFromTo(pawn.Position, targetCell, pawn.Map, leanSources);
            var best = leanSources
                .Where(s => s != pawn.Position && s.IsValid && s != IntVec3.Zero
                            && GenSight.LineOfSight(s, targetCell, pawn.Map, skipFirstCell: true))
                .OrderBy(s => s.DistanceToSquared(targetCell))
                .FirstOrDefault();
            LeanTarget = (best != IntVec3.Zero && best != pawn.Position)
                ? (best - pawn.Position).ToVector3()
                : Vector3.zero;
        }

        private Verb GetActiveVerb()
        {
            var verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
            if (verb == null || verb.verbProps.IsMeleeAttack)
                verb = pawn.VerbTracker?.AllVerbs?.FirstOrDefault(v => v is Verb_MeleeAttack && v.Available());
            return verb;
        }

        private bool IsAbilityCastJob()
        {
            if (pawn.CurJob?.ability != null)
                return true;
            if (ModCompatibility.IsVEFAbilityCast(pawn))
                return true;
            return false;
        }

        private LocalTargetInfo GetBestTarget(IntVec3 targetCell)
        {
            var things = targetCell.GetThingList(pawn.Map);
            Thing best = things.FirstOrDefault(t => t is Pawn && t != pawn)
                ?? things.FirstOrDefault(t => t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item);
            return best != null ? new LocalTargetInfo(best) : new LocalTargetInfo(targetCell);
        }

        private void HandleHoldToFire(bool mouseOverGizmo, bool mouseOverUI)
        {

            if (Event.current.type == EventType.Repaint
                && pawn.Drafted
                && PerspectiveShiftMod.settings.holdToFire
                && Input.GetMouseButton(0)
                && !mouseOverUI && !mouseOverGizmo
                && !State.ControlsFrozen
                && !Find.Targeter.IsTargeting)
            {
                HandleFiring();
            }
        }

        private void UpdateCursorAndReticle(bool mouseOverGizmo, bool mouseOverUI)
        {

            if (pawn.Drafted && !pawn.InMentalState)
            {
                if (!Find.TickManager.Paused && Find.Selector.IsSelected(pawn) && !Find.Targeter.IsTargeting)
                {
                    if (!PerspectiveShiftMod.settings.disableCustomGizmos)
                        Find.Selector.Deselect(pawn);
                }

                if (!Find.TickManager.Paused && !State.ControlsFrozen && pawn.stances.curStance is not Stance_Busy)
                {
                    Vector3 toMouse = UI.MouseMapPosition() - pawn.DrawPos;
                    toMouse.y = 0f;
                    if (toMouse.sqrMagnitude >= 0.01f)
                    {
                        aimAngle = Mathf.Atan2(toMouse.x, toMouse.z) * Mathf.Rad2Deg;
                        if (aimAngle < 0f) aimAngle += 360f;
                    }
                }

                if (mouseOverUI || mouseOverGizmo || Find.TickManager.Paused || State.ControlsFrozen || Find.Targeter.IsTargeting)
                {
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.visible = false;
                    DrawReticle(Event.current.mousePosition);
                }
            }
            else
            {
                Cursor.visible = true;
            }
        }
    }
}
