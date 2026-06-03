using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace TurretGroupControl
{
    [HarmonyPatch]
    public static class CeleTechTurretDrawOverlayCompatibilityPatch
    {
        private const string CeleTechNamespacePrefix = "TOT_DLL_test.";
        private const string DrawExtraSelectionOverlaysMethodName = nameof(Thing.DrawExtraSelectionOverlays);

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in AccessTools.AllTypes())
            {
                if (!IsCeleTechTurretType(type))
                {
                    continue;
                }

                var method = AccessTools.DeclaredMethod(type, DrawExtraSelectionOverlaysMethodName);
                if (method != null && method.GetParameters().Length == 0 && method.ReturnType == typeof(void))
                {
                    yield return method;
                }
            }
        }

        public static Exception Finalizer(Exception __exception, Thing __instance)
        {
            if (__exception == null)
            {
                return null;
            }

            if (__exception is NullReferenceException && IsCeleTechTurretType(__instance?.GetType()))
            {
                Log.WarningOnce($"[Turret Group Control] 已拦截 CeleTech 炮塔选中覆盖层错误，炮塔组功能将继续可用。炮塔：{__instance?.LabelCap ?? "unknown"}。原始错误：{__exception.Message}", 19460103);
                return null;
            }

            return __exception;
        }

        private static bool IsCeleTechTurretType(Type type)
        {
            if (type == null || type.FullName == null || !type.FullName.StartsWith(CeleTechNamespacePrefix))
            {
                return false;
            }

            return type.Name.Contains("Turret") || type.Name.Contains("Bunker");
        }
    }
}
