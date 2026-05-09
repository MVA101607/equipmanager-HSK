using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EquipmentManager
{
    internal class MeleeWeaponCache : ItemCache
    {
        private AccessTools.FieldRef<Tool, float> _armorPenetrationBluntDelegate;
        private AccessTools.FieldRef<Tool, float> _armorPenetrationSharpDelegate;
        private bool _initialized;
        private Type _toolType;

        // CE StatDef для MeleePenetrationFactor
        private static StatDef _sdMeleePenetrationFactor;
        private static StatDef SdMeleePenetrationFactor =>
            _sdMeleePenetrationFactor ??= DefDatabase<StatDef>.GetNamedSilentFail("MeleePenetrationFactor");

        public MeleeWeaponCache([NotNull] Thing thing)
        {
            Thing = thing ?? throw new ArgumentNullException(nameof(thing));
        }

        private float ArmorPenSharp { get; set; }
        private float ArmorPenBlunt { get; set; }
        private float DpsSharp { get; set; }
        private float DpsBlunt { get; set; }

        private AccessTools.FieldRef<Tool, float> ArmorPenetrationBluntDelegate
        {
            get { Initialize(); return _armorPenetrationBluntDelegate; }
        }

        private AccessTools.FieldRef<Tool, float> ArmorPenetrationSharpDelegate
        {
            get { Initialize(); return _armorPenetrationSharpDelegate; }
        }

        private Type ToolType
        {
            get { Initialize(); return _toolType; }
        }

        private Thing Thing { get; }

        private float GetCustomStatValue([NotNull] StatDef statDef)
        {
            try
            {
                if (!Enum.TryParse(CustomMeleeWeaponStats.GetStatName(statDef.defName),
                        out CustomMeleeWeaponStat meleeWeaponStat))
                {
                    Log.Error($"Equipment Manager: Unknown custom melee stat ({statDef.defName})");
                    return 0f;
                }
                return meleeWeaponStat switch
                {
                    CustomMeleeWeaponStat.DpsSharp     => DpsSharp,
                    CustomMeleeWeaponStat.DpsBlunt     => DpsBlunt,
                    CustomMeleeWeaponStat.ArmorPenSharp => ArmorPenSharp,
                    CustomMeleeWeaponStat.ArmorPenBlunt => ArmorPenBlunt,
                    CustomMeleeWeaponStat.TechLevel     => (float)Thing.def.techLevel,
                    _ => throw new ArgumentOutOfRangeException(nameof(statDef))
                };
            }
            catch (Exception e)
            {
                Log.Error(
                    $"Equipment Manager: Error evaluating custom melee stat '{statDef.defName}' of '{Thing.def.defName}':\n{e.Message}");
                return 0f;
            }
        }

        public float GetStatValue(StatDef statDef)
        {
            if (!StatValues.TryGetValue(statDef, out var value))
            {
                value = CustomMeleeWeaponStats.IsCustomStat(statDef.defName)
                    ? GetCustomStatValue(statDef)
                    : StatHelper.GetStatValue(Thing, statDef);
                StatValues.Add(statDef, value);
            }
            return value;
        }

        public float GetStatValueDeviation([NotNull] StatDef statDef)
        {
            return statDef == null ? throw new ArgumentNullException(nameof(statDef)) :
                CustomMeleeWeaponStats.IsCustomStat(statDef.defName)
                    ? GetCustomStatValue(statDef)
                    : StatHelper.GetStatValueDeviation(Thing, statDef);
        }

        private void Initialize()
        {
            if (_initialized) { return; }
            _initialized = true;
            if (!CombatExtendedHelper.CombatExtended) { return; }
            _toolType = AccessTools.TypeByName("CombatExtended.ToolCE");
            if (_toolType == null)
            {
                Log.Error("Equipment Manager: Could not find 'CombatExtended.ToolCE'");
                return;
            }
            _armorPenetrationSharpDelegate = AccessTools.FieldRefAccess<float>(_toolType, "armorPenetrationSharp");
            if (_armorPenetrationSharpDelegate == null)
            {
                Log.Error("Equipment Manager: Could not find 'CombatExtended.ToolCE.armorPenetrationSharp'");
            }

            _armorPenetrationBluntDelegate = AccessTools.FieldRefAccess<float>(_toolType, "armorPenetrationBlunt");
            if (_armorPenetrationBluntDelegate == null)
            {
                Log.Error("Equipment Manager: Could not find 'CombatExtended.ToolCE.armorPenetrationBlunt'");
            }
        }

        public override bool Update(RimworldTime time)
        {
            if (!base.Update(time)) { return false; }
            try
            {
                // ── DPS sharp/blunt — взвешены по AdjustedMeleeSelectionWeight ────
                var allVerbProps = VerbUtility.GetAllVerbProperties(Thing.def.Verbs, Thing.def.tools);
                if (allVerbProps != null)
                {
                    var sharpVerbs = allVerbProps.Where(vp =>
                        (vp.verbProps?.IsMeleeAttack ?? false) &&
                        "Sharp".Equals(vp.maneuver?.verb?.meleeDamageDef?.armorCategory?.defName,
                            StringComparison.OrdinalIgnoreCase)).ToList();
                    var bluntVerbs = allVerbProps.Where(vp =>
                        (vp.verbProps?.IsMeleeAttack ?? false) &&
                        "Blunt".Equals(vp.maneuver?.verb?.meleeDamageDef?.armorCategory?.defName,
                            StringComparison.OrdinalIgnoreCase)).ToList();

                    DpsSharp = CalcDps(sharpVerbs);
                    DpsBlunt = CalcDps(bluntVerbs);
                }

                // ── ArmorPen sharp/blunt — взвешены по chanceFactor ──────────────
                if (CombatExtendedHelper.CombatExtended && ToolType != null &&
                    ArmorPenetrationSharpDelegate != null && ArmorPenetrationBluntDelegate != null)
                {
                    var tools = Thing.def.tools?
                        .Where(t => t.power > 0f && t.GetType() == ToolType)
                        .ToList() ?? new List<Tool>();

                    if (tools.Count > 0)
                    {
                        var totalWeight = tools.Sum(t => t.chanceFactor);
                        var penFactor = SdMeleePenetrationFactor != null
                            ? Thing.GetStatValue(SdMeleePenetrationFactor)
                            : 1f;

                        if (totalWeight > 0f)
                        {
                            ArmorPenSharp = tools.Sum(t =>
                                (t.chanceFactor / totalWeight) * ArmorPenetrationSharpDelegate(t)) * penFactor;
                            ArmorPenBlunt = tools.Sum(t =>
                                (t.chanceFactor / totalWeight) * ArmorPenetrationBluntDelegate(t)) * penFactor;
                        }
                        else
                        {
                            ArmorPenSharp = ArmorPenBlunt = 0f;
                        }
                    }
                    else
                    {
                        // Не CE-инструменты — откат на ванильный armorPenetration
                        var vanillaTools = Thing.def.tools?.Where(t => t.power > 0f).ToList()
                            ?? new List<Tool>();
                        var totalWeight = vanillaTools.Sum(t => t.chanceFactor);
                        ArmorPenSharp = totalWeight > 0f
                            ? vanillaTools.Sum(t => (t.chanceFactor / totalWeight) * t.armorPenetration)
                            : 0f;
                        ArmorPenBlunt = 0f;
                    }
                }
                else
                {
                    // CE не активен — ванильный fallback
                    var tools = Thing.def.tools?.Where(t => t.power > 0f).ToList() ?? new List<Tool>();
                    var totalWeight = tools.Sum(t => t.chanceFactor);
                    ArmorPenSharp = totalWeight > 0f
                        ? tools.Sum(t => (t.chanceFactor / totalWeight) * t.armorPenetration)
                        : 0f;
                    ArmorPenBlunt = 0f;
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Equipment Manager: Could not update cache of '{Thing.LabelCapNoCount}' ({Thing.def?.defName}): {ex.Message}");
            }
            return true;
        }

        private float CalcDps(List<VerbUtility.VerbPropertiesWithSource> verbs)
        {
            if (!verbs.Any()) { return 0f; }
            var dmg = verbs.AverageWeighted(
                vp => vp.verbProps.AdjustedMeleeSelectionWeight(vp.tool, null, Thing, null, false),
                vp => vp.verbProps.AdjustedMeleeDamageAmount(vp.tool, null, Thing, null));
            var cd = verbs.AverageWeighted(
                vp => vp.verbProps.AdjustedMeleeSelectionWeight(vp.tool, null, Thing, null, false),
                vp => vp.verbProps.AdjustedCooldown(vp.tool, null, Thing));
            return cd <= 0f ? 0f : (float)Math.Round(dmg / cd, 2);
        }
    }
}
