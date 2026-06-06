using System;
using System.Linq;
using EquipmentManager.CustomWidgets;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Strings = EquipmentManager.Resources.Strings.Roles;

namespace EquipmentManager.Windows
{
    internal class ManageRolesDialog : Window
    {
        private static Vector2 _availablePawnsScrollPosition;
        private static Vector2 _scrollPosition;
        private float _scrollViewHeight;
        private Role _selectedRole;
        private string _saveProfileName = string.Empty;
        private bool _showSaveProfileDialog;

        public ManageRolesDialog(Role selectedRole)
        {
            forcePause = true;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            SelectedRole = selectedRole;
        }

        private int AvailablePawnsColumnCount => InitialSize.x < MaxSize.x ? 3 : 5;
        private int AvailablePawnsRowCount => InitialSize.y < MaxSize.y ? 2 : 3;
        private static float AvailablePawnsRowHeight => Text.LineHeightOf(GameFont.Small) + UiHelpers.ElementGap;

        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        public override Vector2 InitialSize => UiHelpers.GetWindowSize(new Vector2(850f, 650f), MaxSize);
        private int LabeledButtonListColumnCount => InitialSize.x < MaxSize.x ? 2 : 3;
        private static Vector2 MaxSize => new(1200f, 1000f);
        private int PawnSettingsColumnCount => InitialSize.x < MaxSize.x ? 3 : 4;

        private Role SelectedRole
        {
            get => _selectedRole;
            set
            {
                CheckSelectedRoleHasName();
                _selectedRole = value;
                ResetScrollPositions();
            }
        }

        private void CheckSelectedRoleHasName()
        {
            if (SelectedRole == null || !SelectedRole.Label.NullOrEmpty()) { return; }
            SelectedRole.Label = $"{SelectedRole.Id}";
        }

        private void DoAvailablePawns(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Widgets.Label(labelRect, Strings.AvailablePawns);
            Text.Font = font;
            Text.Anchor = anchor;
            PawnBox.DoPawnBox(
                new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width,
                    rect.yMax - (labelRect.yMax + UiHelpers.ElementGap)), new Color(1f, 1f, 1f, 0.05f),
                new Color(1f, 1f, 1f, 0.4f), AvailablePawnsColumnCount, UiHelpers.ElementGap,
                ref _availablePawnsScrollPosition, SelectedRole.GetAvailablePawnsOrdered());
        }

        private void DoButtonRow(Rect rect)
        {
            const int buttonCount = 8;
            var buttonWidth = (rect.width - (UiHelpers.ButtonGap * (buttonCount - 1))) / buttonCount;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, buttonWidth, UiHelpers.ButtonHeight),
                    Strings.SelectRole))
            {
                Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetRoles()
                    .Select(loadout => new FloatMenuOption(loadout.Label, () => SelectedRole = loadout)).ToList()));
            }
            if (Widgets.ButtonText(
                    new Rect(rect.x + buttonWidth + UiHelpers.ButtonGap, rect.y, buttonWidth, UiHelpers.ButtonHeight),
                    Strings.AddRole)) { SelectedRole = EquipmentManager.AddRole(); }
            if (Widgets.ButtonText(
                    new Rect(rect.x + ((buttonWidth + UiHelpers.ButtonGap) * 2), rect.y, buttonWidth,
                        UiHelpers.ButtonHeight), Strings.CopyRole))
            {
                Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetRoles().Select(loadout =>
                        new FloatMenuOption(loadout.Label,
                            () => SelectedRole = EquipmentManager.CopyRole(loadout)))
                    .ToList()));
            }
            if (Widgets.ButtonText(
                    new Rect(rect.x + ((buttonWidth + UiHelpers.ButtonGap) * 3), rect.y, buttonWidth,
                        UiHelpers.ButtonHeight), Strings.DeleteRole))
            {
                Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetRoles().Select(loadout =>
                    new FloatMenuOption(loadout.Label, () =>
                    {
                        EquipmentManager.DeleteRole(loadout);
                        if (loadout == SelectedRole) { SelectedRole = null; }
                    })).ToList()));
            }
            if (Widgets.ButtonText(
                    new Rect(rect.x + ((buttonWidth + UiHelpers.ButtonGap) * 4), rect.y, buttonWidth,
                        UiHelpers.ButtonHeight), Strings.ManageWeaponRules))
            {
                Find.WindowStack.Add(new ManageWeaponRulesDialog());
            }
            // Кнопка "Сохранить профиль"
            if (Widgets.ButtonText(
                    new Rect(rect.x + ((buttonWidth + UiHelpers.ButtonGap) * 5), rect.y, buttonWidth,
                        UiHelpers.ButtonHeight), Strings.SaveProfile))
            {
                _saveProfileName = string.Empty;
                Find.WindowStack.Add(new SaveProfileDialog(onConfirm: name => RolesProfileManager.SaveProfile(name)));
            }
            // Кнопка "Загрузить профиль" — открывает ImportRolesDialog (читает .xml-профиль)
            if (Widgets.ButtonText(
                    new Rect(rect.x + ((buttonWidth + UiHelpers.ButtonGap) * 6), rect.y, buttonWidth,
                        UiHelpers.ButtonHeight), Strings.ImportRoles))
            {
                Find.WindowStack.Add(new ImportRolesDialog());
            }
            if (Widgets.ButtonText(
                    new Rect(rect.x + ((buttonWidth + UiHelpers.ButtonGap) * 7), rect.y, buttonWidth,
                        UiHelpers.ButtonHeight), Strings.Log)) { Find.WindowStack.Add(new LogDialog()); }
        }

        /// <summary>
        /// Встроенный мини-диалог ввода имени профиля (рисуется поверх основного окна).
        /// </summary>
        private void DoSaveProfileDialog(Rect inRect)
        {
            if (!_showSaveProfileDialog) { return; }
            // ↓ Поглощаем все клики по основному окну
            if (Mouse.IsOver(inRect))
            {
                // Блокируем все события вне dialogRect
                var e = Event.current;
                if (e.isMouse || e.isKey)
                {
                    // вычисляем dialogRect заново (до ContractedBy)
                    const float dW = 420f;
                    const float dH = 110f;
                    var blocker = new Rect(
                        inRect.center.x - (dW / 2f),
                        inRect.center.y - (dH / 2f),
                        dW, dH);
                    if (!blocker.Contains(e.mousePosition))
                    {
                        e.Use(); // поглощаем клик вне диалога
                    }
                }
            }
            const float dialogW = 420f;
            const float dialogH = 110f;
            var dialogRect = new Rect(
                inRect.center.x - (dialogW / 2f),
                inRect.center.y - (dialogH / 2f),
                dialogW, dialogH);

            Widgets.DrawBoxSolidWithOutline(dialogRect,
                new Color(0.15f, 0.15f, 0.15f, 0.97f),
                new Color(1f, 1f, 1f, 0.4f));
            dialogRect = dialogRect.ContractedBy(UiHelpers.ElementGap);

            Text.Font  = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(dialogRect.x, dialogRect.y, dialogRect.width, UiHelpers.ListRowHeight),
                Strings.SaveProfileNamePrompt);
            Text.Anchor = TextAnchor.UpperLeft;

            var inputRect = new Rect(dialogRect.x, dialogRect.y + UiHelpers.ListRowHeight + UiHelpers.ElementGap,
                dialogRect.width, UiHelpers.ListRowHeight);
            _saveProfileName = Widgets.TextField(inputRect, _saveProfileName);

            var btnY = inputRect.yMax + UiHelpers.ElementGap;
            var btnW = (dialogRect.width - UiHelpers.ButtonGap) / 2f;

            if (Widgets.ButtonText(new Rect(dialogRect.x, btnY, btnW, UiHelpers.ButtonHeight),
                    Strings.SaveProfileConfirm,
                    active: !_saveProfileName.NullOrEmpty()))
            {
                if (RolesProfileManager.SaveProfile(_saveProfileName))
                { _showSaveProfileDialog = false; }
            }
            if (Widgets.ButtonText(new Rect(dialogRect.x + btnW + UiHelpers.ButtonGap, btnY, btnW,
                    UiHelpers.ButtonHeight), Strings.CancelDataImport))
            {
                _showSaveProfileDialog = false;
            }
        }

        private float DoRoleSettings(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Widgets.Label(labelRect, Strings.RoleSettings);
            Text.Font = font;
            Text.Anchor = anchor;
            var priorityRect = LabelInput.DoLabeledRect(
                new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width, UiHelpers.ListRowHeight),
                Strings.PriorityLabel, Strings.PriorityTooltip);
            Widgets.HorizontalSlider(priorityRect, ref SelectedRole.Priority, new FloatRange(0, 10),
                $"{SelectedRole.Priority:N0}", 1f);
            var settingsRect = new Rect(rect.x, priorityRect.yMax + UiHelpers.ElementGap, rect.width,
                UiHelpers.ListRowHeight);
            var columnWidth = (settingsRect.width - (UiHelpers.ElementGap * (UiHelpers.BoolSettingsColumnCount - 1))) /
                UiHelpers.BoolSettingsColumnCount;
            for (var i = 1; i < UiHelpers.BoolSettingsColumnCount; i++)
            {
                UiHelpers.DoGapLineVertical(new Rect(
                    settingsRect.x + (i * (columnWidth + UiHelpers.ElementGap)) - UiHelpers.ElementGap, settingsRect.y,
                    UiHelpers.ElementGap, settingsRect.height));
            }
            var dropUnassignedWeaponsRect = UiHelpers.GetBoolSettingRect(settingsRect, 0, columnWidth);
            var checkboxRect = new Rect(dropUnassignedWeaponsRect.x, dropUnassignedWeaponsRect.y,
                dropUnassignedWeaponsRect.height, dropUnassignedWeaponsRect.height);
            Widgets.Checkbox(checkboxRect.x, checkboxRect.y, ref SelectedRole.DropUnassignedWeapons);
            var dropUnassignedWeaponsLabelRect = new Rect(checkboxRect.xMax + (UiHelpers.ElementGap / 2f),
                dropUnassignedWeaponsRect.y,
                dropUnassignedWeaponsRect.width - checkboxRect.width - (UiHelpers.ElementGap / 2f),
                dropUnassignedWeaponsRect.height);
            TooltipHandler.TipRegion(dropUnassignedWeaponsLabelRect, Strings.DropUnassignedWeaponsTooltip);
            anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(dropUnassignedWeaponsLabelRect, Strings.DropUnassignedWeapons);
            Text.Anchor = anchor;
            return dropUnassignedWeaponsRect.yMax - rect.yMin;
        }


        private float DoPawnCapacities(Rect rect)
        {
            var columnWidth = (rect.width - UiHelpers.ElementGap) / 2f;
            var weightsRect = new Rect(rect.x, rect.y, columnWidth, 1f);
            var gapRect = new Rect(weightsRect.xMax, rect.y, UiHelpers.ElementGap, 1f);
            var limitsRect = new Rect(gapRect.xMax, rect.y, columnWidth, 1f);
            gapRect.height = Math.Max(DoPawnCapacityWeights(weightsRect), DoPawnCapacityLimits(limitsRect));
            UiHelpers.DoGapLineVertical(gapRect);
            return gapRect.height;
        }

        private float DoPawnCapacityLimits(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width * 3f / 4f, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnCapacityLimits);
            Text.Font = GameFont.Small;
            var buttonRect = new Rect(labelRect.xMax + UiHelpers.ElementGap, rect.y,
                rect.width - labelRect.width - UiHelpers.ElementGap, labelRect.height);
            if (Widgets.ButtonText(buttonRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(DefDatabase<PawnCapacityDef>.AllDefs
                    .Where(def =>
                        def.showOnHumanlikes &&
                        SelectedRole.PawnCapacityLimits.All(pcl => pcl.PawnCapacityDefName != def.defName))
                    .OrderBy(def => def.label).Select(def => new FloatMenuOption(def.LabelCap,
                        () => SelectedRole.PawnCapacityLimits.Add(new PawnCapacityLimit(def.defName)))).ToList()));
            }
            var rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width, 1f);
            for (var i = 0; i < SelectedRole.PawnCapacityLimits.Count; i++)
            {
                var limit = SelectedRole.PawnCapacityLimits[i];
                rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap + (UiHelpers.ListRowHeight * i),
                    rect.width, UiHelpers.ListRowHeight).ContractedBy(4f);
                var deleteButtonRect = new Rect(rowRect.x, rowRect.y, rowRect.height, rowRect.height).ContractedBy(4f);
                if (Widgets.ButtonImageFitted(deleteButtonRect, Resources.Textures.Delete))
                {
                    _ = SelectedRole.PawnCapacityLimits.Remove(limit);
                    break;
                }
                var statLabelRect = new Rect(deleteButtonRect.xMax + (UiHelpers.ElementGap / 2f), rowRect.y,
                    (rowRect.width / 2f) - deleteButtonRect.width - (UiHelpers.ElementGap / 2f), rowRect.height);
                if (limit.PawnCapacityDef != null && !limit.PawnCapacityDef.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(statLabelRect, limit.PawnCapacityDef.description);
                }
                _ = Widgets.LabelFit(statLabelRect, limit.PawnCapacityDef?.LabelCap ?? limit.PawnCapacityDefName);
                var statInputRect = new Rect(statLabelRect.xMax + UiHelpers.ElementGap, rowRect.y,
                    rowRect.xMax - statLabelRect.xMax - UiHelpers.ElementGap, rowRect.height);
                var limitInputWidth = (statInputRect.width - (UiHelpers.ElementGap * 3)) / 2f;
                var minValueRect = new Rect(statInputRect.x, statInputRect.y, limitInputWidth, statInputRect.height);
                limit.MinValueBuffer = Widgets.TextField(minValueRect, limit.MinValueBuffer, 10);
                var dashRect = new Rect(minValueRect.xMax, statInputRect.y, UiHelpers.ElementGap * 3,
                    statInputRect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(dashRect, "-");
                Text.Anchor = TextAnchor.UpperLeft;
                var maxValueRect = new Rect(dashRect.xMax, statInputRect.y, limitInputWidth, statInputRect.height);
                limit.MaxValueBuffer = Widgets.TextField(maxValueRect, limit.MaxValueBuffer, 10);
            }
            Text.Font = font;
            Text.Anchor = anchor;
            return rowRect.yMax - rect.yMin;
        }

        private float DoPawnCapacityWeights(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width * 3f / 4f, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnCapacityWeights);
            Text.Font = GameFont.Small;
            var buttonRect = new Rect(labelRect.xMax + UiHelpers.ElementGap, rect.y,
                rect.width - labelRect.width - UiHelpers.ElementGap, labelRect.height);
            if (Widgets.ButtonText(buttonRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(DefDatabase<PawnCapacityDef>.AllDefs
                    .Where(def =>
                        def.showOnHumanlikes &&
                        SelectedRole.PawnCapacityWeights.All(pcw => pcw.PawnCapacityDefName != def.defName))
                    .OrderBy(def => def.label).Select(def => new FloatMenuOption(def.LabelCap,
                        () => SelectedRole.PawnCapacityWeights.Add(new PawnCapacityWeight(def.defName)))).ToList()));
            }
            var rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width, 1f);
            for (var i = 0; i < SelectedRole.PawnCapacityWeights.Count; i++)
            {
                var weight = SelectedRole.PawnCapacityWeights[i];
                rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap + (UiHelpers.ListRowHeight * i),
                    rect.width, UiHelpers.ListRowHeight).ContractedBy(4f);
                var deleteButtonRect = new Rect(rowRect.x, rowRect.y, rowRect.height, rowRect.height).ContractedBy(4f);
                if (Widgets.ButtonImageFitted(deleteButtonRect, Resources.Textures.Delete))
                {
                    _ = SelectedRole.PawnCapacityWeights.Remove(weight);
                    break;
                }
                var statLabelRect = new Rect(deleteButtonRect.xMax + (UiHelpers.ElementGap / 2f), rowRect.y,
                    (rowRect.width / 2f) - deleteButtonRect.width - (UiHelpers.ElementGap / 2f), rowRect.height);
                if (weight.PawnCapacityDef != null && !weight.PawnCapacityDef.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(statLabelRect, weight.PawnCapacityDef.description);
                }
                _ = Widgets.LabelFit(statLabelRect, weight.PawnCapacityDef?.LabelCap ?? weight.PawnCapacityDefName);
                var statInputRect = new Rect(statLabelRect.xMax + UiHelpers.ElementGap, rowRect.y,
                    rowRect.xMax - statLabelRect.xMax - UiHelpers.ElementGap, rowRect.height);
                Widgets.HorizontalSlider(statInputRect, ref weight.Weight,
                    new FloatRange(-1 * PawnCapacityWeight.WeightCap, PawnCapacityWeight.WeightCap),
                    $"{weight.Weight:N1}", 0.1f);
            }
            Text.Font = font;
            Text.Anchor = anchor;
            return rowRect.yMax - rect.yMin;
        }

        private float DoPawnPassions(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnPassions);
            var settingsRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width,
                UiHelpers.ListRowHeight);
            var index = 0;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            foreach (var passionLimit in SelectedRole.PassionLimits.Where(pl => pl.SkillDef != null))
            {
                var passionRect = GetPawnSettingRect(settingsRect, index);
                var deleteButtonRect = new Rect(passionRect.x, passionRect.y, passionRect.height, passionRect.height)
                    .ContractedBy(4f);
                if (Widgets.ButtonImageFitted(deleteButtonRect, Resources.Textures.Delete))
                {
                    _ = SelectedRole.PassionLimits.Remove(passionLimit);
                    break;
                }
                var passionIconRect = new Rect(deleteButtonRect.xMax + (UiHelpers.ElementGap / 2f), passionRect.y,
                    passionRect.height, passionRect.height).ContractedBy(4f);
                switch (passionLimit.Value)
                {
                    case PassionValue.None:
                        GUI.DrawTexture(passionIconRect, Widgets.CheckboxOffTex, ScaleMode.ScaleToFit);
                        if (Widgets.ButtonInvisible(passionRect))
                        {
                            SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                            passionLimit.Value = PassionValue.Minor;
                        }
                        break;
                    case PassionValue.Minor:
                        GUI.DrawTexture(passionIconRect, Resources.Textures.PassionMinor, ScaleMode.ScaleToFit);
                        if (Widgets.ButtonInvisible(passionRect))
                        {
                            SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                            passionLimit.Value = PassionValue.Major;
                        }
                        break;
                    case PassionValue.Major:
                        GUI.DrawTexture(passionIconRect, Resources.Textures.PassionMajor, ScaleMode.ScaleToFit);
                        if (Widgets.ButtonInvisible(passionRect))
                        {
                            SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                            passionLimit.Value = PassionValue.Any;
                        }
                        break;
                    case PassionValue.Any:
                        GUI.DrawTexture(
                            new Rect(passionIconRect.x, passionIconRect.y + (passionIconRect.height / 4f),
                                passionIconRect.width * 3f / 4f, passionIconRect.height * 3f / 4f).ContractedBy(2f),
                            Resources.Textures.PassionMinor, ScaleMode.ScaleToFit);
                        GUI.DrawTexture(
                            new Rect(passionIconRect.x + (passionIconRect.width / 4f), passionIconRect.y,
                                passionIconRect.width * 3 / 4f, passionIconRect.height * 3 / 4f).ContractedBy(2f),
                            Resources.Textures.PassionMajor, ScaleMode.ScaleToFit);
                        if (Widgets.ButtonInvisible(passionRect))
                        {
                            SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                            passionLimit.Value = PassionValue.None;
                        }
                        break;
                }
                var skillLabelRect = new Rect(passionIconRect.xMax + (UiHelpers.ElementGap / 2f), passionRect.y,
                    passionRect.width - passionIconRect.width - (UiHelpers.ElementGap / 2f), passionRect.height);
                if (!passionLimit.SkillDef.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(skillLabelRect, passionLimit.SkillDef.description);
                }
                Widgets.Label(skillLabelRect, passionLimit.SkillDef.skillLabel.CapitalizeFirst());
                index++;
            }
            var settingRect = GetPawnSettingRect(settingsRect, index);
            if (Widgets.ButtonText(settingRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(DefDatabase<SkillDef>.AllDefsListForReading
                    .Where(def => !SelectedRole.PassionLimits.Select(pl => pl.SkillDefName).Contains(def.defName))
                    .OrderBy(def => def.defName).Select(def =>
                        new FloatMenuOption(
                            def.skillLabel.NullOrEmpty() ? def.defName : def.skillLabel.CapitalizeFirst(),
                            () => SelectedRole.PassionLimits.Add(new PassionLimit(def.defName)))).ToList()));
            }
            Text.Font = font;
            Text.Anchor = anchor;
            return settingRect.yMax - rect.yMin;
        }

        private static void DoPawnSetting(Rect rect, bool value, Action<bool> setter, Action deleteAction, string label,
            string tooltip)
        {
            var deleteButtonRect = new Rect(rect.x, rect.y, rect.height, rect.height).ContractedBy(4f);
            if (Widgets.ButtonImageFitted(deleteButtonRect, Resources.Textures.Delete)) { deleteAction(); }
            var checkboxRect =
                new Rect(deleteButtonRect.xMax + (UiHelpers.ElementGap / 2f), rect.y, rect.height, rect.height)
                    .ContractedBy(4f);
            CheckBox.DoCheckboxWithCallback(checkboxRect, value, false, setter);
            var labelRect = new Rect(checkboxRect.xMax + (UiHelpers.ElementGap / 2f), rect.y,
                rect.width - checkboxRect.width - (UiHelpers.ElementGap / 2f), rect.height);
            if (!tooltip.NullOrEmpty()) { TooltipHandler.TipRegion(labelRect, tooltip); }
            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = anchor;
        }

        private float DoPawnSkillLimits(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width * 3f / 4f, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnSkillLimits);
            Text.Font = GameFont.Small;
            var buttonRect = new Rect(labelRect.xMax + UiHelpers.ElementGap, rect.y,
                rect.width - labelRect.width - UiHelpers.ElementGap, labelRect.height);
            if (Widgets.ButtonText(buttonRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(DefDatabase<SkillDef>.AllDefsListForReading
                    .Where(def => SelectedRole.SkillLimits.All(sl => sl.SkillDefName != def.defName)).Select(def =>
                        new FloatMenuOption(
                            def.skillLabel.NullOrEmpty() ? def.defName : def.skillLabel.CapitalizeFirst(),
                            () => SelectedRole.SkillLimits.Add(new SkillLimit(def.defName)))).ToList()));
            }
            var rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width, 1f);
            for (var i = 0; i < SelectedRole.SkillLimits.Count; i++)
            {
                var limit = SelectedRole.SkillLimits[i];
                rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap + (UiHelpers.ListRowHeight * i),
                    rect.width, UiHelpers.ListRowHeight).ContractedBy(4f);
                var deleteButtonRect = new Rect(rowRect.x, rowRect.y, rowRect.height, rowRect.height).ContractedBy(4f);
                if (Widgets.ButtonImageFitted(deleteButtonRect, Resources.Textures.Delete))
                {
                    _ = SelectedRole.SkillLimits.Remove(limit);
                    break;
                }
                var skillLabelRect = new Rect(deleteButtonRect.xMax + (UiHelpers.ElementGap / 2f), rowRect.y,
                    (rowRect.width / 2f) - deleteButtonRect.width - (UiHelpers.ElementGap / 2f), rowRect.height);
                if (limit.SkillDef != null && !limit.SkillDef.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(skillLabelRect, limit.SkillDef?.description);
                }
                _ = Widgets.LabelFit(skillLabelRect,
                    limit.SkillDef?.skillLabel.NullOrEmpty() ?? true
                        ? limit.SkillDefName
                        : limit.SkillDef.skillLabel.CapitalizeFirst());
                var skillInputRect = new Rect(skillLabelRect.xMax + UiHelpers.ElementGap, rowRect.y,
                    rowRect.xMax - skillLabelRect.xMax - UiHelpers.ElementGap, rowRect.height);
                var limitInputWidth = (skillInputRect.width - (UiHelpers.ElementGap * 3)) / 2f;
                var minValueRect = new Rect(skillInputRect.x, skillInputRect.y, limitInputWidth, skillInputRect.height);
                limit.MinValueBuffer = Widgets.TextField(minValueRect, limit.MinValueBuffer, 10);
                var dashRect = new Rect(minValueRect.xMax, skillInputRect.y, UiHelpers.ElementGap * 3,
                    skillInputRect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(dashRect, "-");
                Text.Anchor = TextAnchor.UpperLeft;
                var maxValueRect = new Rect(dashRect.xMax, skillInputRect.y, limitInputWidth, skillInputRect.height);
                limit.MaxValueBuffer = Widgets.TextField(maxValueRect, limit.MaxValueBuffer, 10);
            }
            Text.Font = font;
            Text.Anchor = anchor;
            return rowRect.yMax - rect.yMin;
        }

        private float DoPawnSkills(Rect rect)
        {
            var columnWidth = (rect.width - UiHelpers.ElementGap) / 2f;
            var weightsRect = new Rect(rect.x, rect.y, columnWidth, 1f);
            var gapRect = new Rect(weightsRect.xMax, rect.y, UiHelpers.ElementGap, 1f);
            var limitsRect = new Rect(gapRect.xMax, rect.y, columnWidth, 1f);
            gapRect.height = Math.Max(DoPawnSkillWeights(weightsRect), DoPawnSkillLimits(limitsRect));
            UiHelpers.DoGapLineVertical(gapRect);
            return gapRect.height;
        }

        private float DoPawnSkillWeights(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width * 3f / 4f, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnSkillWeights);
            Text.Font = GameFont.Small;
            var buttonRect = new Rect(labelRect.xMax + UiHelpers.ElementGap, rect.y,
                rect.width - labelRect.width - UiHelpers.ElementGap, labelRect.height);
            if (Widgets.ButtonText(buttonRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(DefDatabase<SkillDef>.AllDefsListForReading
                    .Where(def => SelectedRole.SkillWeights.All(sw => sw.SkillDefName != def.defName)).Select(def =>
                        new FloatMenuOption(
                            def.skillLabel.NullOrEmpty() ? def.defName : def.skillLabel.CapitalizeFirst(),
                            () => SelectedRole.SkillWeights.Add(new SkillWeight(def.defName)))).ToList()));
            }
            var rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width, 1f);
            for (var i = 0; i < SelectedRole.SkillWeights.Count; i++)
            {
                var weight = SelectedRole.SkillWeights[i];
                rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap + (UiHelpers.ListRowHeight * i),
                    rect.width, UiHelpers.ListRowHeight).ContractedBy(4f);
                var deleteButtonRect = new Rect(rowRect.x, rowRect.y, rowRect.height, rowRect.height).ContractedBy(4f);
                if (Widgets.ButtonImageFitted(deleteButtonRect, Resources.Textures.Delete))
                {
                    _ = SelectedRole.SkillWeights.Remove(weight);
                    break;
                }
                var skillLabelRect = new Rect(deleteButtonRect.xMax + (UiHelpers.ElementGap / 2f), rowRect.y,
                    (rowRect.width / 2f) - deleteButtonRect.width - (UiHelpers.ElementGap / 2f), rowRect.height);
                if (weight.SkillDef != null && !weight.SkillDef.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(skillLabelRect, weight.SkillDef?.description);
                }
                _ = Widgets.LabelFit(skillLabelRect, weight.SkillDef?.LabelCap ?? weight.SkillDefName);
                var skillInputRect = new Rect(skillLabelRect.xMax + UiHelpers.ElementGap, rowRect.y,
                    rowRect.xMax - skillLabelRect.xMax - UiHelpers.ElementGap, rowRect.height);
                Widgets.HorizontalSlider(skillInputRect, ref weight.Weight,
                    new FloatRange(-1 * SkillWeight.WeightCap, SkillWeight.WeightCap), $"{weight.Weight:N1}", 0.1f);
            }
            Text.Font = font;
            Text.Anchor = anchor;
            return rowRect.yMax - rect.yMin;
        }

        private float DoPawnStatLimits(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width * 3f / 4f, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnStatLimits);
            Text.Font = GameFont.Small;
            var buttonRect = new Rect(labelRect.xMax + UiHelpers.ElementGap, rect.y,
                rect.width - labelRect.width - UiHelpers.ElementGap, labelRect.height);
            if (Widgets.ButtonText(buttonRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(StatHelper.DefaultPawnStatDefs
                    .Where(def => SelectedRole.StatLimits.All(sl => sl.StatDefName != def.defName)).Select(def =>
                        new FloatMenuOption($"{def.LabelCap} [{def.category?.LabelCap ?? "No category"}]",
                            () => SelectedRole.StatLimits.Add(new StatLimit(def.defName)))).ToList()));
            }
            var rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width, 1f);
            for (var i = 0; i < SelectedRole.StatLimits.Count; i++)
            {
                var limit = SelectedRole.StatLimits[i];
                rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap + (UiHelpers.ListRowHeight * i),
                    rect.width, UiHelpers.ListRowHeight).ContractedBy(4f);
                var deleteButtonRect = new Rect(rowRect.x, rowRect.y, rowRect.height, rowRect.height).ContractedBy(4f);
                if (Widgets.ButtonImageFitted(deleteButtonRect, Resources.Textures.Delete))
                {
                    _ = SelectedRole.StatLimits.Remove(limit);
                    break;
                }
                var statLabelRect = new Rect(deleteButtonRect.xMax + (UiHelpers.ElementGap / 2f), rowRect.y,
                    (rowRect.width / 2f) - deleteButtonRect.width - (UiHelpers.ElementGap / 2f), rowRect.height);
                if (limit.StatDef != null && !limit.StatDef.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(statLabelRect, limit.StatDef?.description);
                }
                _ = Widgets.LabelFit(statLabelRect, limit.StatDef?.LabelCap ?? limit.StatDefName);
                var statInputRect = new Rect(statLabelRect.xMax + UiHelpers.ElementGap, rowRect.y,
                    rowRect.xMax - statLabelRect.xMax - UiHelpers.ElementGap, rowRect.height);
                var limitInputWidth = (statInputRect.width - (UiHelpers.ElementGap * 3)) / 2f;
                var minValueRect = new Rect(statInputRect.x, statInputRect.y, limitInputWidth, statInputRect.height);
                limit.MinValueBuffer = Widgets.TextField(minValueRect, limit.MinValueBuffer, 10);
                var dashRect = new Rect(minValueRect.xMax, statInputRect.y, UiHelpers.ElementGap * 3,
                    statInputRect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(dashRect, "-");
                Text.Anchor = TextAnchor.UpperLeft;
                var maxValueRect = new Rect(dashRect.xMax, statInputRect.y, limitInputWidth, statInputRect.height);
                limit.MaxValueBuffer = Widgets.TextField(maxValueRect, limit.MaxValueBuffer, 10);
            }
            Text.Font = font;
            Text.Anchor = anchor;
            return rowRect.yMax - rect.yMin;
        }

        private float DoPawnStats(Rect rect)
        {
            var columnWidth = (rect.width - UiHelpers.ElementGap) / 2f;
            var weightsRect = new Rect(rect.x, rect.y, columnWidth, 1f);
            var gapRect = new Rect(weightsRect.xMax, rect.y, UiHelpers.ElementGap, 1f);
            var limitsRect = new Rect(gapRect.xMax, rect.y, columnWidth, 1f);
            gapRect.height = Math.Max(DoPawnStatWeights(weightsRect), DoPawnStatLimits(limitsRect));
            UiHelpers.DoGapLineVertical(gapRect);
            return gapRect.height;
        }

        private float DoPawnStatWeights(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width * 3f / 4f, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnStatWeights);
            Text.Font = GameFont.Small;
            var buttonRect = new Rect(labelRect.xMax + UiHelpers.ElementGap, rect.y,
                rect.width - labelRect.width - UiHelpers.ElementGap, labelRect.height);
            if (Widgets.ButtonText(buttonRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(StatHelper.DefaultPawnStatDefs
                    .Where(def => SelectedRole.StatWeights.All(sw => sw.StatDefName != def.defName)).Select(def =>
                        new FloatMenuOption($"{def.LabelCap} [{def.category?.LabelCap ?? "No category"}]",
                            () => SelectedRole.StatWeights.Add(new StatWeight(def.defName, false)))).ToList()));
            }
            var rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width, 1f);
            for (var i = 0; i < SelectedRole.StatWeights.Count; i++)
            {
                var weight = SelectedRole.StatWeights[i];
                rowRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap + (UiHelpers.ListRowHeight * i),
                    rect.width, UiHelpers.ListRowHeight).ContractedBy(4f);
                var deleteButtonRect = new Rect(rowRect.x, rowRect.y, rowRect.height, rowRect.height).ContractedBy(4f);
                if (!weight.Protected)
                {
                    if (Widgets.ButtonImageFitted(deleteButtonRect, Resources.Textures.Delete))
                    {
                        _ = SelectedRole.StatWeights.Remove(weight);
                        break;
                    }
                }
                var statLabelRect = new Rect(deleteButtonRect.xMax + (UiHelpers.ElementGap / 2f), rowRect.y,
                    (rowRect.width / 2f) - deleteButtonRect.width - (UiHelpers.ElementGap / 2f), rowRect.height);
                if (weight.StatDef != null && !weight.StatDef.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(statLabelRect, weight.StatDef?.description);
                }
                _ = Widgets.LabelFit(statLabelRect, weight.StatDef?.LabelCap ?? weight.StatDefName);
                var statInputRect = new Rect(statLabelRect.xMax + UiHelpers.ElementGap, rowRect.y,
                    rowRect.xMax - statLabelRect.xMax - UiHelpers.ElementGap, rowRect.height);
                Widgets.HorizontalSlider(statInputRect, ref weight.Weight,
                    new FloatRange(-1 * StatWeight.WeightCap, StatWeight.WeightCap), $"{weight.Weight:N1}", 0.1f);
            }
            Text.Font = font;
            Text.Anchor = anchor;
            return rowRect.yMax - rect.yMin;
        }

        private float DoPawnTraits(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnTraits);
            Text.Font = font;
            Text.Anchor = anchor;
            var settingsRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width,
                UiHelpers.ListRowHeight);
            var index = 0;
            foreach (var pawnTrait in SelectedRole.PawnTraits.ToList())
            {
                var traitRect = GetPawnSettingRect(settingsRect, index);
                var traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(pawnTrait.Key);
                string label;
                string description;
                if (traitDef == null) { label = description = pawnTrait.Key; }
                else
                {
                    label = traitDef.label.CapitalizeFirst();
                    if (label.NullOrEmpty()) { label = pawnTrait.Key; }
                    description = traitDef.description;
                    if (description.NullOrEmpty()) { description = pawnTrait.Key; }
                }
                DoPawnSetting(traitRect, pawnTrait.Value, value => SelectedRole.PawnTraits[pawnTrait.Key] = value,
                    () => _ = SelectedRole.PawnTraits.Remove(pawnTrait.Key), label, description);
                index++;
            }
            var settingRect = GetPawnSettingRect(settingsRect, index);
            if (Widgets.ButtonText(settingRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(DefDatabase<TraitDef>.AllDefsListForReading
                    .Where(traitDef => !SelectedRole.PawnTraits.ContainsKey(traitDef.defName))
                    .OrderBy(traitDef => traitDef.defName).Select(traitDef =>
                        new FloatMenuOption(
                            traitDef.label.NullOrEmpty() ? traitDef.defName : traitDef.label.CapitalizeFirst(),
                            () => SelectedRole.PawnTraits[traitDef.defName] = true)).ToList()));
            }
            return settingRect.yMax - rect.yMin;
        }

        private float DoPawnWorkCapacities(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Widgets.Label(labelRect, Strings.PawnWorkCapacities);
            Text.Font = font;
            Text.Anchor = anchor;
            var settingsRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width,
                UiHelpers.ListRowHeight);
            var index = 0;
            foreach (var pawnCapacity in SelectedRole.PawnWorkCapacities.ToList())
            {
                var tagRect = GetPawnSettingRect(settingsRect, index);
                var label = Enum.TryParse<WorkTags>(pawnCapacity.Key, out var tag)
                    ? tag.LabelTranslated().CapitalizeFirst()
                    : pawnCapacity.Key;
                DoPawnSetting(tagRect, pawnCapacity.Value,
                    value => SelectedRole.PawnWorkCapacities[pawnCapacity.Key] = value,
                    () => _ = SelectedRole.PawnWorkCapacities.Remove(pawnCapacity.Key), label, null);
                index++;
            }
            var settingRect = GetPawnSettingRect(settingsRect, index);
            if (Widgets.ButtonText(settingRect, Resources.Strings.Add))
            {
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(WorkTags)).OfType<WorkTags>()
                    .Where(tag => !SelectedRole.PawnWorkCapacities.ContainsKey(tag.ToString()))
                    .OrderBy(tag => tag.LabelTranslated().CapitalizeFirst()).Select(tag =>
                        new FloatMenuOption(tag.LabelTranslated().CapitalizeFirst(),
                            () => SelectedRole.PawnWorkCapacities[tag.ToString()] = true)).ToList()));
            }
            return settingRect.yMax - rect.yMin;
        }

        private void DoPrimaryWeaponRule(Rect rect)
        {
            var inputRect = LabelInput.DoLabeledRect(rect, Strings.PrimaryWeaponLabel);
            var inputWidth = (inputRect.width - UiHelpers.ElementGap) / 2f;
            var typeRect = new Rect(inputRect.x, inputRect.y, inputWidth, inputRect.height);
            var ruleRect = new Rect(inputRect.x + inputWidth + UiHelpers.ElementGap, inputRect.y, inputWidth,
                inputRect.height);
            if (Widgets.ButtonText(typeRect, Strings.GetPrimaryWeaponTypeLabel(SelectedRole.PrimaryRuleType)))
            {
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(Role.PrimaryWeaponType))
                    .OfType<Role.PrimaryWeaponType>().Select(pwt =>
                        new FloatMenuOption(Strings.GetPrimaryWeaponTypeLabel(pwt),
                            () => SelectedRole.PrimaryRuleType = pwt)).ToList()));
            }
            switch (SelectedRole.PrimaryRuleType)
            {
                case Role.PrimaryWeaponType.None:
                    break;
                case Role.PrimaryWeaponType.RangedWeapon:
                    if (Widgets.ButtonText(ruleRect,
                            SelectedRole.PrimaryRangedWeaponRuleId == null
                                ? Resources.Strings.WeaponRules.NoRuleSelected
                                : EquipmentManager.GetRangedWeaponRule((int) SelectedRole.PrimaryRangedWeaponRuleId)
                                    .Label))
                    {
                        Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetRangedWeaponRules().Select(rule =>
                                new FloatMenuOption(rule.Label,
                                    () => SelectedRole.PrimaryRangedWeaponRuleId = rule.Id))
                            .ToList()));
                    }
                    break;
                case Role.PrimaryWeaponType.MeleeWeapon:
                    if (Widgets.ButtonText(ruleRect,
                            SelectedRole.PrimaryMeleeWeaponRuleId == null
                                ? Resources.Strings.WeaponRules.NoRuleSelected
                                : EquipmentManager.GetMeleeWeaponRule((int) SelectedRole.PrimaryMeleeWeaponRuleId)
                                    .Label))
                    {
                        Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetMeleeWeaponRules().Select(rule =>
                                new FloatMenuOption(rule.Label,
                                    () => SelectedRole.PrimaryMeleeWeaponRuleId = rule.Id))
                            .ToList()));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private void DoSecondaryWeaponRule(Rect rect)
        {
            var inputRect = LabelInput.DoLabeledRect(rect, Strings.SecondaryWeaponLabel);
            var inputWidth = (inputRect.width - UiHelpers.ElementGap) / 2f;
            var typeRect = new Rect(inputRect.x, inputRect.y, inputWidth, inputRect.height);
            var ruleRect = new Rect(inputRect.x + inputWidth + UiHelpers.ElementGap, inputRect.y, inputWidth,
                inputRect.height);
            if (Widgets.ButtonText(typeRect, Strings.GetSecondaryWeaponTypeLabel(SelectedRole.SecondaryRuleType)))
            {
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(Role.PrimaryWeaponType))
                    .OfType<Role.PrimaryWeaponType>().Select(pwt =>
                        new FloatMenuOption(Strings.GetSecondaryWeaponTypeLabel(pwt),
                            () => SelectedRole.SecondaryRuleType = pwt)).ToList()));
            }
            switch (SelectedRole.SecondaryRuleType)
            {
                case Role.PrimaryWeaponType.None:
                    break;
                case Role.PrimaryWeaponType.RangedWeapon:
                    if (Widgets.ButtonText(ruleRect,
                            SelectedRole.SecondaryRangedWeaponRuleId == null
                                ? Resources.Strings.WeaponRules.NoRuleSelected
                                : EquipmentManager.GetRangedWeaponRule((int) SelectedRole.SecondaryRangedWeaponRuleId)
                                    .Label))
                    {
                        Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetRangedWeaponRules().Select(rule =>
                                new FloatMenuOption(rule.Label,
                                    () => SelectedRole.SecondaryRangedWeaponRuleId = rule.Id))
                            .ToList()));
                    }
                    break;
                case Role.PrimaryWeaponType.MeleeWeapon:
                    if (Widgets.ButtonText(ruleRect,
                            SelectedRole.SecondaryMeleeWeaponRuleId == null
                                ? Resources.Strings.WeaponRules.NoRuleSelected
                                : EquipmentManager.GetMeleeWeaponRule((int) SelectedRole.SecondaryMeleeWeaponRuleId)
                                    .Label))
                    {
                        Find.WindowStack.Add(new FloatMenu(EquipmentManager.GetMeleeWeaponRules().Select(rule =>
                                new FloatMenuOption(rule.Label,
                                    () => SelectedRole.SecondaryMeleeWeaponRuleId = rule.Id))
                            .ToList()));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private float DoRules(Rect rect)
        {
            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Widgets.Label(labelRect, Strings.Rules);
            Text.Font = font;
            Text.Anchor = anchor;
            var primaryWeaponRect = new Rect(rect.x, labelRect.yMax + UiHelpers.ElementGap, rect.width,
                UiHelpers.ListRowHeight);
            DoPrimaryWeaponRule(primaryWeaponRect);
            var secondaryWeaponRect = new Rect(rect.x, primaryWeaponRect.yMax + UiHelpers.ElementGap, rect.width,
                UiHelpers.ListRowHeight);
            DoSecondaryWeaponRule(secondaryWeaponRect);
            var toolRect = new Rect(rect.x, secondaryWeaponRect.yMax + UiHelpers.ElementGap, rect.width,
                UiHelpers.ListRowHeight);
            DoToolRule();
            return toolRect.yMax - rect.yMin;
        }

        private void DoToolRule()
        {
            // Оставлено пустым намеренно: строка под инструменты убрана из UI,
            // но высота rect сохраняет вертикальный отступ для остального интерфейса.
        }

        public override void DoWindowContents(Rect inRect)
        {
            var sectionHeaderHeight = Text.LineHeightOf(GameFont.Medium) + UiHelpers.ElementGap;
            var buttonRowRect = new Rect(inRect.x + UiHelpers.ButtonGap, inRect.y,
                inRect.width - (UiHelpers.ButtonGap * 2), UiHelpers.ButtonHeight);
            var labelRect = new Rect(inRect.x, buttonRowRect.yMax + UiHelpers.ElementGap, inRect.width,
                UiHelpers.LabelHeight);
            DoButtonRow(buttonRowRect);
            UiHelpers.DoGapLineHorizontal(new Rect(inRect.x, buttonRowRect.yMax, inRect.width, UiHelpers.ElementGap));
            if (SelectedRole == null) { LabelInput.DoLabelWithoutInput(labelRect, Strings.NoRoleSelected); }
            else
            {
                LabelInput.DoLabelInput(labelRect, Strings.RoleLabel, ref SelectedRole.Label);
                UiHelpers.DoGapLineHorizontal(new Rect(inRect.x, labelRect.yMax, inRect.width, UiHelpers.ElementGap));
                var availablePawnsHeight = sectionHeaderHeight + (AvailablePawnsRowHeight * AvailablePawnsRowCount) +
                    (UiHelpers.ElementGap * (AvailablePawnsRowCount - 1));
                var availablePawnsRect = new Rect(inRect.x, inRect.yMax - availablePawnsHeight, inRect.width,
                    availablePawnsHeight);
                var outerRect = new Rect(inRect.x, labelRect.yMax + UiHelpers.ElementGap, inRect.width,
                    availablePawnsRect.y - UiHelpers.ElementGap - (labelRect.yMax + UiHelpers.ElementGap));
                var scrollViewRect = new Rect(outerRect.x, outerRect.y,
                    outerRect.width - GUI.skin.verticalScrollbar.fixedWidth - 4f, _scrollViewHeight);
                var y = 0f;
                Widgets.BeginScrollView(outerRect, ref _scrollPosition, scrollViewRect);
                y += DoRoleSettings(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width, 1f));
                UiHelpers.DoGapLineHorizontal(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width,
                    UiHelpers.ElementGap));
                y += UiHelpers.ElementGap;
                y += DoRules(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width, 1f));
                UiHelpers.DoGapLineHorizontal(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width,
                    UiHelpers.ElementGap));
                y += UiHelpers.ElementGap;
                y += DoPawnTraits(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width, 1f));
                UiHelpers.DoGapLineHorizontal(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width,
                    UiHelpers.ElementGap));
                y += UiHelpers.ElementGap;
                y += DoPawnCapacities(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width, 1f));
                UiHelpers.DoGapLineHorizontal(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width,
                    UiHelpers.ElementGap));
                y += UiHelpers.ElementGap;
                y += DoPawnWorkCapacities(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width, 1f));
                UiHelpers.DoGapLineHorizontal(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width,
                    UiHelpers.ElementGap));
                y += UiHelpers.ElementGap;
                y += DoPawnSkills(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width, 1f));
                UiHelpers.DoGapLineHorizontal(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width,
                    UiHelpers.ElementGap));
                y += UiHelpers.ElementGap;
                y += DoPawnPassions(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width, 1f));
                UiHelpers.DoGapLineHorizontal(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width,
                    UiHelpers.ElementGap));
                y += UiHelpers.ElementGap;
                y += DoPawnStats(new Rect(scrollViewRect.x, scrollViewRect.y + y, scrollViewRect.width, 1f));
                if (Event.current.type == EventType.Layout) { _scrollViewHeight = y; }
                Widgets.EndScrollView();
                UiHelpers.DoGapLineHorizontal(new Rect(inRect.x, outerRect.yMax, inRect.width, UiHelpers.ElementGap));
                DoAvailablePawns(availablePawnsRect);
            }
			DoSaveProfileDialog(inRect);
        }

        private Rect GetLabeledButtonListItemRect(Rect rect, int index)
        {
            var rowIndex = Math.DivRem(index, LabeledButtonListColumnCount, out var columnIndex);
            var columnWidth =
                ((-1 * LabeledButtonListColumnCount * UiHelpers.ElementGap) + UiHelpers.ElementGap + rect.width) /
                LabeledButtonListColumnCount;
            return new Rect(rect.x + ((columnWidth + UiHelpers.ElementGap) * columnIndex),
                rect.y + ((UiHelpers.ButtonHeight + UiHelpers.ButtonGap) * rowIndex), columnWidth,
                UiHelpers.ButtonHeight);
        }

        private Rect GetPawnSettingRect(Rect rect, int index)
        {
            var rowIndex = Math.DivRem(index, PawnSettingsColumnCount, out var columnIndex);
            var columnWidth =
                ((-1 * PawnSettingsColumnCount * UiHelpers.ElementGap) + UiHelpers.ElementGap + rect.width) /
                PawnSettingsColumnCount;
            return new Rect(rect.x + ((columnWidth + UiHelpers.ElementGap) * columnIndex),
                rect.y + ((UiHelpers.ListRowHeight + UiHelpers.ElementGap) * rowIndex), columnWidth,
                UiHelpers.ListRowHeight);
        }

        public override void PreClose()
        {
            base.PreClose();
            CheckSelectedRoleHasName();
        }

        private static void ResetScrollPositions()
        {
            _scrollPosition.Set(0f, 0f);
            _availablePawnsScrollPosition.Set(0f, 0f);
        }
    }

    internal class SaveProfileDialog : Window
    {
        private readonly Action<string> _onConfirm;
        private string _name = string.Empty;

        public SaveProfileDialog(Action<string> onConfirm)
        {
            _onConfirm = onConfirm;
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true; // ← блокирует клики снаружи
        }

        public override Vector2 InitialSize => new(420f, 150f);

        public override void DoWindowContents(Rect inRect)
        {
            var labelRect = new Rect(inRect.x, inRect.y, inRect.width, UiHelpers.ListRowHeight);
            Widgets.Label(labelRect, Strings.SaveProfileNamePrompt);

            var inputRect = new Rect(inRect.x, labelRect.yMax + UiHelpers.ElementGap,
                inRect.width, UiHelpers.ListRowHeight);
            _name = Widgets.TextField(inputRect, _name);

            var btnY = inputRect.yMax + UiHelpers.ElementGap;
            var btnW = (inRect.width - UiHelpers.ButtonGap) / 2f;

            if (Widgets.ButtonText(new Rect(inRect.x, btnY, btnW, UiHelpers.ButtonHeight),
                    Strings.SaveProfileConfirm, active: !_name.NullOrEmpty()))
            {
                _onConfirm(_name);
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.x + btnW + UiHelpers.ButtonGap, btnY, btnW,
                    UiHelpers.ButtonHeight), Strings.CancelDataImport))
            {
                Close();
            }
        }
    }
}

