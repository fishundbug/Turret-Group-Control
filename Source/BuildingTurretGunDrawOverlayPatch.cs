using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.DrawExtraSelectionOverlays))]
    public static class BuildingTurretGunDrawOverlayPatch
    {
        public static void Postfix(Building_TurretGun __instance)
        {
            if (__instance == null || !__instance.Spawned || TurretGroupMod.Settings?.showGroupLabels != true)
            {
                return;
            }

            var manager = TurretGroupUtility.GetManager(__instance.Map);
            var group = manager?.FindGroupFor(__instance);
            if (group == null)
            {
                return;
            }

            var drawPos = __instance.DrawPos;
            drawPos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            GenMapUI.DrawThingLabel(drawPos + new Vector3(0f, 0f, 0.55f), group.name, Color.cyan);
        }
    }
}
