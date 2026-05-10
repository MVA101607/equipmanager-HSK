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
            var editButtonSize = (rect.height - 4) / 2;
            // Два маленьких квадрата справа: [Edit] сверху, [Refresh] снизу
            var loadoutButtonRect = new Rect(rect.x, rect.y + 2,
                rect.width - editButtonSize - 4, rect.height - 4);

            if (pawn.IsQuestLodger())
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(loadoutButtonRect, "Unchangeable".Translate().Truncate(loadoutButtonRect.width));
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                var pawnRole = EquipmentManager.GetPawnRole(pawn);
                var role = EquipmentManager.GetRole(pawnRole?.RoleId);
                var label = role != null ? role.Label : Resources.Strings.Roles.Default.NoRole;
                if (pawnRole?.Automatic ?? false) { label = $"* {label}"; }
                Widgets.Dropdown(loadoutButtonRect, pawn, p => EquipmentManager.GetRole(pawn),
                    Button_GenerateMenu, label,
                    dragLabel: label.Truncate(loadoutButtonRect.width), paintable: true);
            }

            // Кнопка Edit — верхний правый квадрат
            var editButtonRect = new Rect(rect.xMax - editButtonSize, rect.y + 2f,
                editButtonSize, editButtonSize);

            // Кнопка принудительного обновления — нижний правый квадрат
            var forceUpdateButtonRect = new Rect(rect.xMax - editButtonSize,
                rect.y + 2f + editButtonSize, editButtonSize, editButtonSize);

            if (!pawn.IsQuestLodger())
            {
                // Edit
                TooltipHandler.TipRegion(editButtonRect, "AssignTabEdit".Translate());
                if (Widgets.ButtonImage(editButtonRect, Resources.Textures.Edit))
                {
                    Find.WindowStack.Add(new ManageRolesDialog(EquipmentManager.GetRole(pawn)));
                }

                // Force Update: переназначить роль и оружие прямо сейчас
                TooltipHandler.TipRegion(forceUpdateButtonRect,
                    "EquipmentManager.ForceUpdatePawn.Tooltip".Translate());
                if (Widgets.ButtonImage(forceUpdateButtonRect, Resources.Textures.Refresh))
                {
                    var mapComp = pawn.Map?.GetComponent<EquipmentManagerMapComponent>();
                    mapComp?.ForceUpdateForPawn(pawn);
                }
            }
        }

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
            MouseoverSounds.DoRegion(rect);
            var buttonRect = new Rect(rect.x, rect.y + (rect.height - 65f), Mathf.Min(rect.width, 360f), 32f);
            if (Widgets.ButtonText(buttonRect, Resources.Strings.Roles.ManageRoles))
            {
                Find.WindowStack.Add(new ManageRolesDialog(null));
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