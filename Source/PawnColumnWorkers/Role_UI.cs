using System;
using System.Collections.Generic;
using System.Linq;
using EquipmentManager.Windows;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace EquipmentManager.PawnColumnWorkers
{
    [UsedImplicitly]
    internal class Role_UI : PawnColumnWorker
    {
        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        // ── Вспомогательные методы для цикличной кнопки режима ──────────────

        private static Texture2D GetModeTexture(AssignMode mode)
        {
            return mode switch
            {
                AssignMode.Weapon   => Resources.Textures.AssignWeapon,
                AssignMode.Tool     => Resources.Textures.AssignTool,
                AssignMode.NoAction => Resources.Textures.AssignNoAction,
                _                   => Resources.Textures.AssignBoth
            };
        }

        private static string GetModeTooltip(AssignMode mode)
        {
            return mode switch
            {
                AssignMode.Weapon   => Resources.Strings.Roles.AssignModeWeaponTooltip,
                AssignMode.Tool     => Resources.Strings.Roles.AssignModeToolTooltip,
                AssignMode.NoAction => Resources.Strings.Roles.AssignModeNoActionTooltip,
                _                   => Resources.Strings.Roles.AssignModeBothTooltip
            };
        }

        private static AssignMode NextMode(AssignMode mode)
        {
            return mode switch
            {
                AssignMode.Both     => AssignMode.Weapon,
                AssignMode.Weapon   => AssignMode.Tool,
                AssignMode.Tool     => AssignMode.NoAction,
                AssignMode.NoAction => AssignMode.Both,
                _                   => AssignMode.Both
            };
        }

        // ────────────────────────────────────────────────────────────────────

        private static IEnumerable<Widgets.DropdownMenuElement<EquipmentManager.Role>> Button_GenerateMenu(Pawn pawn)
        {
            var roles = EquipmentManager.GetRoles().ToList();
            return roles.Any()
                ? new[]
                {
                    new Widgets.DropdownMenuElement<EquipmentManager.Role>
                    {
                        option = new FloatMenuOption($"* {Resources.Strings.Roles.AutoSelect}",
                            () => EquipmentManager.SetPawnRole(pawn, null, true))
                    }
                }.Union(roles.Select(role => new Widgets.DropdownMenuElement<EquipmentManager.Role>
                {
                    option = new FloatMenuOption(role.Label,
                        () => EquipmentManager.SetPawnRole(pawn, role, false)),
                    payload = role
                }))
                : Array.Empty<Widgets.DropdownMenuElement<EquipmentManager.Role>>();
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return (EquipmentManager.GetPawnRole(a)?.RoleId ?? int.MinValue).CompareTo(
                EquipmentManager.GetPawnRole(b)?.RoleId ?? int.MinValue);
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            // Две кнопки в одну строку справа; каждая — квадрат со стороной (height-4)
            var btnSize = 16f;
            const float btnGap = 2f;
            var twoButtonsWidth = btnSize * 2 + btnGap;

            // Кнопка роли занимает оставшуюся ширину
            var loadoutButtonRect = new Rect(rect.x, rect.y + 2,
                rect.width - twoButtonsWidth - btnGap, rect.height - 4);

            if (pawn.IsQuestLodger())
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(loadoutButtonRect, "Unchangeable".Translate().Truncate(loadoutButtonRect.width));
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                var pawnRole = EquipmentManager.GetPawnRole(pawn);
                var role     = EquipmentManager.GetRole(pawnRole?.RoleId);
                var label    = role != null ? role.Label : Resources.Strings.Roles.Default.NoRole;
                if (pawnRole?.Automatic ?? false) { label = $"* {label}"; }
                Widgets.Dropdown(loadoutButtonRect, pawn, p => EquipmentManager.GetRole(pawn),
                    Button_GenerateMenu, label,
                    dragLabel: label.Truncate(loadoutButtonRect.width), paintable: true);

                // ── Левая кнопка ряда: «Обновить» (Refresh) ──────────────────
                var forceUpdateButtonRect = new Rect(
                    rect.xMax - twoButtonsWidth,
                    rect.y + 2f,
                    btnSize,
                    btnSize);

                TooltipHandler.TipRegion(forceUpdateButtonRect,
                    "EquipmentManager.ForceUpdatePawn.Tooltip".Translate());
                if (Widgets.ButtonImage(forceUpdateButtonRect, Resources.Textures.Refresh))
                {
                    var mapComp = pawn.Map?.GetComponent<EquipmentManagerMapComponent>();
                    mapComp?.ForceUpdateForPawn(pawn);
                }

                // ── Правая кнопка ряда: цикличный выбор режима ───────────────
                var modeButtonRect = new Rect(
                    rect.xMax - btnSize,
                    rect.y + 2f,
                    btnSize,
                    btnSize);

                var currentMode = pawnRole?.Mode ?? AssignMode.Both;
                TooltipHandler.TipRegion(modeButtonRect, GetModeTooltip(currentMode));
                if (Widgets.ButtonImage(modeButtonRect, GetModeTexture(currentMode)))
                {
                    if (pawnRole == null)
                    {
                        // Создаём запись для пешки, если её ещё нет
                        EquipmentManager.SetPawnRole(pawn, role, false);
                        pawnRole = EquipmentManager.GetPawnRole(pawn);
                    }
                    if (pawnRole != null)
                    {
                        pawnRole.Mode = NextMode(pawnRole.Mode);
                    }
                }
            }
        }

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
            MouseoverSounds.DoRegion(rect);

            const float buttonHeight   = 32f;
            const float iconButtonSize = 16f;
            const float iconButtonGap  = 2f;

            var headerButtonY = rect.y + (rect.height - 65f);

            // Две иконки справа (Refresh + глобальный режим), как в ячейках строк
            var twoIconsWidth  = iconButtonSize * 2 + iconButtonGap;
            var iconBaseX      = rect.x + Mathf.Min(rect.width, 360f) - twoIconsWidth;
            var iconY          = headerButtonY + (buttonHeight - iconButtonSize) / 2f;

            // «Manage Roles» — сужаем, чтобы уместить два значка справа
            var manageButtonWidth = iconBaseX - rect.x - iconButtonGap;
            var manageButtonRect  = new Rect(rect.x, headerButtonY, manageButtonWidth, buttonHeight);

            if (Widgets.ButtonText(manageButtonRect, "Equip. manager"))
            {
                Find.WindowStack.Add(new ManageRolesDialog(null));
            }

            // ── Левая иконка: кнопка глобального переназначения ────────────────
            var globalReassignRect = new Rect(iconBaseX, iconY, iconButtonSize, iconButtonSize);

            TooltipHandler.TipRegion(globalReassignRect,
                "EquipmentManager.GlobalReassign.Tooltip".Translate());

            if (Widgets.ButtonImage(globalReassignRect, Resources.Textures.Refresh))
            {
                var map = Find.CurrentMap;
                if (map != null)
                {
                    GlobalReassigner.GlobalReassignAll(map);
                }
            }

            // ── Правая иконка: глобальный цикличный выбор режима ───────────────
            var globalModeRect = new Rect(iconBaseX + iconButtonSize + iconButtonGap, iconY,
                iconButtonSize, iconButtonSize);

            var globalMode = GetGlobalMode();
            TooltipHandler.TipRegion(globalModeRect,
                GetModeTooltip(globalMode) + 
                "EquipmentManager.Roles.GlobalAssignModeHint".Translate());

            if (Widgets.ButtonImage(globalModeRect, GetModeTexture(globalMode)))
            {
                var nextMode = NextMode(globalMode);
                SetAllPawnsMode(nextMode);
            }
            // ───────────────────────────────────────────────────────────────────
        }

        /// <summary>
        /// Возвращает режим, общий для всех колонистов карты.
        /// Если режимы различаются — возвращает Both.
        /// </summary>
        private static AssignMode GetGlobalMode()
        {
            var map = Find.CurrentMap;
            if (map == null) { return AssignMode.Both; }
            var pawns = map.mapPawns.FreeColonistsSpawned.ToList();
            if (!pawns.Any()) { return AssignMode.Both; }
            var first = EquipmentManager.GetPawnRole(pawns[0])?.Mode ?? AssignMode.Both;
            return pawns.All(p => (EquipmentManager.GetPawnRole(p)?.Mode ?? AssignMode.Both) == first)
                ? first
                : AssignMode.Both;
        }

        /// <summary>
        /// Устанавливает режим всем пешкам, у которых уже есть PawnRole.
        /// Пешкам без записи создаём запись с текущей ролью.
        /// </summary>
        private static void SetAllPawnsMode(AssignMode mode)
        {
            var map = Find.CurrentMap;
            if (map == null) { return; }

            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                var pawnRole = EquipmentManager.GetPawnRole(pawn);
                if (pawnRole == null)
                {
                    var role = EquipmentManager.GetRole(pawn);
                    EquipmentManager.SetPawnRole(pawn, role, false);
                    pawnRole = EquipmentManager.GetPawnRole(pawn);
                }
                if (pawnRole != null) { pawnRole.Mode = mode; }
            }
        }

        public override int GetMinHeaderHeight(PawnTable table)
        {
            return Mathf.Max(base.GetMinHeaderHeight(table), 65);
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(194f));
        }

        public override int GetOptimalWidth(PawnTable table)
        {
            return Mathf.Clamp(Mathf.CeilToInt(251f), GetMinWidth(table), GetMaxWidth(table));
        }
    }
}
