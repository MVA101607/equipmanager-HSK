using System;
using System.Collections.Generic;
using System.Linq;
using EquipmentManager.CustomWidgets;
using UnityEngine;
using Verse;

namespace EquipmentManager.Windows
{
    internal class ImportRolesDialog : Window
    {
        private readonly List<Role> _roles = new();
        private readonly List<MeleeWeaponRule> _meleeWeaponRules = new();
        private readonly List<RangedWeaponRule> _rangedWeaponRules = new();
        private readonly Dictionary<string, string> _profiles = new();
        private readonly List<ToolRule> _toolRules = new();
        private readonly List<WorkTypeRule> _workTypeRules = new();
        private Vector2 _rolesListScrollPosition;
        private Vector2 _profilesListScrollPosition;
        private string _selectedProfile;

        public ImportRolesDialog()
        {
            forcePause = true;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        public override Vector2 InitialSize =>
            UiHelpers.GetWindowSize(new Vector2(850f, 500f), new Vector2(1000f, 500f));

        private void DoButtonRow(Rect rect)
        {
            var importButtonRect = new Rect(rect.center.x - UiHelpers.ActionButtonWidth - UiHelpers.ButtonGap, rect.y,
                UiHelpers.ActionButtonWidth, UiHelpers.ButtonHeight);
            if (Widgets.ButtonText(importButtonRect, Resources.Strings.Roles.ImportData,
                    active: _selectedProfile != null && _roles.Any()))
            {
                ImportSaveGameData();
                Close();
            }
            var cancelImportButtonRect = new Rect(rect.center.x + UiHelpers.ButtonGap, rect.y,
                UiHelpers.ActionButtonWidth, UiHelpers.ButtonHeight);
            if (Widgets.ButtonText(cancelImportButtonRect, Resources.Strings.Roles.CancelDataImport)) { Close(); }
        }

        private void DoRoleList(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, Text.LineHeight),
                Resources.Strings.Roles.RoleListHeader);
            Text.Font = GameFont.Small;
            var listingRect = new Rect(rect.x, rect.y + Text.LineHeightOf(GameFont.Medium) + UiHelpers.ElementGap,
                rect.width, rect.height - Text.LineHeightOf(GameFont.Medium) - (UiHelpers.ElementGap * 2));
            Widgets.DrawBoxSolidWithOutline(listingRect, new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.4f));
            var listing = new Listing_Standard(listingRect, () => _rolesListScrollPosition);
            var viewRect = new Rect(rect.x, rect.y,
                rect.width - GUI.skin.verticalScrollbar.fixedWidth - UiHelpers.ElementGap,
                _roles.Count * UiHelpers.ListRowHeight);
            Widgets.BeginScrollView(listingRect, ref _rolesListScrollPosition, viewRect);
            listing.Begin(viewRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            foreach (var loadout in _roles)
            {
                Widgets.Label(listing.GetRect(UiHelpers.ListRowHeight).ContractedBy(4f), loadout.Label);
            }
            Text.Anchor = TextAnchor.UpperLeft;
            listing.End();
            Widgets.EndScrollView();
        }

        private void DoProfilesList(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, Text.LineHeight),
                Resources.Strings.Roles.ProfilesListHeader);
            Text.Font = GameFont.Small;
            var listingRect = new Rect(rect.x, rect.y + Text.LineHeightOf(GameFont.Medium) + UiHelpers.ElementGap,
                rect.width, rect.height - Text.LineHeightOf(GameFont.Medium) - (UiHelpers.ElementGap * 2));
            Widgets.DrawBoxSolidWithOutline(listingRect, new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.4f));
            var listing = new Listing_Standard(listingRect, () => _profilesListScrollPosition);
            var viewRect = new Rect(rect.x, rect.y,
                rect.width - GUI.skin.verticalScrollbar.fixedWidth - UiHelpers.ElementGap,
                _profiles.Count * UiHelpers.ListRowHeight * 1.5f);
            Widgets.BeginScrollView(listingRect, ref _profilesListScrollPosition, viewRect);
            listing.Begin(viewRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            foreach (var profile in _profiles)
            {
                var rowRect = listing.GetRect(UiHelpers.ListRowHeight * 1.5f);
                var toggleButtonRect = new Rect(rowRect.x,
                    rowRect.y + (((UiHelpers.ListRowHeight * 1.5f) - Math.Min(32f, UiHelpers.ListRowHeight)) / 2f),
                    Math.Min(32f, UiHelpers.ListRowHeight), Math.Min(32f, UiHelpers.ListRowHeight)).ContractedBy(4f);
                ButtonImageToggle.DoButtonImageToggle(() => profile.Key == _selectedProfile, newValue =>
                {
                    if (!newValue) { return; }
                    _selectedProfile = profile.Key;
                    LoadProfileIntoBuffers(profile.Value);
                }, toggleButtonRect, Widgets.CheckboxOnTex, Widgets.CheckboxOffTex);
                var nameRectX = toggleButtonRect.x + toggleButtonRect.width + 4f;
                Widgets.Label(new Rect(nameRectX, rowRect.y, rowRect.xMax - nameRectX, rowRect.height).ContractedBy(4f),
                    profile.Key);
            }
            Text.Anchor = TextAnchor.UpperLeft;
            listing.End();
            Widgets.EndScrollView();
        }

        public override void DoWindowContents(Rect inRect)
        {
            const int columnCount = 2;
            var columnWidth = (inRect.width - (UiHelpers.ElementGap * 2) - (UiHelpers.ElementGap * (columnCount - 1))) /
                columnCount;
            var columnHeight = inRect.height - (UiHelpers.ElementGap * 2) -
                (UiHelpers.ButtonHeight + (UiHelpers.ButtonGap * 2));
            var savedGamesRect = new Rect(inRect.x + UiHelpers.ElementGap, inRect.y + UiHelpers.ElementGap, columnWidth,
                columnHeight);
            DoProfilesList(savedGamesRect);
            var rolesRect = new Rect(savedGamesRect.xMax + UiHelpers.ElementGap, inRect.y + UiHelpers.ElementGap,
                columnWidth, columnHeight);
            DoRoleList(rolesRect);
            var actionButtonsRect = new Rect(inRect.x, inRect.yMax - UiHelpers.ButtonHeight - UiHelpers.ButtonGap,
                inRect.width, UiHelpers.ButtonHeight);
            DoButtonRow(actionButtonsRect);
        }

        private void ImportSaveGameData()
        {
            Find.WindowStack.WindowOfType<ManageWeaponRulesDialog>()?.Close();
            Find.WindowStack.WindowOfType<ManageRolesDialog>()?.Close();
            foreach (var loadout in EquipmentManager.GetRoles().ToList())
            {
                EquipmentManager.DeleteRole(loadout);
            }
            foreach (var rule in EquipmentManager.GetMeleeWeaponRules().ToList())
            {
                EquipmentManager.DeleteMeleeWeaponRule(rule);
            }
            foreach (var rule in _meleeWeaponRules) { EquipmentManager.AddMeleeWeaponRule(rule); }
            foreach (var rule in EquipmentManager.GetRangedWeaponRules().ToList())
            {
                EquipmentManager.DeleteRangedWeaponRule(rule);
            }
            foreach (var rule in _rangedWeaponRules) { EquipmentManager.AddRangedWeaponRule(rule); }
            foreach (var rule in EquipmentManager.GetToolRules().ToList()) { EquipmentManager.DeleteToolRule(rule); }
            foreach (var rule in _toolRules) { EquipmentManager.AddToolRule(rule); }
            foreach (var rule in EquipmentManager.GetWorkTypeRules().ToList())
            {
                EquipmentManager.DeleteWorkTypeRule(rule);
            }
            foreach (var rule in _workTypeRules) { EquipmentManager.AddWorkTypeRule(rule); }
            foreach (var loadout in _roles) { EquipmentManager.AddRole(loadout); }
        }

        private void LoadProfiles()
        {
            _profiles.Clear();
            foreach (var kv in RolesProfileManager.GetProfiles())
            {
                _profiles[kv.Key] = kv.Value;
            }
        }

        public override void PostOpen()
        {
            base.PostOpen();
            LoadProfiles();
        }

        private void LoadProfileIntoBuffers(string profilePath)
        {
            _rolesListScrollPosition = Vector2.zero;
            _roles.Clear();
            _meleeWeaponRules.Clear();
            _rangedWeaponRules.Clear();
            _toolRules.Clear();
            _workTypeRules.Clear();
            // Сначала пытаемся прочесть как профиль (.xml формат RolesProfileManager).
            // Если корневой узел не найден, fallback'имся на чтение сейва игры.
            var data = ProfileXmlReader.ReadProfile(profilePath);
            if (data.Roles.Count == 0 && data.WorkTypeRules.Count == 0 && data.ToolRules.Count == 0 &&
                data.MeleeWeaponRules.Count == 0 && data.RangedWeaponRules.Count == 0)
            {
                try { data = ProfileXmlReader.ReadSaveGame(profilePath); }
                catch (Exception ex)
                {
                    Log.Warning(
                        $"Equipment Manager: Could not parse {profilePath} as profile or save game: {ex.Message}");
                }
            }
            _workTypeRules.AddRange(data.WorkTypeRules);
            _toolRules.AddRange(data.ToolRules);
            _meleeWeaponRules.AddRange(data.MeleeWeaponRules);
            _rangedWeaponRules.AddRange(data.RangedWeaponRules);
            _roles.AddRange(data.Roles);
        }
    }
}
