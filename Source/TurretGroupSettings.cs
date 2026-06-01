using Verse;

namespace TurretGroupControl
{
    public class TurretGroupSettings : ModSettings
    {
        public bool showGroupLabels = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showGroupLabels, "showGroupLabels", true);
        }
    }
}
