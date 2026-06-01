using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    public static class TurretGroupUtility
    {
        public static TurretGroupManager GetManager(Map map)
        {
            return map?.GetComponent<TurretGroupManager>();
        }

        public static IEnumerable<Thing> GetSelectedSupportedTurrets()
        {
            foreach (var thing in Find.Selector.SelectedObjects)
            {
                if (thing is Thing turret && TurretGroupManager.IsSupportedTurret(turret))
                {
                    yield return turret;
                }
            }
        }

        public static void SelectGroupFor(Thing turret)
        {
            var map = turret?.Map;
            var manager = GetManager(map);
            var group = manager?.FindGroupFor(turret);
            if (group != null)
            {
                manager.SelectGroup(group);
            }
        }

        public static void ToggleHoldFireFor(Thing turret, bool holdFire)
        {
            var map = turret?.Map;
            var manager = GetManager(map);
            var group = manager?.FindGroupFor(turret);
            if (group != null)
            {
                manager.ToggleHoldFire(group.id, holdFire);
            }
        }
    }
}
