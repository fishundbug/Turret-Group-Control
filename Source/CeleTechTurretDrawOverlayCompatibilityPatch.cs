using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    /// <summary>
    /// 根因修复：CeleTech Arsenal 的 <c>Building_CMCTurretGun.DrawExtraSelectionOverlays()</c>
    /// 直接访问 <c>AttackVerb.verbProps</c> 却没有做空检查（反编译源 Building_CMCTurretGun.cs:481）。
    /// 对导弹、防空、主炮等 <c>AttackVerb</c> 为 null 的炮塔，选中时每帧抛出
    /// NullReferenceException，并在 UIRootUpdate 中循环刷红字。
    ///
    /// CeleTech 作者在自身其它方法（如 Building_CMCTurretGun_AAAS.OrderAttack）中都使用了
    /// <c>AttackVerb?.verbProps == null</c> 守卫，唯独在覆盖层绘制处遗漏。本补丁补上同样的守卫：
    /// 当 AttackVerb 无效时，照常绘制基类覆盖层与强制目标连线，仅跳过依赖 verb 的射程圈
    /// （没有 verb 也无从得知射程，原方法在此本就只会崩溃）；当 AttackVerb 有效时完全放行原方法，
    /// 因此 CeleTech 的全部功能均被保留。
    ///
    /// 补丁仅作用于 <c>Building_CMCTurretGun</c> 声明的该方法，其全部炮塔子类（导弹/防空/主炮/
    /// 火箭/地堡/PD）通过继承同样得到修复。CeleTech 未安装时自动禁用，不产生任何依赖。
    /// </summary>
    public static class CeleTechTurretDrawOverlayCompatibilityPatch
    {
        private const string CeleTechTurretTypeName = "TOT_DLL_test.Building_CMCTurretGun";

        private static Action<Thing> baseDrawExtraSelectionOverlays;
        private static bool baseDrawExtraSelectionOverlaysResolved;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName(CeleTechTurretTypeName);
            if (type == null)
            {
                yield break;
            }

            var method = AccessTools.DeclaredMethod(type, nameof(Thing.DrawExtraSelectionOverlays));
            if (method != null && method.GetParameters().Length == 0 && method.ReturnType == typeof(void))
            {
                yield return method;
            }
        }

        public static bool Prefix(Building __instance)
        {
            if (!(__instance is Building_Turret turret))
            {
                return true; // 非预期类型：放行原方法，不做任何干预。
            }

            Verb attackVerb = turret.AttackVerb;
            if (attackVerb != null && attackVerb.verbProps != null)
            {
                // AttackVerb 有效（绝大多数情况）：完全放行 CeleTech 原方法，保留全部覆盖层与功能。
                return true;
            }

            // AttackVerb 为空：原方法会在 AttackVerb.verbProps 处空引用。补上作者遗漏的守卫，
            // 绘制不依赖 verb 的安全覆盖层，跳过射程圈，避免每帧崩溃。
            DrawBaseSelectionOverlaysSafe(__instance);
            DrawForcedTargetLineSafe(turret);
            return false;
        }

        private static void DrawBaseSelectionOverlaysSafe(Building instance)
        {
            var drawBase = ResolveBaseDrawExtraSelectionOverlays();
            drawBase?.Invoke(instance);
        }

        private static Action<Thing> ResolveBaseDrawExtraSelectionOverlays()
        {
            if (baseDrawExtraSelectionOverlaysResolved)
            {
                return baseDrawExtraSelectionOverlays;
            }

            baseDrawExtraSelectionOverlaysResolved = true;
            try
            {
                var method = AccessTools.DeclaredMethod(typeof(Building), nameof(Thing.DrawExtraSelectionOverlays))
                    ?? AccessTools.Method(typeof(Thing), nameof(Thing.DrawExtraSelectionOverlays));
                if (method == null)
                {
                    return null;
                }

                if (method.DeclaringType == typeof(Building))
                {
#pragma warning disable CS0618
                    var buildingDelegate = AccessTools.MethodDelegate<Action<Building>>(method, null, false);
#pragma warning restore CS0618
                    baseDrawExtraSelectionOverlays = thing =>
                    {
                        if (thing is Building building)
                        {
                            buildingDelegate(building);
                        }
                    };
                }
                else
                {
#pragma warning disable CS0618
                    baseDrawExtraSelectionOverlays = AccessTools.MethodDelegate<Action<Thing>>(method, null, false);
#pragma warning restore CS0618
                }
            }
            catch (Exception ex)
            {
                Log.WarningOnce($"[Turret Group Control] 无法创建 CeleTech 覆盖层基类调用委托，将仅跳过 CeleTech 缺失 AttackVerb 的射程圈绘制。原因：{ex.GetType().Name}: {ex.Message}", 19460104);
            }

            return baseDrawExtraSelectionOverlays;
        }

        private static void DrawForcedTargetLineSafe(Building_Turret turret)
        {
            LocalTargetInfo forced = turret.forcedTarget;
            if (!forced.IsValid || (forced.HasThing && (forced.Thing == null || !forced.Thing.Spawned)))
            {
                return;
            }

            Vector3 target = forced.HasThing ? forced.Thing.TrueCenter() : forced.Cell.ToVector3Shifted();
            Vector3 source = turret.TrueCenter();
            target.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            source.y = target.y;
            GenDraw.DrawLineBetween(source, target, Building_TurretGun.ForcedTargetLineMat, 0.2f);
        }
    }
}
