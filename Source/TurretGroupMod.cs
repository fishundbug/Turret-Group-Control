using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    public class TurretGroupMod : Mod
    {
        public static TurretGroupSettings Settings { get; private set; }

        public TurretGroupMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TurretGroupSettings>();
            var harmony = new Harmony("fishundbug.TurretGroupControl");
            harmony.PatchAll();
        }

        public override string SettingsCategory()
        {
            return "Turret Group Control";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "Show turret group labels",
                ref Settings.showGroupLabels,
                "Show a small text label above grouped turrets."
            );
            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
