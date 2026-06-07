using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    internal class Role : IExposable
    {
        // SystemIdOff оставлен только для миграции старых сейвов.
        // Роль OFF больше не используется — функцию выполняет AssignMode.NoAction.
        public const int SystemIdOff = -1;

        public static bool IsSystemId(int id)
        {
            return id == SystemIdOff;
        }

        public enum PrimaryWeaponType
        {
            None,
            RangedWeapon,
            MeleeWeapon
        }

        /// <summary>
        /// Системные роли. OFF удалён — его функцию выполняет AssignMode.NoAction.
        /// Массив оставлен пустым для обратной совместимости кода.
        /// </summary>
        public static IEnumerable<Role> SystemRoles => Array.Empty<Role>();

        public static IEnumerable<Role> DefaultRoles =>
            SystemRoles.Concat(DefaultProfile.Roles);

        private int _id;
        private bool _initialized;
        private List<int> _meleeSidearmRules = new();
        private List<PassionLimit> _passionLimits = new();
        private List<PawnCapacityLimit> _pawnCapacityLimits = new();
        private List<PawnCapacityWeight> _pawnCapacityWeights = new();
        private Dictionary<string, bool> _pawnTraits = new();
        private Dictionary<string, bool> _pawnWorkCapacities = new();
        private PrimaryWeaponType _primaryRuleType = PrimaryWeaponType.None;
        private PrimaryWeaponType _secondaryRuleType = PrimaryWeaponType.None;
        private List<int> _rangedSidearmRules = new();
        private List<SkillLimit> _skillLimits = new();
        private List<SkillWeight> _skillWeights = new();
        private List<StatLimit> _statLimits = new();
        private List<StatWeight> _statWeights = new();
        public bool DropUnassignedWeapons = true;
        public bool IsDisabled = false;
        public string Label;
        public int? PrimaryMeleeWeaponRuleId;
        public int? PrimaryRangedWeaponRuleId;
        public int? SecondaryMeleeWeaponRuleId;
        public int? SecondaryRangedWeaponRuleId;
        public float Priority;
        public int? ToolRuleId;

        [UsedImplicitly]
        public Role() { }

        public Role(int id)
        {
            _id = id;
        }

        public Role(int id, string label, int priority, PrimaryWeaponType primaryRuleType,
            int? primaryRangedWeaponRuleId, int? primaryMeleeWeaponRuleId, List<int> rangedSidearmRules,
            List<int> meleeSidearmRules, int? toolRuleId, Dictionary<string, bool> pawnTraits,
            Dictionary<string, bool> pawnWorkCapacities, bool dropUnassignedWeapons, List<PassionLimit> passionLimits,
            List<PawnCapacityLimit> pawnCapacityLimits, List<PawnCapacityWeight> pawnCapacityWeights,
            List<SkillLimit> skillLimits, List<SkillWeight> skillWeights, List<StatLimit> statLimits,
            List<StatWeight> statWeights,
            PrimaryWeaponType secondaryRuleType = PrimaryWeaponType.None,
            int? secondaryRangedWeaponRuleId = null, int? secondaryMeleeWeaponRuleId = null)
        {
            _id = id;
            Label = label;
            Priority = priority;
            _primaryRuleType = primaryRuleType;
            PrimaryRangedWeaponRuleId = primaryRangedWeaponRuleId;
            PrimaryMeleeWeaponRuleId = primaryMeleeWeaponRuleId;
            _rangedSidearmRules = rangedSidearmRules;
            _meleeSidearmRules = meleeSidearmRules;
            _secondaryRuleType = secondaryRuleType;
            SecondaryRangedWeaponRuleId = secondaryRangedWeaponRuleId;
            SecondaryMeleeWeaponRuleId = secondaryMeleeWeaponRuleId;
            ToolRuleId = toolRuleId;
            _pawnTraits = pawnTraits;
            _pawnWorkCapacities = pawnWorkCapacities;
            DropUnassignedWeapons = dropUnassignedWeapons;
            _passionLimits = passionLimits;
            _pawnCapacityLimits = pawnCapacityLimits;
            _pawnCapacityWeights = pawnCapacityWeights;
            _skillLimits = skillLimits;
            _skillWeights = skillWeights;
            _statLimits = statLimits;
            _statWeights = statWeights;
        }

        public int Id => _id;
        /// <summary>Истина для системной роли OFF — её нельзя изменить или удалить.</summary>
        public bool IsSystemRole => IsSystemId(_id);
        public PrimaryWeaponType SecondaryRuleType
        {
            get => _secondaryRuleType;
            set
            {
                _secondaryRuleType = value;
                switch (value)
                {
                    case PrimaryWeaponType.None:
                        SecondaryMeleeWeaponRuleId = null;
                        SecondaryRangedWeaponRuleId = null;
                        break;
                    case PrimaryWeaponType.RangedWeapon:
                        SecondaryMeleeWeaponRuleId = null;
                        break;
                    case PrimaryWeaponType.MeleeWeapon:
                        SecondaryRangedWeaponRuleId = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
        }


        public List<int> MeleeSidearmRules
        {
            get
            {
                Initialize();
                return _meleeSidearmRules;
            }
        }

        public List<PassionLimit> PassionLimits
        {
            get
            {
                Initialize();
                return _passionLimits;
            }
        }

        public List<PawnCapacityLimit> PawnCapacityLimits
        {
            get
            {
                Initialize();
                return _pawnCapacityLimits;
            }
        }

        public List<PawnCapacityWeight> PawnCapacityWeights
        {
            get
            {
                Initialize();
                return _pawnCapacityWeights;
            }
        }

        public Dictionary<string, bool> PawnTraits
        {
            get
            {
                Initialize();
                return _pawnTraits;
            }
        }

        public Dictionary<string, bool> PawnWorkCapacities
        {
            get
            {
                Initialize();
                return _pawnWorkCapacities;
            }
        }

        public PrimaryWeaponType PrimaryRuleType
        {
            get => _primaryRuleType;
            set
            {
                _primaryRuleType = value;
                switch (_primaryRuleType)
                {
                    case PrimaryWeaponType.None:
                        PrimaryMeleeWeaponRuleId = null;
                        PrimaryRangedWeaponRuleId = null;
                        break;
                    case PrimaryWeaponType.RangedWeapon:
                        PrimaryMeleeWeaponRuleId = null;
                        break;
                    case PrimaryWeaponType.MeleeWeapon:
                        PrimaryRangedWeaponRuleId = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public List<int> RangedSidearmRules
        {
            get
            {
                Initialize();
                return _rangedSidearmRules;
            }
        }

        public List<SkillLimit> SkillLimits
        {
            get
            {
                Initialize();
                return _skillLimits;
            }
        }

        public List<SkillWeight> SkillWeights
        {
            get
            {
                Initialize();
                return _skillWeights;
            }
        }

        public List<StatLimit> StatLimits
        {
            get
            {
                Initialize();
                return _statLimits;
            }
        }

        public List<StatWeight> StatWeights
        {
            get
            {
                Initialize();
                return _statWeights;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _id, nameof(Id));
            Scribe_Values.Look(ref Label, nameof(Label));
            Scribe_Values.Look(ref Priority, nameof(Priority));
            Scribe_Values.Look(ref _primaryRuleType, nameof(PrimaryRuleType));
            Scribe_Values.Look(ref PrimaryRangedWeaponRuleId, nameof(PrimaryRangedWeaponRuleId));
            Scribe_Values.Look(ref PrimaryMeleeWeaponRuleId, nameof(PrimaryMeleeWeaponRuleId));
            Scribe_Values.Look(ref _secondaryRuleType, nameof(SecondaryRuleType));
            Scribe_Values.Look(ref SecondaryRangedWeaponRuleId, nameof(SecondaryRangedWeaponRuleId));
            Scribe_Values.Look(ref SecondaryMeleeWeaponRuleId, nameof(SecondaryMeleeWeaponRuleId));
            Scribe_Collections.Look(ref _rangedSidearmRules, nameof(RangedSidearmRules));
            Scribe_Collections.Look(ref _meleeSidearmRules, nameof(MeleeSidearmRules));
            Scribe_Values.Look(ref ToolRuleId, nameof(ToolRuleId));
            Scribe_Collections.Look(ref _pawnTraits, nameof(PawnTraits));
            Scribe_Collections.Look(ref _pawnWorkCapacities, nameof(PawnWorkCapacities));
            Scribe_Values.Look(ref DropUnassignedWeapons, nameof(DropUnassignedWeapons));
            Scribe_Values.Look(ref IsDisabled, nameof(IsDisabled));
            Scribe_Collections.Look(ref _passionLimits, nameof(PassionLimits), LookMode.Deep);
            Scribe_Collections.Look(ref _pawnCapacityLimits, nameof(PawnCapacityLimits), LookMode.Deep);
            Scribe_Collections.Look(ref _pawnCapacityWeights, nameof(PawnCapacityWeights), LookMode.Deep);
            Scribe_Collections.Look(ref _skillLimits, nameof(SkillLimits), LookMode.Deep);
            Scribe_Collections.Look(ref _skillWeights, nameof(SkillWeights), LookMode.Deep);
            Scribe_Collections.Look(ref _statLimits, nameof(StatLimits), LookMode.Deep);
            Scribe_Collections.Look(ref _statWeights, nameof(StatWeights), LookMode.Deep);
        }

        public IReadOnlyList<Pawn> GetAvailablePawnsOrdered()
        {
            Initialize();
            return new List<Pawn>(PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => IsAvailable(p))
                .OrderByDescending(GetScore));
        }

        public float GetScore(Pawn pawn)
        {
            Initialize();
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned;
            var score = 0f;
            foreach (var statWeight in _statWeights.Where(sw => sw.StatDef != null))
            {
                var pawnValues = pawns.Select(p => p.GetStatValue(statWeight.StatDef)).ToList();
                var normalizedValue = StatHelper.NormalizeValue(pawn.GetStatValue(statWeight.StatDef),
                    new FloatRange(pawnValues.Min(), pawnValues.Max()));
                score += normalizedValue * statWeight.Weight;
            }
            foreach (var skillWeight in _skillWeights.Where(sw => sw.SkillDef != null))
            {
                var pawnValues = pawns.Select(p => p.skills.GetSkill(skillWeight.SkillDef).Level).ToList();
                var normalizedValue = StatHelper.NormalizeValue(pawn.skills.GetSkill(skillWeight.SkillDef).Level,
                    new FloatRange(pawnValues.Min(), pawnValues.Max()));
                score += normalizedValue * skillWeight.Weight;
            }
            foreach (var pawnCapacityWeight in _pawnCapacityWeights.Where(pcw => pcw.PawnCapacityDef != null))
            {
                var pawnValues = pawns.Select(p => p.health.capacities.GetLevel(pawnCapacityWeight.PawnCapacityDef))
                    .ToList();
                var normalizedValue = StatHelper.NormalizeValue(
                    pawn.health.capacities.GetLevel(pawnCapacityWeight.PawnCapacityDef),
                    new FloatRange(pawnValues.Min(), pawnValues.Max()));
                score += normalizedValue * pawnCapacityWeight.Weight;
            }
            return score;
        }

        private void Initialize()
        {
            if (_initialized) { return; }
            _initialized = true;
            _meleeSidearmRules ??= new List<int>();
            _rangedSidearmRules ??= new List<int>();
            _pawnTraits ??= new Dictionary<string, bool>();
            _pawnWorkCapacities ??= new Dictionary<string, bool>();
            _passionLimits ??= new List<PassionLimit>();
            _pawnCapacityLimits ??= new List<PawnCapacityLimit>();
            _pawnCapacityWeights ??= new List<PawnCapacityWeight>();
            _skillLimits ??= new List<SkillLimit>();
            _skillWeights ??= new List<SkillWeight>();
            _statLimits ??= new List<StatLimit>();
            _statWeights ??= new List<StatWeight>();
        }

        public bool IsAvailable(Pawn pawn, bool showLog = false)
        {
            Initialize();
            var em = showLog ? Current.Game?.GetComponent<EquipmentManagerGameComponent>() : null;
            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
            {
                var role = pawn.Ideo.GetRole(pawn);
                if (role?.def?.roleEffects != null)
                {
                    if (PrimaryRuleType == PrimaryWeaponType.RangedWeapon && PrimaryRangedWeaponRuleId != null &&
                        role.def.roleEffects.Any(effect => effect is RoleEffect_NoRangedWeapons))
                    {
                        em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - IdeoRole blocks ranged");
                        return false;
                    }
                    if (PrimaryRuleType == PrimaryWeaponType.MeleeWeapon && PrimaryMeleeWeaponRuleId != null &&
                        role.def.roleEffects.Any(effect => effect is RoleEffect_NoMeleeWeapons))
                    {
                        em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - IdeoRole blocks melee");
                        return false;
                    }
                }
            }
            foreach (var pawnTrait in _pawnTraits)
            {
                var trait = DefDatabase<TraitDef>.GetNamedSilentFail(pawnTrait.Key);
                if (trait == null)
                {
                    em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: trait '{pawnTrait.Key}' not found, skipped");
                    continue;
                }
                var hasTrait = pawn.story.traits.HasTrait(trait);
                if (hasTrait != pawnTrait.Value)
                {
                    em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - trait '{pawnTrait.Key}' required={pawnTrait.Value} actual={hasTrait}");
                    return false;
                }
            }
            foreach (var pawnCapacity in _pawnWorkCapacities)
            {
                if (!Enum.TryParse<WorkTags>(pawnCapacity.Key, out var tag))
                {
                    em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: WorkTag '{pawnCapacity.Key}' parse FAILED, skipped");
                    continue;
                }
                var isDisabled = pawn.WorkTagIsDisabled(tag);
                if (isDisabled == pawnCapacity.Value)
                {
                    em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - WorkTag '{pawnCapacity.Key}' required={pawnCapacity.Value} isDisabled={isDisabled}");
                    return false;
                }
            }
            foreach (var passionLimit in _passionLimits.Where(pl => pl.SkillDef != null))
            {
                var passion = pawn.skills.GetSkill(passionLimit.SkillDef).passion;
                switch (passionLimit.Value)
                {
                    case PassionValue.None:
                        if (passion != Passion.None)
                        {
                            em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - passion '{passionLimit.SkillDef.defName}' required=None actual={passion}");
                            return false;
                        }
                        break;
                    case PassionValue.Minor:
                        if (passion != Passion.Minor)
                        {
                            em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - passion '{passionLimit.SkillDef.defName}' required=Minor actual={passion}");
                            return false;
                        }
                        break;
                    case PassionValue.Major:
                        if (passion != Passion.Major)
                        {
                            em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - passion '{passionLimit.SkillDef.defName}' required=Major actual={passion}");
                            return false;
                        }
                        break;
                    case PassionValue.Any:
                        if (passion == Passion.None)
                        {
                            em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - passion '{passionLimit.SkillDef.defName}' required=Any actual=None");
                            return false;
                        }
                        break;
                }
            }
            foreach (var pawnCapacityLimit in _pawnCapacityLimits.Where(pcl => pcl.PawnCapacityDef != null))
            {
                var capacity = pawn.health.capacities.GetLevel(pawnCapacityLimit.PawnCapacityDef);
                if ((pawnCapacityLimit.MinValue != null && capacity < pawnCapacityLimit.MinValue) ||
                    (pawnCapacityLimit.MaxValue != null && capacity > pawnCapacityLimit.MaxValue))
                {
                    em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - capacity '{pawnCapacityLimit.PawnCapacityDef.defName}'={capacity:F2}, Rule: min={pawnCapacityLimit.MinValue} max={pawnCapacityLimit.MaxValue}");
                    return false;
                }
            }
            foreach (var statLimit in _statLimits.Where(sl => sl.StatDef != null))
            {
                var statValue = pawn.GetStatValue(statLimit.StatDef);
                if ((statLimit.MinValue != null && statValue < statLimit.MinValue) ||
                    (statLimit.MaxValue != null && statValue > statLimit.MaxValue))
                {
                    em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - stat '{statLimit.StatDef.defName}'={statValue:F2}, Rule: min={statLimit.MinValue} max={statLimit.MaxValue}");
                    return false;
                }
            }
            foreach (var skillLimit in _skillLimits.Where(sl => sl.SkillDef != null))
            {
                var skillValue = pawn.skills.GetSkill(skillLimit.SkillDef).Level;
                if ((skillLimit.MinValue != null && skillValue < skillLimit.MinValue) ||
                    (skillLimit.MaxValue != null && skillValue > skillLimit.MaxValue))
                {
                    em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: NO - skill '{skillLimit.SkillDef.defName}'={skillValue}, Rule: min={skillLimit.MinValue} max={skillLimit.MaxValue}");
                    return false;
                }
            }
            em?.LogMessage($"[EM] IsAvailable [{Label}] -> {pawn.LabelShortCap}: YES");
            return true;
        }
    }
}