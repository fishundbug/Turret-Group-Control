using HarmonyLib;
using RimWorld;

namespace TurretGroupControl
{
    [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.DrawExtraSelectionOverlays))]
    public static class BuildingTurretGunDrawOverlayPatch
    {
        public static void Postfix(Building_TurretGun __instance)
        {
            // Intentionally left blank.
            // The previous label drawing used GUI calls outside OnGUI and caused runtime errors.
            // Keep this patch as a placeholder for future overlay-safe drawing.
        }
    }
}
