using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    public class TurretGroupMod : Mod
    {
        public static TurretGroupSettings Settings { get; private set; }

        private static readonly Harmony HarmonyInstance = new Harmony("fishundbug.TurretGroupControl");
        private static bool delayedCompatibilityPatchesApplied;

        public TurretGroupMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TurretGroupSettings>();
            HarmonyInstance.PatchAll();

            // RimWorld 在后台线程创建 Mod 类。对所有第三方炮塔类型批量打补丁时，
            // Harmony/MonoMod 可能触发第三方静态构造函数，而部分模组会在静态构造中加载材质。
            // 因此将广谱炮塔兼容补丁延迟到 LongEvent 完成后的主线程执行，避免跨线程资源加载红字。
            LongEventHandler.ExecuteWhenFinished(ApplyDelayedCompatibilityPatches);
        }

        private static void ApplyDelayedCompatibilityPatches()
        {
            if (delayedCompatibilityPatchesApplied)
            {
                return;
            }

            delayedCompatibilityPatchesApplied = true;
            PatchPostfixes(BuildingTurretGunGetGizmosPatch.TargetMethods(), typeof(BuildingTurretGunGetGizmosPatch), nameof(BuildingTurretGunGetGizmosPatch.Postfix), "炮塔组 Gizmo");
            PatchPostfixes(TurretGetInspectStringPatch.TargetMethods(), typeof(TurretGetInspectStringPatch), nameof(TurretGetInspectStringPatch.Postfix), "炮塔信息栏");
            PatchPrefixes(CeleTechTurretDrawOverlayCompatibilityPatch.TargetMethods(), typeof(CeleTechTurretDrawOverlayCompatibilityPatch), nameof(CeleTechTurretDrawOverlayCompatibilityPatch.Prefix), "CeleTech 选中覆盖层");
        }

        private static void PatchPostfixes(IEnumerable<MethodBase> targetMethods, Type patchType, string patchMethodName, string label)
        {
            PatchMethods(targetMethods, null, new HarmonyMethod(AccessTools.Method(patchType, patchMethodName)), label);
        }

        private static void PatchPrefixes(IEnumerable<MethodBase> targetMethods, Type patchType, string patchMethodName, string label)
        {
            PatchMethods(targetMethods, new HarmonyMethod(AccessTools.Method(patchType, patchMethodName)), null, label);
        }

        private static void PatchMethods(IEnumerable<MethodBase> targetMethods, HarmonyMethod prefix, HarmonyMethod postfix, string label)
        {
            int count = 0;
            foreach (var method in targetMethods)
            {
                if (method == null)
                {
                    continue;
                }

                try
                {
                    HarmonyInstance.Patch(method, prefix: prefix, postfix: postfix);
                    count++;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[Turret Group Control] 跳过一个{label}兼容补丁目标：{method.FullDescription()}。原因：{ex.GetType().Name}: {ex.Message}");
                }
            }

            if (count > 0)
            {
                Log.Message($"[Turret Group Control] 已应用 {count} 个{label}兼容补丁。");
            }
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
