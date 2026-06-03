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
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                groups ??= new Dictionary<int, TurretGroupData>();
                CleanupAllGroups();
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
                holdFire = false
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
            if (thing == null)
            {
                return null;
            }

            foreach (var group in groups.Values)
            {
                if (group.Contains(thing))
                {
                    return group;
                }
            }

            return null;
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

            RemoveMember(turret);
            group.CleanupMembers();
            group.members.Add(turret);
            ApplyHoldFireToTurret(turret, group.holdFire);
        }

        public void RemoveMember(Thing turret)
        {
            if (turret == null)
            {
                return;
            }

            foreach (var group in groups.Values)
            {
                group.members?.RemoveAll(t => t == turret);
            }
        }

        public void RemoveMember(int groupId, Thing turret)
        {
            if (turret == null || !groups.TryGetValue(groupId, out var group))
            {
                return;
            }

            group.members?.RemoveAll(t => t == turret);
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

        public void ApplyHoldFire(TurretGroupData group)
        {
            if (group == null)
            {
                return;
            }

            group.CleanupMembers();
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
                groups.Remove(emptyGroups[i]);
            }
        }

        public void CleanupInvalidMembers()
        {
            if (groups == null || groups.Count == 0)
            {
                return;
            }

            foreach (var group in groups.Values)
            {
                group?.CleanupMembers();
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
