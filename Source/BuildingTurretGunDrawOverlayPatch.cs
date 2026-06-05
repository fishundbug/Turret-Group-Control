using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    public static class TurretGroupSelectionOverlayPatch
    {
        private static readonly HashSet<int> DrawnGroupIdsThisFrame = new HashSet<int>();
        private static int lastDrawFrame = -1;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in AccessTools.AllTypes())
            {
                if (type == null || type.IsAbstract || !typeof(Building_Turret).IsAssignableFrom(type))
                {
                    continue;
                }

                var method = AccessTools.DeclaredMethod(type, nameof(Thing.DrawExtraSelectionOverlays));
                if (method != null && method.GetParameters().Length == 0 && method.ReturnType == typeof(void))
                {
                    yield return method;
                }
            }
        }

        public static void Postfix(Thing __instance)
        {
            if (__instance == null || !__instance.Spawned || __instance.Map == null || !TurretGroupUtility.IsSupportedTurret(__instance))
            {
                return;
            }

            var manager = TurretGroupUtility.GetManager(__instance.Map);
            var group = manager?.FindGroupFor(__instance);
            if (group == null)
            {
                return;
            }

            if (Time.frameCount != lastDrawFrame)
            {
                lastDrawFrame = Time.frameCount;
                DrawnGroupIdsThisFrame.Clear();
            }

            if (!DrawnGroupIdsThisFrame.Add(group.id))
            {
                return;
            }

            foreach (var member in group.members.Where(member => member != null && !member.DestroyedOrNull() && member.Spawned && member.Map == __instance.Map))
            {
                // 保留当前被选中炮塔的原版白色选框；只给同组其它炮塔画原版链接淡黄色选框。
                if (Find.Selector.IsSelected(member))
                {
                    continue;
                }

                SelectionDrawer.DrawSelectionBracketFor(member, StorageGroupUtility.GroupedMat);
            }
        }
    }
}
