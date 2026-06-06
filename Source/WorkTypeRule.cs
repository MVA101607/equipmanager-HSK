using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    internal class WorkTypeRule : IExposable
    {
        private static HashSet<ThingDef> _allRelevantThings;

        private static readonly Dictionary<string, IEnumerable<string>> DefaultWorkTypeStats =
            new()
            {
                {
                    "Cooking",
                    new[]
                    {
                        "FoodPoisonChance", "DrugCookingSpeed", "ButcheryFleshSpeed", "ButcheryFleshEfficiency",
                        "CookSpeed"
                    }
                },
                {"Hunting", new[] {"HuntingStealth"}},
                {"Doctor", new[] {"MedicalTendQualityOffset", "MedicalPotency"}}
            };

        private readonly HashSet<StatDef> _requiredStats = new();
        private List<StatWeight> _defaultStatWeights;
        private bool _isInitialized;
        private List<StatWeight> _statWeights = new();
        private WorkTypeDef _workTypeDef;
        private string _workTypeDefName;

        [UsedImplicitly]
        public WorkTypeRule() { }

        public WorkTypeRule(string workTypeDefName)
        {
            _workTypeDefName = workTypeDefName;
        }

        public WorkTypeRule(string workTypeDefName, List<StatWeight> statWeights)
        {
            _workTypeDefName = workTypeDefName;
            _statWeights = statWeights;
        }

        private static IEnumerable<ThingDef> AllRelevantThings
        {
            get
            {
                if (_allRelevantThings == null || _allRelevantThings.Count == 0)
                {
                    // Используем тот же пул что и ToolRule — включает HSK-инструменты
                    // у которых нет рабочих статов в statBases/equippedStatOffsets,
                    // но есть def.tools (ближнее оружие / инструменты).
                    _allRelevantThings = new HashSet<ThingDef>(ToolRule.AllRelevantThings);
                }
                return _allRelevantThings;
            }
        }

        public static IEnumerable<WorkTypeRule> DefaultRules
        {
            get
            {
                var rules = new List<WorkTypeRule>();
                foreach (var workTypeDef in WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder)
                {
                    var rule = new WorkTypeRule(workTypeDef.defName);
                    foreach (var statWeight in rule.DefaultStatWeights.Where(sw => sw.StatDef != null))
                    {
                        rule.SetStatWeight(statWeight.StatDef, statWeight.Weight);
                    }
                    rules.Add(rule);
                }
                return rules;
            }
        }

        private IEnumerable<StatWeight> DefaultStatWeights
        {
            get
            {
                Initialize();
                return _defaultStatWeights;
            }
        }

        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        public string Label =>
            WorkTypeDef != null
                ? WorkTypeDef.labelShort.NullOrEmpty() ? WorkTypeDefName : WorkTypeDef.labelShort.CapitalizeFirst()
                : WorkTypeDefName;

        public IEnumerable<StatDef> RequiredStats
        {
            get
            {
                Initialize();
                return _requiredStats.Intersect(_statWeights.Select(sw => sw.StatDef));
            }
        }

        private WorkTypeDef WorkTypeDef
        {
            get
            {
                Initialize();
                return _workTypeDef;
            }
        }

        public string WorkTypeDefName => _workTypeDefName;

        /// <summary>
        /// Статы, которые реально участвуют в UI данного типа работы
        /// (те самые, для которых показаны бегунки).
        /// </summary>
        private IEnumerable<StatDef> ActiveStats =>
            _statWeights.Where(sw => sw.StatDef != null).Select(sw => sw.StatDef);

        /// <summary>
        /// Для ThingDef используем public API через отклонение от defaultBaseValue,
        /// потому что прямой StatHelper.GetStatValue(ThingDef, ...) в репо private.
        /// У предмета должен быть хотя бы один важный бонус строго больше 1.
        /// </summary>
        private bool HasRelevantBonusAboveOne(ThingDef def)
        {
            if (def == null) { return false; }
            Initialize();
            var stats = ActiveStats.ToList();
            if (!stats.Any()) { return false; }
            return stats.Any(stat => (StatHelper.GetStatValueDeviation(def, stat) + stat.defaultBaseValue) > 1f);
        }

        private bool HasRelevantBonusAboveOne(Thing thing)
        {
            if (thing == null) { return false; }
            Initialize();
            var stats = ActiveStats.ToList();
            if (!stats.Any()) { return false; }
            return stats.Any(stat => StatHelper.GetStatValue(thing, stat) > 1f);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _workTypeDefName, nameof(WorkTypeDefName));
            Scribe_Collections.Look(ref _statWeights, "StatWeights", LookMode.Deep);
        }

        public void DeleteStatWeight(string statDefName)
        {
            _ = _statWeights.RemoveAll(weight => weight.StatDefName == statDefName);
        }

        public IEnumerable<Thing> GetCurrentlyAvailableItemsSorted(Map map)
        {
            var items = new List<Thing>();
            var globalItemDefs = new HashSet<ThingDef>(GetGloballyAvailableItems());
            foreach (var thing in map?.listerThings?.ThingsInGroup(ThingRequestGroup.Weapon).Where(thing =>
                         globalItemDefs.Contains(thing.def)) ?? Array.Empty<Thing>())
            {
                var comp = thing.TryGetComp<CompForbiddable>();
                if (comp != null && comp.Forbidden) { continue; }
                items.Add(thing);
            }
            items.SortByDescending(GetThingScore);
            return items;
        }

        public IEnumerable<ThingDef> GetGloballyAvailableItems()
        {
            var items = new List<ThingDef>();
            var activeStatLabels = ActiveStats
                .Select(s => s.LabelCap.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var def in AllRelevantThings)
            {
                // Стандартная проверка (statBases / equippedStatOffsets)
                var hasStandardStat = ActiveStats.Any(stat =>
                    Math.Abs(StatHelper.GetStatValueDeviation(def, stat)) > 0.001f);

                // HSK-проверка через SpecialDisplayStats
                var hasSpecialStat = !hasStandardStat &&
                    activeStatLabels.Any(label => GetSpecialStatsForDef(def).ContainsKey(label));

                if (hasStandardStat || hasSpecialStat) { items.Add(def); }
            }
            items.SortByDescending(GetThingDefScore);
            return items;
        }

        public IReadOnlyList<StatWeight> GetStatWeights()
        {
            Initialize();
            return _statWeights;
        }

        // Кэш: ThingDef → (label в нижнем регистре → значение)
        // Строится лениво при первом вызове GetSpecialStatsForDef.
        private static readonly Dictionary<ThingDef, Dictionary<string, float>> _specialStatsCache =
            new();

        private static Dictionary<string, float> GetSpecialStatsForDef(ThingDef def)
        {
            if (_specialStatsCache.TryGetValue(def, out var cached)) { return cached; }
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var thing = def.MadeFromStuff
                    ? ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def))
                    : ThingMaker.MakeThing(def);
                foreach (var entry in thing.SpecialDisplayStats() ?? Enumerable.Empty<StatDrawEntry>())
                {
                    if (entry == null || entry.LabelCap.NullOrEmpty()) { continue; }
                    // Пробуем распарсить числовое значение из ValueString (например "120%" → 1.2, "1.5x" → 1.5)
                    var raw = entry.ValueString?.Replace("%", "").Replace("x", "").Trim();
                    if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var val))
                    {
                        result[entry.LabelCap] = val;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"EquipmentManager: SpecialDisplayStats failed for {def.defName}: {ex.Message}");
            }
            _specialStatsCache[def] = result;
            return result;
        }

        public float GetThingDefScore([NotNull] ThingDef def)
        {
            return def == null
                ? throw new ArgumentNullException(nameof(def))
                : _statWeights.Where(statWeight => statWeight.StatDef != null).Sum(statWeight =>
                    EquipmentManager.NormalizeStatValue(statWeight.StatDef,
                        StatHelper.GetStatValueDeviation(def, statWeight.StatDef)) * statWeight.Weight);
        }

        public float GetThingScore([NotNull] Thing thing)
        {
            if (thing == null) { throw new ArgumentNullException(nameof(thing)); }

            var specialStats = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var entry in thing.SpecialDisplayStats() ?? Enumerable.Empty<StatDrawEntry>())
                {
                    if (entry == null || entry.LabelCap.NullOrEmpty()) { continue; }
                    var raw = entry.ValueString?.Replace("%", "").Replace("x", "").Trim();
                    if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var val))
                    {
                        specialStats[entry.LabelCap] = val;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"EquipmentManager: SpecialDisplayStats failed for {thing.LabelCapNoCount}: {ex.Message}");
            }

            return _statWeights.Where(sw => sw.StatDef != null).Sum(sw =>
            {
                if (!specialStats.TryGetValue(sw.StatDef.LabelCap, out var specialVal)) { return 0f; }
                var deviation = specialVal - sw.StatDef.defaultBaseValue;
                return EquipmentManager.NormalizeStatValue(sw.StatDef, deviation) * sw.Weight;
            });
        }

        private void Initialize()
        {
            if (_isInitialized) { return; }
            _isInitialized = true;
            _workTypeDef = DefDatabase<WorkTypeDef>.GetNamedSilentFail(_workTypeDefName);
            if (WorkTypeDef == null) { return; }
            _defaultStatWeights = new List<StatWeight>();
            if (DefaultWorkTypeStats.ContainsKey(WorkTypeDefName))
            {
                foreach (var statDefName in DefaultWorkTypeStats[WorkTypeDefName])
                {
                    if (!_defaultStatWeights.Any(sw => sw.StatDefName == statDefName))
                    {
                        _defaultStatWeights.Add(new StatWeight(statDefName, false) {Weight = 2f});
                    }
                }
            }
            foreach (var statDef in WorkTypeDef.relevantSkills
                         .Where(skill => SkillStatMap.Map.ContainsKey(skill))
                         .Select(skill => SkillStatMap.Map[skill])
                         .SelectMany(stats => stats))
            {
                _ = _requiredStats.Add(statDef);
                if (!_defaultStatWeights.Any(sw => sw.StatDefName == statDef.defName))
                {
                    _defaultStatWeights.Add(new StatWeight(statDef.defName, false) {Weight = 1f});
                }
            }
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefs.Where(recipeDef =>
                         recipeDef.requiredGiverWorkType == WorkTypeDef))
            {
                if (recipe.efficiencyStat != null &&
                    !_defaultStatWeights.Any(sw => sw.StatDefName == recipe.efficiencyStat.defName))
                {
                    _defaultStatWeights.Add(new StatWeight(recipe.efficiencyStat.defName, false) {Weight = 0.8f});
                }
                if (recipe.workSpeedStat != null)
                {
                    _ = _requiredStats.Add(recipe.workSpeedStat);
                    if (!_defaultStatWeights.Any(sw => sw.StatDefName == recipe.workSpeedStat.defName))
                    {
                        _defaultStatWeights.Add(new StatWeight(recipe.workSpeedStat.defName, false) {Weight = 0.5f});
                    }
                }
                if (recipe.workTableEfficiencyStat != null &&
                    !_defaultStatWeights.Any(sw => sw.StatDefName == recipe.workTableEfficiencyStat.defName))
                {
                    _defaultStatWeights.Add(
                        new StatWeight(recipe.workTableEfficiencyStat.defName, false) {Weight = 0.8f});
                }
                if (recipe.workTableSpeedStat != null &&
                    !_defaultStatWeights.Any(sw => sw.StatDefName == recipe.workTableSpeedStat.defName))
                {
                    _defaultStatWeights.Add(
                        new StatWeight(recipe.workTableSpeedStat.defName, false) {Weight = 0.5f});
                }
            }
            _ = _defaultStatWeights.RemoveAll(sw => !StatHelper.WorkTypeStatDefs.Contains(sw.StatDef));
            _ = _requiredStats.RemoveWhere(statDef => !StatHelper.WorkTypeStatDefs.Contains(statDef));
        }

        public void SetStatWeight([NotNull] StatDef statDef, float weight)
        {
            if (statDef == null) { throw new ArgumentNullException(nameof(statDef)); }
            var statWeight = _statWeights.FirstOrDefault(sw => sw.StatDef == statDef);
            if (statWeight == null)
            {
                statWeight = new StatWeight(statDef.defName, false);
                _statWeights.Add(statWeight);
            }
            statWeight.Weight = weight;
        }

        public static void InvalidateCache()
        {
            _allRelevantThings = null;
            _specialStatsCache.Clear();
        }
    }
}
