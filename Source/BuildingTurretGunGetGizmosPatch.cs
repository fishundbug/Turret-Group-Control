using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    [HarmonyPatch]
    public static class BuildingTurretGunGetGizmosPatch
    {
        private static readonly Texture2D ManageIcon = TexButton.ToggleLog;
        private static readonly Texture2D CreateGroupIcon = TexButton.Plus;
        private static readonly Texture2D AddToGroupIcon = TexCommand.DesirePower;
        private static readonly Texture2D SelectGroupIcon = TexCommand.Attack;
        private static readonly Texture2D RemoveFromGroupIcon = TexButton.Delete;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in AccessTools.AllTypes())
            {
                if (type == null || type.IsAbstract || !typeof(Building_Turret).IsAssignableFrom(type))
                {
                    continue;
                }

                var method = AccessTools.DeclaredMethod(type, nameof(Thing.GetGizmos));
                if (method != null && method.GetParameters().Length == 0 && typeof(IEnumerable<Gizmo>).IsAssignableFrom(method.ReturnType))
                {
                    yield return method;
                }
            }
        }

        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Thing __instance)
        {
            var existingGizmos = values?.ToList() ?? new List<Gizmo>();
            if (existingGizmos.OfType<Command>().Any(command => command.defaultLabel == "TurretGroupControl_OpenManagementWindow".Translate().ToString()))
            {
                return existingGizmos;
            }

            return AppendTurretGroupGizmos(existingGizmos, __instance);
        }

        private static IEnumerable<Gizmo> AppendTurretGroupGizmos(List<Gizmo> values, Thing turret)
        {
            foreach (var gizmo in values)
            {
                yield return gizmo;
            }

            if (turret == null || !turret.Spawned || turret.Faction != Faction.OfPlayer || !TurretGroupManager.IsSupportedTurret(turret))
            {
                yield break;
            }

            var manager = TurretGroupUtility.GetManager(turret.Map);
            if (manager == null)
            {
                yield break;
            }

            var group = manager.FindGroupFor(turret);
            var selectedTurrets = TurretGroupUtility.GetSelectedSupportedTurrets().ToList();
            if (selectedTurrets.Count == 0)
            {
                selectedTurrets.Add(turret);
            }

            yield return OpenManagementWindowCommand(turret.Map);

            bool anySelectedTurretAlreadyGrouped = selectedTurrets.Any(selected => manager.FindGroupFor(selected) != null);
            if (!anySelectedTurretAlreadyGrouped)
            {
                yield return CreateNewGroupCommand(manager, selectedTurrets);
            }

            if (group != null)
            {
                yield return SelectGroupCommand(manager, group);
                yield return RemoveFromGroupCommand(manager, turret, group);
                yield return SetGroupHoldFireCommand(manager, group, true);
                yield return SetGroupHoldFireCommand(manager, group, false);
            }
            else if (manager.AllGroups().Any())
            {
                yield return AddToExistingGroupMenuCommand(manager, selectedTurrets);
            }
        }

        private static Command_Action OpenManagementWindowCommand(Map map)
        {
            return new Command_Action
            {
                defaultLabel = "TurretGroupControl_OpenManagementWindow".Translate(),
                defaultDesc = "TurretGroupControl_OpenManagementWindowDesc".Translate(),
                icon = ManageIcon,
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
                icon = CreateGroupIcon,
                action = delegate
                {
                    var group = manager.CreateGroup(selectedTurrets);
                    Messages.Message("TurretGroupControl_CreatedGroup".Translate(group.name, group.members.Count), MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }

        private static Command_Action AddToExistingGroupMenuCommand(TurretGroupManager manager, List<Thing> selectedTurrets)
        {
            return new Command_Action
            {
                defaultLabel = "TurretGroupControl_AddToGroupMenu".Translate(),
                defaultDesc = "TurretGroupControl_AddToGroupMenuDesc".Translate(),
                icon = AddToGroupIcon,
                action = delegate
                {
                    var options = new List<FloatMenuOption>();
                    foreach (var group in manager.AllGroups())
                    {
                        var localGroup = group;
                        options.Add(new FloatMenuOption(localGroup.name, delegate
                        {
                            for (int i = 0; i < selectedTurrets.Count; i++)
                            {
                                manager.AddMember(localGroup, selectedTurrets[i]);
                            }
                            Messages.Message("TurretGroupControl_AddedToGroup".Translate(localGroup.name), MessageTypeDefOf.TaskCompletion, false);
                        }));
                    }

                    if (options.Count == 0)
                    {
                        options.Add(new FloatMenuOption("TurretGroupControl_NoGroups".Translate(), null));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        private static Command_Action SelectGroupCommand(TurretGroupManager manager, TurretGroupData group)
        {
            return new Command_Action
            {
                defaultLabel = "TurretGroupControl_SelectGroup".Translate(),
                defaultDesc = "TurretGroupControl_SelectGroupDesc".Translate(group.name),
                icon = SelectGroupIcon,
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
                icon = RemoveFromGroupIcon,
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
                icon = holdFire ? TexCommand.DesirePower : TexCommand.Attack,
                action = delegate
                {
                    manager.ToggleHoldFire(group.id, holdFire);
                    Messages.Message(holdFire ? "TurretGroupControl_GroupHoldFireSet".Translate(group.name) : "TurretGroupControl_GroupFireAtWillSet".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }
    }
}
