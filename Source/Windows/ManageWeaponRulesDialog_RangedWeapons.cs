using System.Collections.Generic;
using System.Linq;
using System.Text;
using EquipmentManager.CustomWidgets;
using UnityEngine;
using Verse;
using Strings = EquipmentManager.Resources.Strings.WeaponRules;

namespace EquipmentManager.Windows
{
    internal partial class ManageWeaponRulesDialog
    {
        private readonly List<Thing> _currentlyAvailableRangedWeapons = new();
        private readonly List<ThingDef> _globallyAvailableRangedWeapons = new();
        private RangedWeaponRule _selectedRangedWeaponRule;

        private RangedWeaponRule SelectedRangedWeaponRule
        {
            get => _selectedRangedWeaponRule;
            set
            {
                CheckSelectedItemRuleHasName(_selectedRangedWeaponRule);
                _selectedRangedWeaponRule?.UpdateGloballyAvailableItems();
                _selectedRangedWeaponRule = value;
                UpdateAvailableItems_RangedWeapons();
            }
        }

        private void DoButtonRow_RangedWeapons(Rect rect)
        {
            const int buttonCount = 4;
            var buttonWidth = (rect.width - (UiHelpers.ButtonGap * (buttonCount - 1))) / buttonCount;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, buttonWidth, UiHelpers.ButtonHeight), Strings.SelectRule))
            {
                Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetRangedWeaponRules().Select(rule =>
                    new FloatMenuOption(rule.Label, () => SelectedRangedWeaponRule = rule)).ToList()));
            }
            if (Widgets.ButtonText(
                    new Rect(rect.x + buttonWidth + UiHelpers.ButtonGap, rect.y, buttonWidth, UiHelpers.ButtonHeight),
                    Strings.AddRule)) { SelectedRangedWeaponRule = EquipmentManager.AddRangedWeaponRule(); }
            if (Widgets.ButtonText(
                    new Rect(rect.x + ((buttonWidth + UiHelpers.ButtonGap) * 2), rect.y, buttonWidth,
                        UiHelpers.ButtonHeight), Strings.CopyRule))
            {
                Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetRangedWeaponRules().Select(rule =>
                    new FloatMenuOption(rule.Label,
                        () => SelectedRangedWeaponRule = EquipmentManager.CopyRangedWeaponRule(rule))).ToList()));
            }
            if (Widgets.ButtonText(
                    new Rect(rect.x + ((buttonWidth + UiHelpers.ButtonGap) * 3), rect.y, buttonWidth,
                        UiHelpers.ButtonHeight), Strings.DeleteRule))
            {
                Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetRangedWeaponRules()
                    .Where(rule => !rule.Protected).Select(rule => new FloatMenuOption(rule.Label, () =>
                    {
                        EquipmentManager.DeleteRangedWeaponRule(rule);
                        if (rule == SelectedRangedWeaponRule) { SelectedRangedWeaponRule = null; }
                    })).ToList()));
            }
        }

        private void DoItemProperties_RangedWeapons(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Widgets.Label(labelRect, Strings.ItemProperties);
            Text.Font = font;
            Text.Anchor = anchor;
            var propertiesRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width,
                rect.yMax - (labelRect.yMax + UiHelpers.ElementGap));
            var columnWidth =
                (propertiesRect.width - (UiHelpers.ElementGap * (UiHelpers.BoolSettingsColumnCount - 1))) /
                UiHelpers.BoolSettingsColumnCount;
            for (var i = 1; i < UiHelpers.BoolSettingsColumnCount; i++)
            {
                UiHelpers.DoGapLineVertical(new Rect(
                    propertiesRect.x + (i * (columnWidth + UiHelpers.ElementGap)) - UiHelpers.ElementGap,
                    propertiesRect.y, UiHelpers.ElementGap, propertiesRect.height));
            }
            DoRuleSetting(UiHelpers.GetBoolSettingRect(propertiesRect, 0, columnWidth),
                () => SelectedRangedWeaponRule.Explosive, value =>
                {
                    SelectedRangedWeaponRule.Explosive = value;
                    UpdateAvailableItems_RangedWeapons();
                }, Strings.RangedWeapons.Explosive, Strings.RangedWeapons.ExplosiveTooltip);
            DoRuleSetting(UiHelpers.GetBoolSettingRect(propertiesRect, 1, columnWidth),
                () => SelectedRangedWeaponRule.ManualCast, value =>
                {
                    SelectedRangedWeaponRule.ManualCast = value;
                    UpdateAvailableItems_RangedWeapons();
                }, Strings.RangedWeapons.ManualCast, Strings.RangedWeapons.ManualCastTooltip);
        }

        private static string AmmoTypePrefLabel(AmmoTypePreference t)
        {
            return t switch
            {
                AmmoTypePreference.Any => "EquipmentManager.AmmoType.Any".Translate(),
                AmmoTypePreference.Stone => "EquipmentManager.AmmoType.Stone".Translate(),
                AmmoTypePreference.Steel => "EquipmentManager.AmmoType.Steel".Translate(),
                AmmoTypePreference.Plasteel => "EquipmentManager.AmmoType.Plasteel".Translate(),
                AmmoTypePreference.Venom => "EquipmentManager.AmmoType.Venom".Translate(),
                AmmoTypePreference.Flame => "EquipmentManager.AmmoType.Flame".Translate(),
                _ => t.ToString()
            };
        }

        private void DoRuleSettings_RangedWeapons(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Widgets.Label(labelRect, Strings.RuleSettings);
            Text.Font = font;
            Text.Anchor = anchor;
            var currentY = labelRect.yMax + UiHelpers.ElementGap;
            var retentionRect = LabelInput.DoLabeledRect(
                new Rect(rect.x, currentY, rect.width, UiHelpers.ListRowHeight),
                Strings.RangedWeapons.RetentionBonus, Strings.RangedWeapons.RetentionBonusTooltip);
            SelectedRangedWeaponRule.RetentionBonus = Widgets.HorizontalSlider(retentionRect,
                SelectedRangedWeaponRule.RetentionBonus, 1f, 2f, true,
                $"x{SelectedRangedWeaponRule.RetentionBonus:F2}", roundTo: 0.05f);
            currentY += UiHelpers.ListRowHeight + UiHelpers.ElementGap;
            if (!CombatExtendedHelper.EnableAmmoSystem) { return; }
            // Слайдер: количество магазинов (0..30)
            var ammoCountRect = LabelInput.DoLabeledRect(
                new Rect(rect.x, currentY, rect.width, UiHelpers.ListRowHeight),
                Strings.RangedWeapons.AmmoCount, Strings.RangedWeapons.AmmoCountTooltip);
            SelectedRangedWeaponRule.AmmoCount = (int) Widgets.HorizontalSlider(ammoCountRect,
                SelectedRangedWeaponRule.AmmoCount, 0f, 30f, true,
                $"{SelectedRangedWeaponRule.AmmoCount} {"EquipmentManager.Magazines".Translate()}",
                roundTo: 1f);
            // Dropdown типа патрона — смещён на 1.5 строки, занимает правую четверть
            currentY += (1.5f * UiHelpers.ListRowHeight) + UiHelpers.ElementGap;
            var ammoTypeLabelRect = new Rect(
                rect.x + (rect.width * 0.5f),
                currentY,
                rect.width * 0.25f,
                UiHelpers.ListRowHeight);
            TooltipHandler.TipRegion(ammoTypeLabelRect,
                "EquipmentManager.WeaponRules.RangedWeapons.AmmoTypeTooltip".Translate());
            Widgets.Label(ammoTypeLabelRect,
                "EquipmentManager.WeaponRules.RangedWeapons.AmmoType".Translate());
            var ammoTypeButtonRect = new Rect(
                rect.x + (rect.width * 0.75f),
                currentY,
                rect.width * 0.25f,
                UiHelpers.ListRowHeight);
            if (Widgets.ButtonText(ammoTypeButtonRect,
                AmmoTypePrefLabel(SelectedRangedWeaponRule.AmmoTypePreference)))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>
                {
                    new("— " + "EquipmentManager.AmmoGroup.General".Translate() + " —",
                        null) { Disabled = true },
                    new(AmmoTypePrefLabel(AmmoTypePreference.Any),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.Any),
                    new("— " + "EquipmentManager.AmmoGroup.Firearms".Translate() + " —",
                        null) { Disabled = true },
                    new(AmmoTypePrefLabel(AmmoTypePreference.FMJ),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.FMJ),
                    new(AmmoTypePrefLabel(AmmoTypePreference.AP),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.AP),
                    new(AmmoTypePrefLabel(AmmoTypePreference.HP),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.HP),
                    new(AmmoTypePrefLabel(AmmoTypePreference.HE),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.HE),
                    new("— " + "EquipmentManager.AmmoGroup.PrimitiveWeapons".Translate() + " —",
                        null) { Disabled = true },
                    new(AmmoTypePrefLabel(AmmoTypePreference.Stone),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.Stone),
                    new(AmmoTypePrefLabel(AmmoTypePreference.Steel),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.Steel),
                    new(AmmoTypePrefLabel(AmmoTypePreference.Plasteel),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.Plasteel),
                    new(AmmoTypePrefLabel(AmmoTypePreference.Venom),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.Venom),
                    new(AmmoTypePrefLabel(AmmoTypePreference.Flame),
                        () => SelectedRangedWeaponRule.AmmoTypePreference = AmmoTypePreference.Flame),
                };
                Find.WindowStack.Add(new FloatMenu(options, null, false));
            }
        }

        private void DoTab_RangedWeapons(Rect rect)
        {
            // RetentionBonus — всегда 1 строка; AmmoCount — только с CE
            var ruleSettingsCount = 1 + (CombatExtendedHelper.EnableAmmoSystem ? 2 : 0);
            const int itemPropertiesCount = 2;
            GetWeaponRuleTabRects(rect, ruleSettingsCount, itemPropertiesCount, out var buttonRowRect,
                out var labelRect, out var equipModeRect, out var ruleSettingsRect, out var itemPropertiesRect,
                out var availableItemsRect, out var exclusiveItemsRect, out var statsRect);
            DoButtonRow_RangedWeapons(buttonRowRect);
            UiHelpers.DoGapLineHorizontal(new Rect(rect.x, buttonRowRect.yMax, rect.width, UiHelpers.ElementGap));
            if (SelectedRangedWeaponRule == null) { LabelInput.DoLabelWithoutInput(labelRect, Strings.NoRuleSelected); }
            else
            {
                LabelInput.DoLabelInput(labelRect, Strings.RuleLabel, ref SelectedRangedWeaponRule.Label);
                UiHelpers.DoGapLineVertical(new Rect(rect.center.x - (UiHelpers.ElementGap / 2f), labelRect.y,
                    UiHelpers.ElementGap, labelRect.height));
                DoWeaponRuleEquipMode(equipModeRect, () => SelectedRangedWeaponRule.EquipMode,
                    mode => SelectedRangedWeaponRule.EquipMode = mode);
                UiHelpers.DoGapLineHorizontal(new Rect(rect.x, labelRect.yMax, rect.width, UiHelpers.ElementGap));
                if (ruleSettingsCount > 0)
                {
                    DoRuleSettings_RangedWeapons(ruleSettingsRect);
                    UiHelpers.DoGapLineHorizontal(new Rect(rect.x, ruleSettingsRect.yMax, rect.width,
                        UiHelpers.ElementGap));
                }
                DoItemProperties_RangedWeapons(itemPropertiesRect);
                UiHelpers.DoGapLineHorizontal(new Rect(rect.x, itemPropertiesRect.yMax, rect.width,
                    UiHelpers.ElementGap));
                DoRuleStats(statsRect, StatHelper.RangedWeaponStatDefs, SelectedRangedWeaponRule.GetStatWeights(),
                    def =>
                    {
                        SelectedRangedWeaponRule.SetStatWeight(def, 0f, false);
                        UpdateAvailableItems_RangedWeapons();
                    }, statDefName =>
                    {
                        SelectedRangedWeaponRule.DeleteStatWeight(statDefName);
                        UpdateAvailableItems_RangedWeapons();
                    }, SelectedRangedWeaponRule.GetStatLimits(), def =>
                    {
                        SelectedRangedWeaponRule.SetStatLimit(def, null, null);
                        UpdateAvailableItems_RangedWeapons();
                    }, statDefName =>
                    {
                        SelectedRangedWeaponRule.DeleteStatLimit(statDefName);
                        UpdateAvailableItems_RangedWeapons();
                    });
                UiHelpers.DoGapLineHorizontal(new Rect(rect.x, statsRect.yMax, rect.width, UiHelpers.ElementGap));
                DoExclusiveItems(exclusiveItemsRect, RangedWeaponRule.AllRelevantThings,
                    SelectedRangedWeaponRule.GetWhitelistedItems(), def =>
                    {
                        SelectedRangedWeaponRule.DeleteWhitelistedItem(def.defName);
                        UpdateAvailableItems_RangedWeapons();
                    }, def =>
                    {
                        SelectedRangedWeaponRule.AddWhitelistedItem(def);
                        UpdateAvailableItems_RangedWeapons();
                    }, SelectedRangedWeaponRule.GetBlacklistedItems(), def =>
                    {
                        SelectedRangedWeaponRule.DeleteBlacklistedItem(def.defName);
                        UpdateAvailableItems_RangedWeapons();
                    }, def =>
                    {
                        SelectedRangedWeaponRule.AddBlacklistedItem(def);
                        UpdateAvailableItems_RangedWeapons();
                    }, def => GetRangedWeaponDefTooltip(def, SelectedRangedWeaponRule));
                UiHelpers.DoGapLineHorizontal(new Rect(rect.x, exclusiveItemsRect.yMax, rect.width,
                    UiHelpers.ElementGap));
                DoAvailableItems(availableItemsRect, _globallyAvailableRangedWeapons, def =>
                    {
                        SelectedRangedWeaponRule.AddBlacklistedItem(def);
                        UpdateAvailableItems_RangedWeapons();
                    }, def => GetRangedWeaponDefTooltip(def, SelectedRangedWeaponRule),
                    _currentlyAvailableRangedWeapons,
                    thing =>
                    {
                        SelectedRangedWeaponRule.AddBlacklistedItem(thing.def);
                        UpdateAvailableItems_RangedWeapons();
                    }, thing => GetRangedWeaponTooltip(thing, SelectedRangedWeaponRule),
                    UpdateAvailableItems_RangedWeapons);
            }
        }

        private string GetRangedWeaponDefTooltip(ThingDef def, ItemRule rule)
        {
            var stringBuilder = new StringBuilder();
            _ = stringBuilder.AppendLine(def.LabelCap);
            var stats = rule.GetStatWeights().Where(sw => sw.StatDef != null).Select(sw => sw.StatDef)
                .Union(rule.GetStatLimits().Where(sl => sl.StatDef != null).Select(sl => sl.StatDef)).ToHashSet();
            if (!stats.Any()) { return stringBuilder.ToString(); }
            var cache = EquipmentManager.GetRangedWeaponDefCache(def, RimworldTime.GetMapTime(Find.CurrentMap));
            _ = stringBuilder.AppendLine();
            foreach (var stat in stats)
            {
                _ = stringBuilder.AppendLine($"- {stat.LabelCap} = {cache.GetStatValue(stat):N2}");
            }
            return stringBuilder.ToString();
        }

        private string GetRangedWeaponTooltip(Thing thing, ItemRule rule)
        {
            var stringBuilder = new StringBuilder();
            _ = stringBuilder.AppendLine(thing.LabelCapNoCount);
            var stats = rule.GetStatWeights().Where(sw => sw.StatDef != null).Select(sw => sw.StatDef)
                .Union(rule.GetStatLimits().Where(sl => sl.StatDef != null).Select(sl => sl.StatDef)).ToHashSet();
            if (!stats.Any()) { return stringBuilder.ToString(); }
            var cache = EquipmentManager.GetRangedWeaponCache(thing, RimworldTime.GetMapTime(Find.CurrentMap));
            _ = stringBuilder.AppendLine();
            foreach (var stat in stats)
            {
                _ = stringBuilder.AppendLine($"- {stat.LabelCap} = {cache.GetStatValue(stat):N2}");
            }
            return stringBuilder.ToString();
        }

        private void PreClose_RangedWeapons()
        {
            CheckSelectedItemRuleHasName(_selectedRangedWeaponRule);
            _selectedRangedWeaponRule?.UpdateGloballyAvailableItems();
        }

        private void UpdateAvailableItems_RangedWeapons()
        {
            _globallyAvailableRangedWeapons.Clear();
            _currentlyAvailableRangedWeapons.Clear();
            if (SelectedRangedWeaponRule == null) { return; }
            var map = Find.CurrentMap;
            SelectedRangedWeaponRule.UpdateGloballyAvailableItems();
            _globallyAvailableRangedWeapons.AddRange(
                SelectedRangedWeaponRule.GetGloballyAvailableItemsSorted(RimworldTime.GetMapTime(map)));
            _currentlyAvailableRangedWeapons.AddRange(
                SelectedRangedWeaponRule.GetCurrentlyAvailableItemsSorted(map, RimworldTime.GetMapTime(map)));
        }
    }
}