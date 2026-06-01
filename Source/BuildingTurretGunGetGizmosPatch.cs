using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.GetGizmos))]
    public static class BuildingTurretGunGetGizmosPatch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Building_TurretGun __instance)
        {
            foreach (var gizmo in values)
            {
                yield return gizmo;
            }

            if (__instance == null || !__instance.Spawned || __instance.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            var manager = TurretGroupUtility.GetManager(__instance.Map);
            if (manager == null)
            {
                yield break;
            }

            var group = manager.FindGroupFor(__instance);
            var selectedTurrets = TurretGroupUtility.GetSelectedSupportedTurrets().ToList();
            if (selectedTurrets.Count == 0)
            {
                selectedTurrets.Add(__instance);
            }

            yield return CreateNewGroupCommand(manager, selectedTurrets);

            if (group != null)
            {
                yield return SelectGroupCommand(manager, group);
                yield return RemoveFromGroupCommand(manager, __instance, group);
                yield return SetGroupHoldFireCommand(manager, group, true);
                yield return SetGroupHoldFireCommand(manager, group, false);
            }
            else
            {
                foreach (var existing in manager.AllGroups())
                {
                    yield return AddToExistingGroupCommand(manager, existing, selectedTurrets);
                }
            }
        }

        private static Command_Action CreateNewGroupCommand(TurretGroupManager manager, List<Thing> selectedTurrets)
        {
            return new Command_Action
            {
                defaultLabel = selectedTurrets.Count > 1 ? "Create turret group" : "Create turret group",
                defaultDesc = "Create a new turret group from the currently selected turrets.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/FormCaravan", false),
                action = delegate
                {
                    var group = manager.CreateGroup(selectedTurrets);
                    Messages.Message($"Created {group.name} with {group.members.Count} turret(s).", MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }

        private static Command_Action AddToExistingGroupCommand(TurretGroupManager manager, TurretGroupData group, List<Thing> selectedTurrets)
        {
            return new Command_Action
            {
                defaultLabel = "Add to " + group.name,
                defaultDesc = "Add the currently selected turrets to this turret group.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Install", false),
                action = delegate
                {
                    for (int i = 0; i < selectedTurrets.Count; i++)
                    {
                        manager.AddMember(group, selectedTurrets[i]);
                    }
                    Messages.Message($"Added turret(s) to {group.name}.", MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }

        private static Command_Action SelectGroupCommand(TurretGroupManager manager, TurretGroupData group)
        {
            return new Command_Action
            {
                defaultLabel = "Select turret group",
                defaultDesc = $"Select every spawned turret in {group.name}.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/SelectAll", false),
                action = delegate
                {
                    manager.SelectGroup(group);
                }
            };
        }

        private static Command_Action RemoveFromGroupCommand(TurretGroupManager manager, Thing turret, TurretGroupData group)
        {
            return new Command_Action
            {
                defaultLabel = "Remove from turret group",
                defaultDesc = $"Remove this turret from {group.name}.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Cancel", false),
                action = delegate
                {
                    manager.RemoveMember(turret);
                    Messages.Message($"Removed turret from {group.name}.", MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }

        private static Command_Action SetGroupHoldFireCommand(TurretGroupManager manager, TurretGroupData group, bool holdFire)
        {
            return new Command_Action
            {
                defaultLabel = holdFire ? "Group hold fire" : "Group fire at will",
                defaultDesc = holdFire ? $"Set every turret in {group.name} to hold fire." : $"Allow every turret in {group.name} to fire at will.",
                icon = ContentFinder<Texture2D>.Get(holdFire ? "UI/Commands/HoldFire" : "UI/Commands/Attack", false),
                action = delegate
                {
                    manager.ToggleHoldFire(group.id, holdFire);
                    Messages.Message($"{group.name}: " + (holdFire ? "hold fire." : "fire at will."), MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }
    }
}
