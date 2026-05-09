using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    internal class RangedWeaponCache : ItemCache
    {
        private AmmoUserPropsDelegate _ammoUserPropsMethod;
        private bool _initialized;
        private bool _isAmmo;

        public RangedWeaponCache([NotNull] Thing thing)
        {
            Thing = thing ?? throw new ArgumentNullException(nameof(thing));
        }

        // ── CE stat defs (ленивая инициализация) ──────────────────────────
        private static StatDef _sdSightsEfficiency;
        private static StatDef _sdShotSpread;
        private static StatDef _sdSwayFactor;
        private static StatDef _sdRecoil;
        private static StatDef _sdMagazineCapacity;
        private static StatDef _sdReloadTime;

        private static StatDef SdSightsEfficiency  => _sdSightsEfficiency  ??= StatDef.Named("SightsEfficiency");
        private static StatDef SdShotSpread        => _sdShotSpread        ??= StatDef.Named("ShotSpread");
        private static StatDef SdSwayFactor        => _sdSwayFactor        ??= StatDef.Named("SwayFactor");
        private static StatDef SdRecoil            => _sdRecoil            ??= StatDef.Named("Recoil");
        private static StatDef SdMagazineCapacity  => _sdMagazineCapacity  ??= StatDef.Named("MagazineCapacity");
        private static StatDef SdReloadTime        => _sdReloadTime        ??= StatDef.Named("ReloadTime");

        // ── кешированные значения ──────────────────────────────────────────
        private float ArmorPenSharp { get; set; }
        private float ArmorPenBlunt { get; set; }
        private int   BurstShotCount { get; set; }
        private float Cooldown { get; set; }
        private float Damage { get; set; }
        private float DpsRealistic { get; set; }
        private float DpsaClose { get; set; }
        private float DpsaLong { get; set; }
        private float DpsaMedium { get; set; }
        private float DpsaShort { get; set; }
        private float MaxRange { get; set; }
        private float MinRange { get; set; }
        private float SightsEfficiency { get; set; }
        private float ShotSpread { get; set; }
        private float SwayFactor { get; set; }
        private float Recoil { get; set; }
        private float MagazineSize { get; set; }
        private float ReloadTime { get; set; }
        private int   TicksBetweenBurstShots { get; set; }
        private float Warmup { get; set; }

        private Thing Thing { get; }

        // ── ammo ──────────────────────────────────────────────────────────
        public IEnumerable<ThingDef> AmmoTypes
        {
            get
            {
                Initialize();
                var ammoTypes = new HashSet<ThingDef>();
                if (_isAmmo)
                {
                    _ = ammoTypes.Add(Thing.def);
                    return ammoTypes;
                }
                if (_ammoUserPropsMethod == null) { return ammoTypes; }
                var ammoUserProps = _ammoUserPropsMethod();
                if (ammoUserProps == null)
                {
                    Log.Error($"Equipment Manager: CompProperties_AmmoUser was not found for {Thing.LabelCapNoCount}");
                    return ammoTypes;
                }
                var ammoSet = CombatExtendedHelper.AmmoSetDelegate(ammoUserProps);
                if (ammoSet == null)
                {
                    Log.Error($"Equipment Manager: Ammo set was not found for {Thing.LabelCapNoCount}");
                    return ammoTypes;
                }
                if (CombatExtendedHelper.AmmoTypesDelegate(ammoSet) is not IEnumerable<object> ammoLinks)
                {
                    Log.Error($"Equipment Manager: Could not get ammo links for {Thing.LabelCapNoCount}");
                    return ammoTypes;
                }
                ammoTypes.AddRange(ammoLinks
                    .Select(link => CombatExtendedHelper.AmmoDelegate(link))
                    .Where(t => t != null));
                return ammoTypes;
            }
        }

        private ThingComp AmmoUserComp =>
            Thing is not ThingWithComps twc ? null :
            twc.AllComps.FirstOrDefault(c => c.GetType() == CombatExtendedHelper.CompAmmoUserType);

        public bool IsAmmo
        {
            get { Initialize(); return _isAmmo; }
        }

        public Def AmmoSet
        {
            get
            {
                Initialize();
                if (_isAmmo || _ammoUserPropsMethod == null) { return null; }
                var props = _ammoUserPropsMethod();
                return props == null ? null : CombatExtendedHelper.AmmoSetDelegate(props);
            }
        }

        public int MagSize
        {
            get
            {
                Initialize();
                if (!CombatExtendedHelper.CombatExtended || CombatExtendedHelper.MagSizeDelegate == null ||
                    _ammoUserPropsMethod == null) { return 0; }
                var props = _ammoUserPropsMethod();
                if (props == null) { return 0; }
                try { return CombatExtendedHelper.MagSizeDelegate(props); }
                catch { return 0; }
            }
        }

        // ── кастомный стат ─────────────────────────────────────────────────
        private float GetCustomStatValue([NotNull] StatDef statDef)
        {
            if (!Enum.TryParse(CustomRangedWeaponStats.GetStatName(statDef.defName),
                    out CustomRangedWeaponStat stat))
            {
                Log.Error($"Equipment Manager: Unknown custom ranged stat ({statDef.defName})");
                return 0f;
            }
            return stat switch
            {
                CustomRangedWeaponStat.DpsRealistic     => DpsRealistic,
                CustomRangedWeaponStat.DpsaClose        => DpsaClose,
                CustomRangedWeaponStat.DpsaShort        => DpsaShort,
                CustomRangedWeaponStat.DpsaMedium       => DpsaMedium,
                CustomRangedWeaponStat.DpsaLong         => DpsaLong,
                CustomRangedWeaponStat.SightsEfficiency => SightsEfficiency,
                CustomRangedWeaponStat.ShotSpread       => ShotSpread,
                CustomRangedWeaponStat.SwayFactor       => SwayFactor,
                CustomRangedWeaponStat.Recoil           => Recoil,
                CustomRangedWeaponStat.MagazineSize     => MagazineSize,
                CustomRangedWeaponStat.ReloadTime       => ReloadTime,
                CustomRangedWeaponStat.Range            => MaxRange,
                CustomRangedWeaponStat.Warmup           => Warmup,
                CustomRangedWeaponStat.ArmorPenSharp    => ArmorPenSharp,
                CustomRangedWeaponStat.ArmorPenBlunt    => ArmorPenBlunt,
                CustomRangedWeaponStat.Damage           => Damage,
                CustomRangedWeaponStat.TechLevel        => (float)Thing.def.techLevel,
                _ => throw new ArgumentOutOfRangeException(nameof(statDef))
            };
        }

        public float GetStatValue([NotNull] StatDef statDef)
        {
            if (!StatValues.TryGetValue(statDef, out var value))
            {
                value = CustomRangedWeaponStats.IsCustomStat(statDef.defName)
                    ? GetCustomStatValue(statDef)
                    : StatHelper.GetStatValue(Thing, statDef);
                StatValues.Add(statDef, value);
            }
            return value;
        }

        public float GetStatValueDeviation([NotNull] StatDef statDef)
        {
            return statDef == null ? throw new ArgumentNullException(nameof(statDef)) :
                CustomRangedWeaponStats.IsCustomStat(statDef.defName)
                    ? GetCustomStatValue(statDef)
                    : StatHelper.GetStatValueDeviation(Thing, statDef);
        }

        // ── инициализация CE-делегатов ─────────────────────────────────────
        private void Initialize()
        {
            if (_initialized) { return; }
            _initialized = true;
            if (!CombatExtendedHelper.CombatExtended) { return; }
            try
            {
                if (AmmoUserComp == null)
                {
                    if (Thing.def.Verbs.Any(vp => string.Equals(vp.verbClass?.FullName,
                            "CombatExtended.Verb_ShootCEOneUse", StringComparison.OrdinalIgnoreCase)))
                    { _isAmmo = true; }
                }
                else
                {
                    var getter = AccessTools.PropertyGetter(CombatExtendedHelper.CompAmmoUserType, "Props");
                    if (getter == null)
                    { Log.Error("Equipment Manager: Could not find 'CombatExtended.CompAmmoUser.Props'"); }
                    else
                    { _ammoUserPropsMethod = AccessTools.MethodDelegate<AmmoUserPropsDelegate>(getter, AmmoUserComp); }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Equipment Manager: Could not create CE delegates for {Thing.LabelCapNoCount}: {ex.Message}");
                _ammoUserPropsMethod = null;
            }
        }

        // ── чтение свойств снаряда ─────────────────────────────────────────
        private void ReadProjectileProperties(ProjectileProperties proj)
        {
            Damage      = proj.GetDamageAmount(Thing);
            ArmorPenSharp = proj.GetArmorPenetration(Thing);
            ArmorPenBlunt = 0f;
        }

        private void ReadProjectilePropertiesCE(ProjectileProperties proj)
        {
            Damage = proj.GetDamageAmount(Thing);
            if (proj.GetType() != CombatExtendedHelper.ProjectilePropertiesType)
            {
                Log.Warning($"Equipment Manager: {Thing.LabelCapNoCount} projectile is not CE-compatible");
                ReadProjectileProperties(proj);
                return;
            }
            ArmorPenSharp = CombatExtendedHelper.ArmorPenetrationSharpDelegate != null
                ? CombatExtendedHelper.ArmorPenetrationSharpDelegate(proj) : 0f;
            ArmorPenBlunt = CombatExtendedHelper.ArmorPenetrationBluntDelegate != null
                ? CombatExtendedHelper.ArmorPenetrationBluntDelegate(proj) : 0f;
        }

        // ── основное обновление ────────────────────────────────────────────
        public override bool Update(RimworldTime time)
        {
            if (!base.Update(time)) { return false; }
            try
            {
                if (Thing.def?.Verbs == null) { return true; }

                var verb = Thing.def.Verbs.FirstOrDefault(vp => vp.range > 0)
                        ?? Thing.def.Verbs.FirstOrDefault();
                if (verb == null)
                {
                    Log.Error($"Equipment Manager: No verb for '{Thing.LabelCapNoCount}'");
                    return true;
                }

                // ── снаряд ─────────────────────────────────────────────────
                if (verb.defaultProjectile?.projectile != null)
                {
                    if (CombatExtendedHelper.CombatExtended)
                        ReadProjectilePropertiesCE(verb.defaultProjectile.projectile);
                    else
                        ReadProjectileProperties(verb.defaultProjectile.projectile);
                }

                // ── verbProps ──────────────────────────────────────────────
                BurstShotCount        = verb.burstShotCount <= 0 ? 1 : verb.burstShotCount;
                TicksBetweenBurstShots = verb.ticksBetweenBurstShots <= 0 ? 10 : verb.ticksBetweenBurstShots;
                Warmup  = verb.warmupTime;
                MinRange = verb.minRange;
                MaxRange = verb.range;
                Cooldown = Thing.GetStatValue(StatDefOf.RangedWeapon_Cooldown);

                // ── CE StatDef-ы ───────────────────────────────────────────
                if (CombatExtendedHelper.CombatExtended)
                {
                    SightsEfficiency = SafeGetStat(SdSightsEfficiency, 1f);
                    ShotSpread       = SafeGetStat(SdShotSpread,       0f);
                    SwayFactor       = SafeGetStat(SdSwayFactor,       0f);
                    Recoil           = SafeGetStat(SdRecoil,           0f);
                    MagazineSize     = SafeGetStat(SdMagazineCapacity, 0f);
                    ReloadTime       = SafeGetStat(SdReloadTime,       0f);
                }
                else
                {
                    SightsEfficiency = 1f;
                    ShotSpread = SwayFactor = Recoil = MagazineSize = ReloadTime = 0f;
                }

                // ── DPS с учётом перезарядки ───────────────────────────────
                // Время одной очереди (в тиках)
                var timePerBurstTicks = (Warmup + Cooldown) * 60f
                                      + BurstShotCount * TicksBetweenBurstShots;
                // Кол-во очередей на магазин (минимум 1)
                var burstsPerMag = MagazineSize > 0
                    ? (float)Math.Ceiling(MagazineSize / BurstShotCount)
                    : 1f;
                // Выстрелов на магазин
                var shotsPerMag = MagazineSize > 0 ? MagazineSize : (float)BurstShotCount;
                // Полное время на магазин (тики) + перезарядка
                var totalTicks = timePerBurstTicks * burstsPerMag + ReloadTime * 60f;
                DpsRealistic = totalTicks > 0
                    ? (float)Math.Round(Damage * shotsPerMag / totalTicks * 60f, 2)
                    : 0f;

                // ── Dpsa = DpsRealistic × SightsEfficiency по дистанциям ───
                // (SightsEfficiency — качество прицела, не зависит от дистанции,
                //  используем как единственный оружейный модификатор точности)
                DpsaClose  = MinRange <= 3f  && MaxRange >= 3f  ? DpsRealistic * SightsEfficiency : 0f;
                DpsaShort  = MinRange <= 12f && MaxRange >= 12f ? DpsRealistic * SightsEfficiency : 0f;
                DpsaMedium = MinRange <= 25f && MaxRange >= 25f ? DpsRealistic * SightsEfficiency : 0f;
                DpsaLong   = MinRange <= 40f && MaxRange >= 40f ? DpsRealistic * SightsEfficiency : 0f;
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Equipment Manager: Could not update cache of '{Thing.LabelCapNoCount}' ({Thing.def?.defName}): {ex.Message}");
            }
            return true;
        }

        private float SafeGetStat(StatDef sd, float fallback)
        {
            try { return sd != null ? Thing.GetStatValue(sd) : fallback; }
            catch { return fallback; }
        }
    }
}
