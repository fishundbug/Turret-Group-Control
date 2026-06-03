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
            return "TurretGroupControl_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "TurretGroupControl_ShowGroupLabels".Translate(),
                ref Settings.showGroupLabels,
                "TurretGroupControl_ShowGroupLabelsDesc".Translate()
            );
            listing.Gap();
            listing.CheckboxLabeled(
                "TurretGroupControl_AutoRemoveEmptyGroups".Translate(),
                ref Settings.autoRemoveEmptyGroups,
                "TurretGroupControl_AutoRemoveEmptyGroupsDesc".Translate()
            );
            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
