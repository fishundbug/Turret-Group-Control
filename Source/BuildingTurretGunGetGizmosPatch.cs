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

            yield return OpenManagementWindowCommand(__instance.Map);
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

        private static Command_Action OpenManagementWindowCommand(Map map)
        {
            return new Command_Action
            {
                defaultLabel = "TurretGroupControl_OpenManagementWindow".Translate(),
                defaultDesc = "TurretGroupControl_OpenManagementWindowDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/OpenDebugActionsMenu", false),
                action = delegate
                {
                    Find.WindowStack.Add(new TurretGroupManagementWindow(map));
                }
            };
        }

        private static Command_Action CreateNewGroupCommand(TurretGroupManager manager, List<Thing> selectedTurrets)
        {
            return new Command_Action
            {
                defaultLabel = "TurretGroupControl_CreateNewGroup".Translate(),
                defaultDesc = "TurretGroupControl_CreateNewGroupDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/FormCaravan", false),
                action = delegate
                {
                    var group = manager.CreateGroup(selectedTurrets);
                    Messages.Message("TurretGroupControl_CreatedGroup".Translate(group.name, group.members.Count), MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }

        private static Command_Action AddToExistingGroupCommand(TurretGroupManager manager, TurretGroupData group, List<Thing> selectedTurrets)
        {
            return new Command_Action
            {
                defaultLabel = "TurretGroupControl_AddToGroup".Translate(group.name),
                defaultDesc = "TurretGroupControl_AddToGroupDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Install", false),
                action = delegate
                {
                    for (int i = 0; i < selectedTurrets.Count; i++)
                    {
                        manager.AddMember(group, selectedTurrets[i]);
                    }
                    Messages.Message("TurretGroupControl_AddedToGroup".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }

        private static Command_Action SelectGroupCommand(TurretGroupManager manager, TurretGroupData group)
        {
            return new Command_Action
            {
                defaultLabel = "TurretGroupControl_SelectGroup".Translate(),
                defaultDesc = "TurretGroupControl_SelectGroupDesc".Translate(group.name),
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
                defaultLabel = "TurretGroupControl_RemoveFromGroup".Translate(),
                defaultDesc = "TurretGroupControl_RemoveFromGroupDesc".Translate(group.name),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Cancel", false),
                action = delegate
                {
                    manager.RemoveMember(turret);
                    Messages.Message("TurretGroupControl_RemovedFromGroup".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }

        private static Command_Action SetGroupHoldFireCommand(TurretGroupManager manager, TurretGroupData group, bool holdFire)
        {
            return new Command_Action
            {
                defaultLabel = holdFire ? "TurretGroupControl_GroupHoldFire".Translate() : "TurretGroupControl_GroupFireAtWill".Translate(),
                defaultDesc = holdFire ? "TurretGroupControl_GroupHoldFireDesc".Translate(group.name) : "TurretGroupControl_GroupFireAtWillDesc".Translate(group.name),
                icon = ContentFinder<Texture2D>.Get(holdFire ? "UI/Commands/HoldFire" : "UI/Commands/Attack", false),
                action = delegate
                {
                    manager.ToggleHoldFire(group.id, holdFire);
                    Messages.Message(holdFire ? "TurretGroupControl_GroupHoldFireSet".Translate(group.name) : "TurretGroupControl_GroupFireAtWillSet".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }
    }
}
