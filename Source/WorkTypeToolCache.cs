using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    /// <summary>
    /// Кэш отсортированных инструментов для каждого WorkTypeDef.
    /// Единый источник правды для UI и для назначения пешкам.
    /// Инвалидируется при изменении ползунков в диалоге или при
    /// появлении/исчезновении предмета на карте.
    /// </summary>
    internal static class WorkTypeToolCache
    {
        // workTypeDefName → отсортированный список Thing на карте
        private static readonly Dictionary<(string, Map), List<Thing>> _sortedOnMap =
            new();

        // workTypeDefName → отфильтрованный список ThingDef (глобально)
        private static readonly Dictionary<string, List<ThingDef>> _filteredGlobal =
            new();

        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        /// <summary>Вызывать при изменении ползунков в диалоге — сбрасывает всё.</summary>
        public static void InvalidateAll()
        {
            _sortedOnMap.Clear();
            _filteredGlobal.Clear();
        }

        /// <summary>Вызывать при появлении/исчезновении предмета на карте — сбрасывает только on-map.</summary>
        public static void InvalidateOnMap()
        {
            _sortedOnMap.Clear();
        }

        /// <summary>
        /// Отфильтрованный список ThingDef (Globally Available) для данного правила.
        /// Содержит только инструменты, у которых есть хотя бы один стат из ползунков.
        /// </summary>
        public static List<ThingDef> GetGloballyAvailable(WorkTypeRule rule)
        {
            if (rule == null) { return new List<ThingDef>(); }
            if (_filteredGlobal.TryGetValue(rule.WorkTypeDefName, out var cached)) { return cached; }

            var activeStatLabels = rule.GetStatWeights()
                .Where(sw => sw.StatDef != null)
                .Select(sw => sw.StatDef.LabelCap.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = new List<ThingDef>();
            if (activeStatLabels.Any())
            {
                foreach (var def in WorkTypeRule.AllRelevantThingsPublic)
                {
                    if (activeStatLabels.Any(label =>
                            WorkTypeRule.GetSpecialStatsForDef(def).ContainsKey(label)))
                    {
                        result.Add(def);
                    }
                }
            }

            _filteredGlobal[rule.WorkTypeDefName] = result;
            return result;
        }

        /// <summary>
        /// Отсортированный по score список Thing на карте для данного правила.
        /// Единый источник и для UI (Currently Available), и для AssignToolsForWorkTypes.
        /// </summary>
        public static List<Thing> GetSortedOnMap(WorkTypeRule rule, Map map)
        {
            if (rule == null || map == null) { return new List<Thing>(); }
            var key = (rule.WorkTypeDefName, map);
            if (_sortedOnMap.TryGetValue(key, out var cached)) { return cached; }

            var globalDefs = new HashSet<ThingDef>(GetGloballyAvailable(rule));
            var result = new List<Thing>();

            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                if (!globalDefs.Contains(thing.def)) { continue; }
                var comp = thing.TryGetComp<CompForbiddable>();
                if (comp != null && comp.Forbidden) { continue; }
                result.Add(thing);
            }
            result.SortByDescending(t => rule.GetThingScore(t));

            _sortedOnMap[key] = result;
            return result;
        }
    }
}
