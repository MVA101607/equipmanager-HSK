using EquipmentManager.CustomWidgets;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Strings = EquipmentManager.Resources.Strings.WeaponRules;

namespace EquipmentManager.Windows
{
    internal partial class ManageWeaponRulesDialog
    {
        private readonly List<Thing> _currentlyAvailableWorkTypes = new();
        private readonly List<ThingDef> _globallyAvailableWorkTypes = new();
        private WorkTypeRule _selectedWorkTypeRule;

        private WorkTypeRule SelectedWorkTypeRule
        {
            get => _selectedWorkTypeRule;
            set
            {
                _selectedWorkTypeRule = value;
                UpdateAvailableItems_WorkTypes();
            }
        }

        private void DoButtonRow_WorkTypes(Rect rect)
        {
            const int buttonCount = 1;
            var buttonWidth = (rect.width - (UiHelpers.ButtonGap * (buttonCount - 1))) / buttonCount;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, buttonWidth, UiHelpers.ButtonHeight), Strings.SelectRule))
            {
                Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetWorkTypeRules().Select(rule =>
                    new FloatMenuOption(rule.Label, () => SelectedWorkTypeRule = rule)).ToList()));
            }
        }

        private void DoTab_WorkTypes(Rect rect)
        {
            var sectionHeaderHeight = Text.LineHeightOf(GameFont.Medium) + UiHelpers.ElementGap;
            var buttonRowRect = new Rect(rect.x, rect.y, rect.width, UiHelpers.ButtonHeight);
            var labelRect = new Rect(rect.x, buttonRowRect.yMax + UiHelpers.ElementGap, rect.width,
                UiHelpers.LabelHeight);
            var availableItemsBoxHeight = (ItemIconSize * AvailableItemIconsRowCount) +
                (ItemIconGap * (AvailableItemIconsRowCount + 1));
            var availableItemsRect = new Rect(rect.x, rect.yMax - availableItemsBoxHeight - sectionHeaderHeight,
                rect.width, availableItemsBoxHeight + sectionHeaderHeight);
            var statsRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width,
                availableItemsRect.y - UiHelpers.ElementGap - labelRect.yMax - UiHelpers.ElementGap);
            DoButtonRow_WorkTypes(buttonRowRect);
            UiHelpers.DoGapLineHorizontal(new Rect(rect.x, buttonRowRect.yMax, rect.width, UiHelpers.ElementGap));
            if (SelectedWorkTypeRule == null) { LabelInput.DoLabelWithoutInput(labelRect, Strings.NoRuleSelected); }
            else
            {
                LabelInput.DoLabelInputReadOnly(labelRect, Strings.RuleLabel, SelectedWorkTypeRule.Label);
                UiHelpers.DoGapLineHorizontal(new Rect(rect.x, labelRect.yMax, rect.width, UiHelpers.ElementGap));
                DoRuleStatWeights(statsRect, StatHelper.WorkTypeStatDefs, SelectedWorkTypeRule.GetStatWeights(), def =>
                {
                    SelectedWorkTypeRule.SetStatWeight(def, 0f);
                    UpdateAvailableItems_WorkTypes();
                }, statDefName =>
                {
                    SelectedWorkTypeRule.DeleteStatWeight(statDefName);
                    UpdateAvailableItems_WorkTypes();
                });
                UiHelpers.DoGapLineHorizontal(new Rect(rect.x, statsRect.yMax, rect.width, UiHelpers.ElementGap));
                DoAvailableItems(availableItemsRect, _globallyAvailableWorkTypes, def => { },
                    def => GetWorkTypeDefTooltip(def, SelectedWorkTypeRule), _currentlyAvailableWorkTypes, thing => { },
                    thing => GetWorkTypeTooltip(thing, SelectedWorkTypeRule), UpdateAvailableItems_WorkTypes);
            }
        }

        private string GetWorkTypeDefTooltip(ThingDef def, WorkTypeRule rule)
        {
            var stringBuilder = new StringBuilder();
            _ = stringBuilder.AppendLine(def.LabelCap);

            var statWeights = rule.GetStatWeights().Where(sw => sw.StatDef != null).ToList();
            if (!statWeights.Any()) { return stringBuilder.ToString(); }

            var time = RimworldTime.GetMapTime(Find.CurrentMap);
            var cache = EquipmentManager.GetToolDefCache(def, time);
            var workTypeDefs = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder.ToList();

            _ = stringBuilder.AppendLine();
            foreach (var sw in statWeights)
            {
                var value = cache.GetStatValue(sw.StatDef, workTypeDefs);
                _ = stringBuilder.AppendLine($"- {sw.StatDef.LabelCap} = {value:N2}  (weight: {sw.Weight:N1})");
            }

            var score = rule.GetThingDefScore(def);
            _ = stringBuilder.AppendLine();
            _ = stringBuilder.AppendLine($"Score: {score:N3}");

            return stringBuilder.ToString();
        }

        private string GetWorkTypeTooltip(Thing thing, WorkTypeRule rule)
        {
            var sb = new StringBuilder();
            _ = sb.AppendLine(thing.LabelCapNoCount);

            var statWeights = rule.GetStatWeights().Where(sw => sw.StatDef != null).ToList();
            if (!statWeights.Any()) { return sb.ToString(); }

            // Читаем SpecialDisplayStats в словарь
            var specialStats = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var entry in thing.SpecialDisplayStats() ?? Enumerable.Empty<StatDrawEntry>())
                {
                    if (entry == null || entry.LabelCap.NullOrEmpty()) { continue; }
                    var raw = entry.ValueString?.Replace("%", "").Replace("x", "").Trim();
                    if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var val))
                    { specialStats[entry.LabelCap] = val; }
                }
            }
            catch { /* игнорируем */ }

            _ = sb.AppendLine();
            foreach (var sw in statWeights)
            {
                _ = specialStats.TryGetValue(sw.StatDef.LabelCap, out var val);
                _ = sb.AppendLine($"- {sw.StatDef.LabelCap} = {val:N3}  weight:{sw.Weight:N1}");
            }

            _ = sb.AppendLine();
            _ = sb.AppendLine($"Score: {rule.GetThingScore(thing):N3}");

            return sb.ToString();
        }

        private void UpdateAvailableItems_WorkTypes()
        {
            _globallyAvailableWorkTypes.Clear();
            _currentlyAvailableWorkTypes.Clear();
            if (SelectedWorkTypeRule == null) { return; }
            _globallyAvailableWorkTypes.AddRange(SelectedWorkTypeRule.GetGloballyAvailableItems());
            _currentlyAvailableWorkTypes.AddRange(
                SelectedWorkTypeRule.GetCurrentlyAvailableItemsSorted(Find.CurrentMap));
        }
    }
}