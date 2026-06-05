using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    public class TurretGroupManager : MapComponent
    {
        private Dictionary<int, TurretGroupData> groups = new Dictionary<int, TurretGroupData>();
        private Dictionary<Thing, TurretGroupData> groupByTurret = new Dictionary<Thing, TurretGroupData>();
        private int nextGroupId = 1;
        private static FieldInfo buildingTurretGunHoldFireField;
        private static bool buildingTurretGunHoldFireFieldResolved;

        public TurretGroupManager(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextGroupId, "nextGroupId", 1);
            Scribe_Collections.Look(ref groups, "groups", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                groups ??= new Dictionary<int, TurretGroupData>();
                CleanupAllGroups();
                RebuildMembershipIndex();
            }
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0)
            {
                return;
            }

            CleanupAllGroups();
        }

        public IEnumerable<TurretGroupData> AllGroups()
        {
            return groups.Values.OrderBy(g => g.id);
        }

        public TurretGroupData CreateGroup(IEnumerable<Thing> turrets)
        {
            int id = nextGroupId++;
            int defaultNameNumber = NextUnusedDefaultGroupNumber();
            var group = new TurretGroupData
            {
                id = id,
                name = DefaultGroupName(defaultNameNumber),
                members = new List<Thing>(),
                holdFire = false,
                powerOff = false
            };

            groups[group.id] = group;

            if (turrets != null)
            {
                foreach (var turret in turrets)
                {
                    AddMember(group, turret);
                }
            }

            return group;
        }

        public TurretGroupData CreateEmptyGroup()
        {
            return CreateGroup(null);
        }

        public TurretGroupData GetGroup(int id)
        {
            groups.TryGetValue(id, out var group);
            return group;
        }

        public TurretGroupData FindGroupFor(Thing thing)
        {
            if (thing == null || thing.DestroyedOrNull())
            {
                return null;
            }

            EnsureMembershipIndex();
            if (!groupByTurret.TryGetValue(thing, out var group))
            {
                return null;
            }

            if (group == null || !groups.TryGetValue(group.id, out var currentGroup) || currentGroup != group || group.members == null || !group.members.Contains(thing))
            {
                groupByTurret.Remove(thing);
                return null;
            }

            return group;
        }

        public void AddMember(int groupId, Thing turret)
        {
            if (!groups.TryGetValue(groupId, out var group))
            {
                return;
            }

            AddMember(group, turret);
        }

        public void AddMember(TurretGroupData group, Thing turret)
        {
            if (group == null || turret == null || !IsSupportedTurret(turret))
            {
                return;
            }

            EnsureMembershipIndex();
            RemoveMember(turret);
            group.CleanupMembers();
            if (!group.members.Contains(turret))
            {
                group.members.Add(turret);
            }
            groupByTurret[turret] = group;
            ApplyHoldFireToTurret(turret, group.holdFire);
            SetTurretPowerOff(turret, group.powerOff);
        }

        public void RemoveMember(Thing turret)
        {
            if (turret == null)
            {
                return;
            }

            EnsureMembershipIndex();
            if (groupByTurret.TryGetValue(turret, out var indexedGroup) && indexedGroup?.members != null)
            {
                indexedGroup.members.RemoveAll(t => t == turret);
                groupByTurret.Remove(turret);
                return;
            }

            bool removed = false;
            foreach (var group in groups.Values)
            {
                if (group?.members == null)
                {
                    continue;
                }

                if (group.members.RemoveAll(t => t == turret) > 0)
                {
                    removed = true;
                }
            }

            if (removed)
            {
                groupByTurret.Remove(turret);
            }
        }

        public void RemoveMember(int groupId, Thing turret)
        {
            if (turret == null || !groups.TryGetValue(groupId, out var group))
            {
                return;
            }

            if (group.members?.RemoveAll(t => t == turret) > 0)
            {
                if (groupByTurret.TryGetValue(turret, out var indexedGroup) && indexedGroup == group)
                {
                    groupByTurret.Remove(turret);
                }
            }
        }

        public bool RenameGroup(int groupId, string newName)
        {
            if (!groups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            newName = (newName ?? string.Empty).Trim();
            if (newName.NullOrEmpty())
            {
                newName = "TurretGroupControl_DefaultGroupName".Translate(group.id).ToString();
            }

            group.name = newName;
            return true;
        }

        public bool DeleteGroup(int groupId)
        {
            if (!groups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            RemoveIndexEntriesForGroup(group);
            return groups.Remove(groupId);
        }

        public void MoveMember(Thing turret, int targetGroupId)
        {
            AddMember(targetGroupId, turret);
        }

        public void ToggleHoldFire(int groupId, bool holdFire)
        {
            if (!groups.TryGetValue(groupId, out var group))
            {
                return;
            }

            group.holdFire = holdFire;
            ApplyHoldFire(group);
        }

        public void TogglePowerOff(int groupId, bool powerOff)
        {
            if (!groups.TryGetValue(groupId, out var group))
            {
                return;
            }

            group.powerOff = powerOff;
            ApplyPowerState(group);
        }

        public void ApplyPowerState(TurretGroupData group)
        {
            if (group == null)
            {
                return;
            }

            group.CleanupMembers();
            RebuildMembershipIndex();
            foreach (var thing in group.members)
            {
                SetTurretPowerOff(thing, group.powerOff);
            }
        }

        public void ApplyHoldFire(TurretGroupData group)
        {
            if (group == null)
            {
                return;
            }

            group.CleanupMembers();
            RebuildMembershipIndex();
            foreach (var thing in group.members)
            {
                ApplyHoldFireToTurret(thing, group.holdFire);
            }
        }

        public void SelectGroup(TurretGroupData group)
        {
            if (group == null)
            {
                return;
            }

            group.CleanupMembers();
            RebuildMembershipIndex();
            var selector = Find.Selector;
            selector.ClearSelection();
            foreach (var thing in group.members)
            {
                if (thing != null && thing.Spawned)
                {
                    selector.Select(thing);
                }
            }
        }

        public void CleanupAllGroups()
        {
            CleanupInvalidMembers();

            if (TurretGroupMod.Settings?.autoRemoveEmptyGroups != true || groups == null || groups.Count == 0)
            {
                return;
            }

            var emptyGroups = new List<int>();
            foreach (var kvp in groups)
            {
                var group = kvp.Value;
                if (group == null || group.members == null || group.members.Count == 0)
                {
                    emptyGroups.Add(kvp.Key);
                }
            }

            for (int i = 0; i < emptyGroups.Count; i++)
            {
                if (groups.TryGetValue(emptyGroups[i], out var group))
                {
                    RemoveIndexEntriesForGroup(group);
                }
                groups.Remove(emptyGroups[i]);
            }
        }

        public void CleanupInvalidMembers()
        {
            if (groups == null || groups.Count == 0)
            {
                groupByTurret?.Clear();
                return;
            }

            foreach (var group in groups.Values)
            {
                group?.CleanupMembers();
            }

            RebuildMembershipIndex();
        }

        private void EnsureMembershipIndex()
        {
            if (groupByTurret == null)
            {
                RebuildMembershipIndex();
            }
        }

        private void RebuildMembershipIndex()
        {
            groupByTurret = new Dictionary<Thing, TurretGroupData>();
            if (groups == null || groups.Count == 0)
            {
                return;
            }

            foreach (var group in groups.Values)
            {
                if (group?.members == null)
                {
                    continue;
                }

                for (int i = 0; i < group.members.Count; i++)
                {
                    var member = group.members[i];
                    if (member == null || member.DestroyedOrNull())
                    {
                        continue;
                    }

                    groupByTurret[member] = group;
                }
            }
        }

        private void RemoveIndexEntriesForGroup(TurretGroupData group)
        {
            if (group == null || groupByTurret == null)
            {
                return;
            }

            if (group.members == null)
            {
                return;
            }

            for (int i = 0; i < group.members.Count; i++)
            {
                var member = group.members[i];
                if (member != null && groupByTurret.TryGetValue(member, out var indexedGroup) && indexedGroup == group)
                {
                    groupByTurret.Remove(member);
                }
            }
        }

        private int NextUnusedDefaultGroupNumber()
        {
            int number = 1;
            while (groups != null && groups.Values.Any(group => group != null && group.name == DefaultGroupName(number)))
            {
                number++;
            }

            return number;
        }

        private static string DefaultGroupName(int number)
        {
            return "TurretGroupControl_DefaultGroupName".Translate(number).ToString();
        }

        public static bool IsSupportedTurret(Thing thing)
        {
            return thing is Building_Turret || thing is Building_TurretGun;
        }

        public static void SetTurretHoldFire(Thing thing, bool holdFire)
        {
            if (thing == null)
            {
                return;
            }

            if (thing is Building_TurretGun)
            {
                EnsureTurretGunHoldFireField();
                if (buildingTurretGunHoldFireField != null)
                {
                    buildingTurretGunHoldFireField.SetValue(thing, holdFire);
                }
                return;
            }

            var field = thing.GetType().GetField("holdFire", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(thing, holdFire);
            }
        }

        public static void SetTurretPowerOff(Thing thing, bool powerOff)
        {
            if (thing == null)
            {
                return;
            }

            var flickable = thing.TryGetComp<CompFlickable>();
            if (flickable == null)
            {
                return;
            }

            bool desiredOn = !powerOff;
            flickable.wantSwitchOn = desiredOn;
            flickable.SwitchIsOn = desiredOn;
            FlickUtility.UpdateFlickDesignation(thing);
        }

        private static void ApplyHoldFireToTurret(Thing thing, bool holdFire)
        {
            SetTurretHoldFire(thing, holdFire);
        }

        private static void EnsureTurretGunHoldFireField()
        {
            if (buildingTurretGunHoldFireFieldResolved)
            {
                return;
            }

            buildingTurretGunHoldFireFieldResolved = true;
            buildingTurretGunHoldFireField = typeof(Building_TurretGun).GetField("holdFire", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
    }
}
