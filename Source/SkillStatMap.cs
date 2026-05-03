using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    internal static class SkillStatMap
    {
        private static Dictionary<SkillDef, List<StatDef>> _map;

        public static Dictionary<SkillDef, List<StatDef>> Map
        {
            get
            {
                if (_map == null) { BuildMap(); }
                return _map;
            }
        }

        // Возвращает стат и все её statFactors (аналог SS Extensions.StatAndItsFactors).
        private static IEnumerable<StatDef> StatAndItsFactors(StatDef stat)
        {
            yield return stat;
            if (stat.statFactors == null) { yield break; }
            foreach (var factor in stat.statFactors)
            {
                yield return factor;
            }
        }

        private static void BuildMap()
        {
            _map = new Dictionary<SkillDef, List<StatDef>>();
            foreach (var skill in DefDatabase<SkillDef>.AllDefsListForReading)
            {
                _map[skill] = new List<StatDef>();
            }
            foreach (var stat in DefDatabase<StatDef>.AllDefsListForReading)
            {
                if (stat.skillNeedFactors != null)
                {
                    foreach (var need in stat.skillNeedFactors)
                    {
                        var list = _map[need.skill];
                        foreach (var s in StatAndItsFactors(stat).Where(s => !list.Contains(s)))
                        {
                            list.Add(s);
                        }
                    }
                }
                if (stat.skillNeedOffsets != null)
                {
                    foreach (var need in stat.skillNeedOffsets)
                    {
                        var list = _map[need.skill];
                        foreach (var s in StatAndItsFactors(stat).Where(s => !list.Contains(s)))
                        {
                            list.Add(s);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Сбросить кэш при загрузке новой игры / смене модов.
        /// </summary>
        public static void Invalidate()
        {
            _map = null;
        }
    }
}
