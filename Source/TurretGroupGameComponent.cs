using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TurretGroupControl
{
    public class TurretGroupGameComponent : GameComponent
    {
        private Dictionary<int, TurretGroupData> groups = new Dictionary<int, TurretGroupData>();
        private Dictionary<Thing, TurretGroupData> groupByTurret = new Dictionary<Thing, TurretGroupData>();
        private int nextGroupId = 1;

        public TurretGroupGameComponent(Game game)
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

        public override void GameComponentTick()
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
            if (group == null || turret == null || !TurretGroupUtility.IsSupportedTurret(turret))
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
            TurretGroupUtility.SetTurretHoldFire(turret, group.holdFire);
            TurretGroupUtility.SetTurretPowerOff(turret, group.powerOff);
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
                newName = DefaultGroupName(group.id);
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
                TurretGroupUtility.SetTurretPowerOff(thing, group.powerOff);
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
                TurretGroupUtility.SetTurretHoldFire(thing, group.holdFire);
            }
        }

        public void SelectGroup(TurretGroupData group, Map contextMap = null)
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
                if (thing != null && thing.Spawned && (contextMap == null || thing.Map == contextMap))
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
            if (group == null || groupByTurret == null || group.members == null)
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
    }
}
