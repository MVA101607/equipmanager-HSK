using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;
using Strings = EquipmentManager.Resources.Strings.Roles;

namespace EquipmentManager
{
    /// <summary>
    /// Кэшированные данные дефолтного профиля, прочитанные из <c>Common/default_roles.xml</c>.
    /// Файл имеет идентичный формат с пользовательскими профилями
    /// (см. <see cref="RolesProfileManager"/> и <see cref="Windows.ImportRolesDialog"/>),
    /// поэтому пользователь может заменить его любым сохранённым профилем,
    /// переименовав в <c>default_roles.xml</c>.
    /// Все 5 секций профиля — WorkTypeRules, ToolRules, MeleeWeaponRules,
    /// RangedWeaponRules, Loadouts — применяются как дефолты соответствующих
    /// списков в <see cref="EquipmentManagerGameComponent"/>.
    /// </summary>
    internal static class DefaultProfile
    {
        private const string DefaultProfilePackageId = "LordKuper.EquipmentManager";
        private const string DefaultProfileRelativePath = "Common/default_roles.xml";

        private static ProfileXmlReader.ProfileData _cachedData;
        private static bool _loaded;

        private static ProfileXmlReader.ProfileData Data
        {
            get
            {
                if (_loaded) { return _cachedData; }
                _loaded = true;
                try
                {
                    var path = ResolvePath();
                    if (path == null || !File.Exists(path))
                    {
                        Log.Warning(
                            $"Equipment Manager: default profile file not found at '{path ?? DefaultProfileRelativePath}', using built-in defaults.");
                        _cachedData = new ProfileXmlReader.ProfileData();
                        return _cachedData;
                    }
                    _cachedData = ProfileXmlReader.ReadProfile(path);
                    ApplyTranslatedRoleLabels(_cachedData.Roles);
                }
                catch (Exception ex)
                {
                    Log.Error($"Equipment Manager: failed to load default profile: {ex.Message}");
                    _cachedData = new ProfileXmlReader.ProfileData();
                }
                return _cachedData;
            }
        }

        public static IEnumerable<Role> Roles => Data.Roles;
        public static IEnumerable<WorkTypeRule> WorkTypeRules => Data.WorkTypeRules;
        public static IEnumerable<ToolRule> ToolRules => Data.ToolRules;
        public static IEnumerable<MeleeWeaponRule> MeleeWeaponRules => Data.MeleeWeaponRules;
        public static IEnumerable<RangedWeaponRule> RangedWeaponRules => Data.RangedWeaponRules;

        private static string ResolvePath()
        {
            var mod = LoadedModManager.RunningModsListForReading?.FirstOrDefault(m =>
                DefaultProfilePackageId.Equals(m?.PackageId, StringComparison.OrdinalIgnoreCase));
            if (mod == null) { return null; }
            return Path.Combine(mod.RootDir, DefaultProfileRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void ApplyTranslatedRoleLabels(IList<Role> roles)
        {
            // Локализация лейблов сохраняется только для исходных дефолтных ролей по их Id.
            // Если пользователь подменил default_roles.xml произвольным профилем, его лейблы
            // останутся нетронутыми (Id незнакомые), что соответствует ожидаемому поведению.
            foreach (var role in roles)
            {
                role.Label = role.Id switch
                {
                    1 => Strings.Default.Assault,
                    2 => Strings.Default.Sniper,
                    3 => Strings.Default.Support,
                    4 => Strings.Default.Slasher,
                    5 => Strings.Default.Crusher,
                    6 => Strings.Default.Pacifist,
                    _ => role.Label
                };
            }
        }
    }
}
