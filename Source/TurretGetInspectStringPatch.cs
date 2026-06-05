using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TurretGroupControl
{
    public static class TurretGetInspectStringPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in AccessTools.AllTypes())
            {
                if (type == null || type.IsAbstract || !typeof(Building_Turret).IsAssignableFrom(type))
                {
                    continue;
                }

                var method = AccessTools.DeclaredMethod(type, nameof(Thing.GetInspectString));
                if (method != null && method.GetParameters().Length == 0 && method.ReturnType == typeof(string))
                {
                    yield return method;
                }
            }
        }

        public static void Postfix(Thing __instance, ref string __result)
        {
            if (__instance == null || !__instance.Spawned || __instance.Map == null || __instance.Faction != Faction.OfPlayer || !TurretGroupUtility.IsSupportedTurret(__instance))
            {
                return;
            }

            var manager = TurretGroupUtility.GetManager(__instance.Map);
            var group = manager?.FindGroupFor(__instance);
            if (group == null)
            {
                return;
            }

            string groupLine = "TurretGroupControl_CurrentGroupInspect".Translate(group.name).ToString();
            if (ContainsExactLine(__result, groupLine))
            {
                return;
            }

            __result = __result.NullOrEmpty() ? groupLine : __result + "\n" + groupLine;
        }

        private static bool ContainsExactLine(string text, string line)
        {
            if (text.NullOrEmpty() || line.NullOrEmpty())
            {
                return false;
            }

            int index = text.IndexOf(line, System.StringComparison.Ordinal);
            while (index >= 0)
            {
                bool startsAtLineStart = index == 0 || text[index - 1] == '\n';
                int endIndex = index + line.Length;
                bool endsAtLineEnd = endIndex == text.Length || text[endIndex] == '\n' || text[endIndex] == '\r';
                if (startsAtLineStart && endsAtLineEnd)
                {
                    return true;
                }

                index = text.IndexOf(line, index + 1, System.StringComparison.Ordinal);
            }

            return false;
        }
    }
}
