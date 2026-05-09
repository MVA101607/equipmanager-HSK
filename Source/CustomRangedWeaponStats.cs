using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;

namespace EquipmentManager
{
    internal enum CustomRangedWeaponStat
    {
        // ── вычисляются модом ──────────────────────────────────────────────
        DpsRealistic,           // DPS с учётом перезарядки магазина
        DpsaClose,              // DpsRealistic × SightsEfficiency, дист. ≤ 3
        DpsaShort,              // дист. ≤ 12
        DpsaMedium,             // дист. ≤ 25
        DpsaLong,               // дист. ≤ 40

        // ── читаются из CE StatDef через GetStatValue ──────────────────────
        SightsEfficiency,       // "SightsEfficiency"  — качество прицела
        ShotSpread,             // "ShotSpread"        — угол разброса (°)
        SwayFactor,             // "SwayFactor"        — дрожание
        Recoil,                 // "Recoil"            — отдача
        MagazineSize,           // "MagazineCapacity"  — размер магазина
        ReloadTime,             // "ReloadTime"        — время перезарядки (с)

        // ── читаются из verbProps / ProjectilePropertiesCE ─────────────────
        Range,                  // verbProps.range
        Warmup,                 // verbProps.warmupTime
        ArmorPenSharp,          // ProjectilePropertiesCE.armorPenetrationSharp
        ArmorPenBlunt,          // ProjectilePropertiesCE.armorPenetrationBlunt
        Damage,                 // projectile.GetDamageAmount
        TechLevel
    }

    internal static class CustomRangedWeaponStats
    {
        private const string Category = "RangedWeapons";

        private static StatCategoryDef CategoryDef { get; } = new StatCategoryDef
        {
            defName = $"{StatHelper.CustomStatPrefix}_{Category}",
            label = $"{StatHelper.CustomStatPrefix}_{Category}"
        };

        private static IEnumerable<string> StatDefNames =>
            Enum.GetValues(typeof(CustomRangedWeaponStat))
                .OfType<CustomRangedWeaponStat>()
                .Select(GetStatDefName);

        public static IEnumerable<StatDef> StatDefs { get; } = StatDefNames.Select(defName =>
            new StatDef
            {
                defName = defName,
                label = Resources.Strings.Stats.GetStatLabel(defName),
                description = Resources.Strings.Stats.GetStatDescription(defName),
                category = CategoryDef
            });

        public static string GetStatDefName(CustomRangedWeaponStat stat)
        {
            return $"{StatHelper.CustomStatPrefix}_{Category}_{stat}";
        }

        public static string GetStatName(string defName)
        {
            var categoryPrefix = $"{StatHelper.CustomStatPrefix}_{Category}_";
            return defName.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase)
                ? defName.Substring(categoryPrefix.Length)
                : null;
        }

        public static bool IsCustomStat(string defName)
        {
            return StatDefNames.Contains(defName);
        }
    }
}
