using Verse;

namespace TurretGroupControl
{
    public class TurretGroupSettings : ModSettings
    {
        public bool showGroupLabels = true;
        public bool autoRemoveEmptyGroups = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showGroupLabels, "showGroupLabels", true);
            Scribe_Values.Look(ref autoRemoveEmptyGroups, "autoRemoveEmptyGroups", false);
        }
    }
}
