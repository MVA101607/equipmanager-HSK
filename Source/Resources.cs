using UnityEngine;
using Verse;

namespace EquipmentManager
{
    internal static class Resources
    {
        internal static class Strings
        {
            internal static readonly string Add = $"EquipmentManager.{nameof(Add)}".Translate();

            internal static class Roles
            {
                internal static readonly string SaveProfile =
                    $"EquipmentManager.Roles.{nameof(SaveProfile)}".Translate();
                internal static readonly string SaveProfileNamePrompt =
                    $"EquipmentManager.Roles.{nameof(SaveProfileNamePrompt)}".Translate();
                internal static readonly string SaveProfileConfirm =
                    $"EquipmentManager.Roles.{nameof(SaveProfileConfirm)}".Translate();
                internal static readonly string ProfilesListHeader =
                    $"EquipmentManager.Roles.{nameof(ProfilesListHeader)}".Translate();

                internal static readonly string AddRole =
                    $"EquipmentManager.Roles.{nameof(AddRole)}".Translate();

                internal static readonly string AutoSelect =
                    $"EquipmentManager.Roles.{nameof(AutoSelect)}".Translate();

                internal static readonly string AvailablePawns =
                    $"EquipmentManager.Roles.{nameof(AvailablePawns)}".Translate();

                internal static readonly string CancelDataImport =
                    $"EquipmentManager.Roles.{nameof(CancelDataImport)}".Translate();

                internal static readonly string CopyRole =
                    $"EquipmentManager.Roles.{nameof(CopyRole)}".Translate();

                internal static readonly string DeleteRole =
                    $"EquipmentManager.Roles.{nameof(DeleteRole)}".Translate();

                internal static readonly string DropUnassignedWeapons =
                    $"EquipmentManager.Roles.{nameof(DropUnassignedWeapons)}".Translate();

                internal static readonly string DropUnassignedWeaponsTooltip =
                    $"EquipmentManager.Roles.{nameof(DropUnassignedWeaponsTooltip)}".Translate();

                internal static readonly string ImportData =
                    $"EquipmentManager.Roles.{nameof(ImportData)}".Translate();

                internal static readonly string ImportRoles =
                    $"EquipmentManager.Roles.{nameof(ImportRoles)}".Translate();

                internal static readonly string RoleLabel =
                    $"EquipmentManager.Roles.{nameof(RoleLabel)}".Translate();

                internal static readonly string RoleListHeader =
                    $"EquipmentManager.Roles.{nameof(RoleListHeader)}".Translate();

                internal static readonly string RoleSettings =
                    $"EquipmentManager.Roles.{nameof(RoleSettings)}".Translate();

                internal static readonly string Log = $"EquipmentManager.Roles.{nameof(Log)}".Translate();

                internal static readonly string ManageRoles =
                    $"EquipmentManager.Roles.{nameof(ManageRoles)}".Translate();

                internal static readonly string ManageWeaponRules =
                    $"EquipmentManager.Roles.{nameof(ManageWeaponRules)}".Translate();

                internal static readonly string MeleeSidearmRulesLabel =
                    $"EquipmentManager.Roles.{nameof(MeleeSidearmRulesLabel)}".Translate();

                internal static readonly string NoRoleSelected =
                    $"EquipmentManager.Roles.{nameof(NoRoleSelected)}".Translate();

                internal static readonly string PawnCapacityLimits =
                    $"EquipmentManager.Roles.{nameof(PawnCapacityLimits)}".Translate();

                internal static readonly string PawnCapacityWeights =
                    $"EquipmentManager.Roles.{nameof(PawnCapacityWeights)}".Translate();

                internal static readonly string PawnPassions =
                    $"EquipmentManager.Roles.{nameof(PawnPassions)}".Translate();

                internal static readonly string PawnSkillLimits =
                    $"EquipmentManager.Roles.{nameof(PawnSkillLimits)}".Translate();

                internal static readonly string PawnSkillWeights =
                    $"EquipmentManager.Roles.{nameof(PawnSkillWeights)}".Translate();

                internal static readonly string PawnStatLimits =
                    $"EquipmentManager.Roles.{nameof(PawnStatLimits)}".Translate();

                internal static readonly string PawnStatWeights =
                    $"EquipmentManager.Roles.{nameof(PawnStatWeights)}".Translate();

                internal static readonly string PawnTraits =
                    $"EquipmentManager.Roles.{nameof(PawnTraits)}".Translate();

                internal static readonly string PawnWorkCapacities =
                    $"EquipmentManager.Roles.{nameof(PawnWorkCapacities)}".Translate();

                internal static readonly string PrimaryWeaponLabel =
                    $"EquipmentManager.Roles.{nameof(PrimaryWeaponLabel)}".Translate();
                internal static readonly string SecondaryWeaponLabel =
                    $"EquipmentManager.Roles.{nameof(SecondaryWeaponLabel)}".Translate();

                internal static readonly string PriorityLabel =
                    $"EquipmentManager.Roles.{nameof(PriorityLabel)}".Translate();

                internal static readonly string PriorityTooltip =
                    $"EquipmentManager.Roles.{nameof(PriorityTooltip)}".Translate();

                internal static readonly string RangedSidearmRulesLabel =
                    $"EquipmentManager.Roles.{nameof(RangedSidearmRulesLabel)}".Translate();

                internal static readonly string Rules = $"EquipmentManager.Roles.{nameof(Rules)}".Translate();

                internal static readonly string SavedGamesListHeader =
                    $"EquipmentManager.Roles.{nameof(SavedGamesListHeader)}".Translate();

                internal static readonly string SelectRole =
                    $"EquipmentManager.Roles.{nameof(SelectRole)}".Translate();

                internal static readonly string ToolsLabel =
                    $"EquipmentManager.Roles.{nameof(ToolsLabel)}".Translate();

                internal static string GetPrimaryWeaponTypeLabel(Role.PrimaryWeaponType primaryWeaponType)
                {
                    return $"EquipmentManager.Roles.PrimaryWeaponTypes.{primaryWeaponType}".Translate();
                }
                internal static string GetSecondaryWeaponTypeLabel(Role.PrimaryWeaponType weaponType)
                {
                    return $"EquipmentManager.Roles.PrimaryWeaponTypes.{weaponType}".Translate();
                }

                internal static readonly string AssignModeBothTooltip =
                    $"EquipmentManager.Roles.{nameof(AssignModeBothTooltip)}".Translate();

                internal static readonly string AssignModeWeaponTooltip =
                    $"EquipmentManager.Roles.{nameof(AssignModeWeaponTooltip)}".Translate();

                internal static readonly string AssignModeToolTooltip =
                    $"EquipmentManager.Roles.{nameof(AssignModeToolTooltip)}".Translate();

                internal static readonly string AssignModeNoActionTooltip =
                    $"EquipmentManager.Roles.{nameof(AssignModeNoActionTooltip)}".Translate();

                internal static class Default
                {
                    internal static readonly string Assault =
                        $"EquipmentManager.Roles.Default.{nameof(Assault)}".Translate();

                    internal static readonly string Crusher =
                        $"EquipmentManager.Roles.Default.{nameof(Crusher)}".Translate();

                    internal static readonly string NoRole =
                        $"EquipmentManager.Roles.Default.{nameof(NoRole)}".Translate();

                    /// <summary>Метка системной роли "ВЫКЛ" (Id=-1).</summary>
                    internal static readonly string Off =
                        $"EquipmentManager.Roles.Default.{nameof(Off)}".Translate();

                    /// <summary>Метка системной роли "Авто" (Id=-2).</summary>
                    internal static readonly string Auto =
                        $"EquipmentManager.Roles.Default.{nameof(Auto)}".Translate();

                    internal static readonly string Pacifist =
                        $"EquipmentManager.Roles.Default.{nameof(Pacifist)}".Translate();

                    internal static readonly string Slasher =
                        $"EquipmentManager.Roles.Default.{nameof(Slasher)}".Translate();

                    internal static readonly string Sniper =
                        $"EquipmentManager.Roles.Default.{nameof(Sniper)}".Translate();

                    internal static readonly string Support =
                        $"EquipmentManager.Roles.Default.{nameof(Support)}".Translate();
                }
            }

            internal static class Stats
            {
                internal static string GetStatDescription(string defName)
                {
                    return $"EquipmentManager.Stats.{defName}.Description".Translate();
                }

                internal static string GetStatLabel(string defName)
                {
                    return $"EquipmentManager.Stats.{defName}.Label".Translate();
                }
            }

            internal static class WeaponRules
            {
                internal static readonly string AddRule = $"EquipmentManager.WeaponRules.{nameof(AddRule)}".Translate();

                internal static readonly string BlacklistedItems =
                    $"EquipmentManager.WeaponRules.{nameof(BlacklistedItems)}".Translate();

                internal static readonly string BlacklistedItemsTooltip =
                    $"EquipmentManager.WeaponRules.{nameof(BlacklistedItemsTooltip)}".Translate();

                internal static readonly string CopyRule =
                    $"EquipmentManager.WeaponRules.{nameof(CopyRule)}".Translate();

                internal static readonly string CurrentlyAvailableItems =
                    $"EquipmentManager.WeaponRules.{nameof(CurrentlyAvailableItems)}".Translate();

                internal static readonly string CurrentlyAvailableItemsTooltip =
                    $"EquipmentManager.WeaponRules.{nameof(CurrentlyAvailableItemsTooltip)}".Translate();

                internal static readonly string DeleteRule =
                    $"EquipmentManager.WeaponRules.{nameof(DeleteRule)}".Translate();

                internal static readonly string GloballyAvailableItems =
                    $"EquipmentManager.WeaponRules.{nameof(GloballyAvailableItems)}".Translate();

                internal static readonly string GloballyAvailableItemsTooltip =
                    $"EquipmentManager.WeaponRules.{nameof(GloballyAvailableItemsTooltip)}".Translate();

                internal static readonly string ItemProperties =
                    $"EquipmentManager.WeaponRules.{nameof(ItemProperties)}".Translate();

                internal static readonly string NoRuleSelected =
                    $"EquipmentManager.WeaponRules.{nameof(NoRuleSelected)}".Translate();

                internal static readonly string Refresh = $"EquipmentManager.WeaponRules.{nameof(Refresh)}".Translate();

                internal static readonly string RuleEquipModeLabel =
                    $"EquipmentManager.WeaponRules.{nameof(RuleEquipModeLabel)}".Translate();

                internal static readonly string RuleLabel =
                    $"EquipmentManager.WeaponRules.{nameof(RuleLabel)}".Translate();

                internal static readonly string RuleSettings =
                    $"EquipmentManager.WeaponRules.{nameof(RuleSettings)}".Translate();

                internal static readonly string SelectRule =
                    $"EquipmentManager.WeaponRules.{nameof(SelectRule)}".Translate();

                internal static readonly string StatLimits =
                    $"EquipmentManager.WeaponRules.{nameof(StatLimits)}".Translate();

                internal static readonly string StatWeights =
                    $"EquipmentManager.WeaponRules.{nameof(StatWeights)}".Translate();

                internal static readonly string WhitelistedItems =
                    $"EquipmentManager.WeaponRules.{nameof(WhitelistedItems)}".Translate();

                internal static readonly string WhitelistedItemsTooltip =
                    $"EquipmentManager.WeaponRules.{nameof(WhitelistedItemsTooltip)}".Translate();

                internal static string GetWeaponEquipModeLabel(ItemRule.WeaponEquipMode equipMode)
                {
                    return $"EquipmentManager.WeaponRules.WeaponEquipModes.{equipMode}".Translate();
                }

                internal static class MeleeWeapons
                {
                    internal static readonly string Rottable =
                        $"EquipmentManager.WeaponRules.MeleeWeapons.{nameof(Rottable)}".Translate();

                    internal static readonly string RottableTooltip =
                        $"EquipmentManager.WeaponRules.MeleeWeapons.{nameof(RottableTooltip)}".Translate();

                    internal static readonly string RetentionBonus =
                        $"EquipmentManager.WeaponRules.MeleeWeapons.{nameof(RetentionBonus)}".Translate();

                    internal static readonly string RetentionBonusTooltip =
                        $"EquipmentManager.WeaponRules.MeleeWeapons.{nameof(RetentionBonusTooltip)}".Translate();

                    internal static readonly string Title =
                        $"EquipmentManager.WeaponRules.MeleeWeapons.{nameof(Title)}".Translate();

                    internal static readonly string UsableWithShields =
                        $"EquipmentManager.WeaponRules.MeleeWeapons.{nameof(UsableWithShields)}".Translate();

                    internal static readonly string UsableWithShieldsTooltip =
                        $"EquipmentManager.WeaponRules.MeleeWeapons.{nameof(UsableWithShieldsTooltip)}".Translate();

                    internal static class Default
                    {
                        internal static readonly string Bluntest =
                            $"EquipmentManager.WeaponRules.MeleeWeapons.Default.{nameof(Bluntest)}".Translate();

                        internal static readonly string HighestDps =
                            $"EquipmentManager.WeaponRules.MeleeWeapons.Default.{nameof(HighestDps)}".Translate();

                        internal static readonly string Sharpest =
                            $"EquipmentManager.WeaponRules.MeleeWeapons.Default.{nameof(Sharpest)}".Translate();
                    }
                }

                internal static class RangedWeapons
                {
                    internal static readonly string AmmoCount =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(AmmoCount)}".Translate();

                    internal static readonly string AmmoCountTooltip =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(AmmoCountTooltip)}".Translate();

                    internal static readonly string RetentionBonus =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(RetentionBonus)}".Translate();

                    internal static readonly string RetentionBonusTooltip =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(RetentionBonusTooltip)}".Translate();

                    internal static readonly string AmmoType =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(AmmoType)}".Translate();

                    internal static readonly string AmmoTypeTooltip =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(AmmoTypeTooltip)}".Translate();

                    internal static readonly string Explosive =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(Explosive)}".Translate();

                    internal static readonly string ExplosiveTooltip =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(ExplosiveTooltip)}".Translate();

                    internal static readonly string ManualCast =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(ManualCast)}".Translate();

                    internal static readonly string ManualCastTooltip =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(ManualCastTooltip)}".Translate();

                    internal static readonly string Title =
                        $"EquipmentManager.WeaponRules.RangedWeapons.{nameof(Title)}".Translate();

                    internal static class Default
                    {
                        internal static readonly string HighestDpsa =
                            $"EquipmentManager.WeaponRules.RangedWeapons.Default.{nameof(HighestDpsa)}".Translate();

                        internal static readonly string HighRof =
                            $"EquipmentManager.WeaponRules.RangedWeapons.Default.{nameof(HighRof)}".Translate();

                        internal static readonly string LongRangeHeavyHitter =
                            $"EquipmentManager.WeaponRules.RangedWeapons.Default.{nameof(LongRangeHeavyHitter)}"
                                .Translate();

                        internal static readonly string LowWarmupTime =
                            $"EquipmentManager.WeaponRules.RangedWeapons.Default.{nameof(LowWarmupTime)}".Translate();
                    }
                }

                internal static class Tools
                {
                    internal static readonly string Ranged =
                        $"EquipmentManager.WeaponRules.Tools.{nameof(Ranged)}".Translate();

                    internal static readonly string RangedTooltip =
                        $"EquipmentManager.WeaponRules.Tools.{nameof(RangedTooltip)}".Translate();

                    internal static readonly string Title =
                        $"EquipmentManager.WeaponRules.Tools.{nameof(Title)}".Translate();

                    internal static class Default
                    {
                        internal static readonly string AllWorkTypes =
                            $"EquipmentManager.WeaponRules.Tools.Default.{nameof(AllWorkTypes)}".Translate();

                        internal static readonly string AssignedWorkTypes =
                            $"EquipmentManager.WeaponRules.Tools.Default.{nameof(AssignedWorkTypes)}".Translate();
                    }
                }

                internal static class WorkTypes
                {
                    internal static readonly string Title =
                        $"EquipmentManager.WeaponRules.WorkTypes.{nameof(Title)}".Translate();
                }
            }
        }

        [StaticConstructorOnStartup]
        internal static class Textures
        {
            internal static readonly Texture2D Refresh = ContentFinder<Texture2D>.Get("equipment-manager-refresh");
            internal static readonly Texture2D Edit = ContentFinder<Texture2D>.Get("equipment-manager-edit");
            internal static readonly Texture2D Delete = ContentFinder<Texture2D>.Get("equipment-manager-delete");
            internal static readonly Texture2D PassionMajor = ContentFinder<Texture2D>.Get("UI/Icons/PassionMajor");
            internal static readonly Texture2D PassionMinor = ContentFinder<Texture2D>.Get("UI/Icons/PassionMinor");

            // Иконки режима автоназначения
            internal static readonly Texture2D AssignBoth     = ContentFinder<Texture2D>.Get("EM_both");
            internal static readonly Texture2D AssignWeapon   = ContentFinder<Texture2D>.Get("EM_weapon");
            internal static readonly Texture2D AssignTool     = ContentFinder<Texture2D>.Get("EM_tool");
            internal static readonly Texture2D AssignNoAction = ContentFinder<Texture2D>.Get("EM_no_action");
        }
    }
}