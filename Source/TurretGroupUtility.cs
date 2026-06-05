using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace TurretGroupControl
{
    public static class TurretGroupUtility
    {
        private static FieldInfo buildingTurretGunHoldFireField;
        private static bool buildingTurretGunHoldFireFieldResolved;

        public static TurretGroupGameComponent GetManager(Map map = null)
        {
            return Current.Game?.GetComponent<TurretGroupGameComponent>();
        }

        public static TurretGroupGameComponent GetManager()
        {
            return Current.Game?.GetComponent<TurretGroupGameComponent>();
        }

        public static IEnumerable<Thing> GetSelectedSupportedTurrets()
        {
            foreach (var thing in Find.Selector.SelectedObjects)
            {
                if (thing is Thing turret && IsSupportedTurret(turret))
                {
                    yield return turret;
                }
            }
        }

        public static void SelectGroupFor(Thing turret)
        {
            var manager = GetManager();
            var group = manager?.FindGroupFor(turret);
            if (group != null)
            {
                manager.SelectGroup(group, turret?.Map);
            }
        }

        public static void ToggleHoldFireFor(Thing turret, bool holdFire)
        {
            var manager = GetManager();
            var group = manager?.FindGroupFor(turret);
            if (group != null)
            {
                manager.ToggleHoldFire(group.id, holdFire);
            }
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
