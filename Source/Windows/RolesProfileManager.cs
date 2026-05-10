using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    /// <summary>
    /// Сохранение / загрузка профилей настроек в собственную папку мода.
    /// Путь: &lt;RimWorld Config&gt;/EquipmentManager/*.xml
    /// Формат — стандартный RimWorld XML через SafeSaver / ScribeLoader,
    /// идентичный формату сейва (те же теги WorkTypeRules, ToolRules, MeleeWeaponRules,
    /// RangedWeaponRules, Loadouts), поэтому ImportRolesDialog может читать эти файлы
    /// без изменения логики парсинга.
    /// </summary>
    internal static class RolesProfileManager
    {
        public const string FileExtension = ".xml";
        private const string LegacyFileExtension = ".emprofile";
        private const string RootNode = "EquipmentManagerProfile";

        // ── Папка профилей ────────────────────────────────────────────────
        public static string ProfileFolder
        {
            get
            {
                var folder = Path.Combine(GenFilePaths.ConfigFolderPath, "EquipmentManager");
                if (!Directory.Exists(folder))
                { _ = Directory.CreateDirectory(folder); }
                return folder;
            }
        }

        /// <summary>
        /// Список профилей: имя файла (без расширения) → полный путь.
        /// Возвращает *.xml (а также legacy *.emprofile, если остались), отсортированные по дате изменения.
        /// </summary>
        public static Dictionary<string, string> GetProfiles()
        {
            var result = new Dictionary<string, string>();
            try
            {
                var paths = Directory.GetFiles(ProfileFolder, $"*{FileExtension}")
                    .Concat(Directory.GetFiles(ProfileFolder, $"*{LegacyFileExtension}"))
                    .OrderByDescending(p => new FileInfo(p).LastWriteTimeUtc);
                foreach (var path in paths)
                {
                    var key = Path.GetFileNameWithoutExtension(path);
                    if (!result.ContainsKey(key)) { result[key] = path; }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Equipment Manager: Could not list profiles: {ex.Message}");
            }
            return result;
        }

        // ── Сохранение ────────────────────────────────────────────────────
        /// <summary>
        /// Сериализует текущие правила и роли в файл profileName.emprofile.
        /// Использует SafeSaver — стандартный механизм RimWorld.
        /// </summary>
        public static bool SaveProfile(string profileName)
        {
            var safeName = MakeSafeFileName(profileName);
            if (safeName.NullOrEmpty())
            {
                Messages.Message("EquipmentManager.Profiles.InvalidName".Translate(),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            var path = Path.Combine(ProfileFolder, safeName + FileExtension);
            try
            {
                SafeSaver.Save(path, RootNode, () =>
                {
                    var em = Current.Game.GetComponent<EquipmentManagerGameComponent>();

                    var workTypeRules = em.GetWorkTypeRules().ToList();
                    Scribe_Collections.Look(ref workTypeRules, "WorkTypeRules", LookMode.Deep);

                    var toolRules = em.GetToolRules().ToList();
                    Scribe_Collections.Look(ref toolRules, "ToolRules", LookMode.Deep);

                    var meleeRules = em.GetMeleeWeaponRules().ToList();
                    Scribe_Collections.Look(ref meleeRules, "MeleeWeaponRules", LookMode.Deep);

                    var rangedRules = em.GetRangedWeaponRules().ToList();
                    Scribe_Collections.Look(ref rangedRules, "RangedWeaponRules", LookMode.Deep);

                    // Тег "Loadouts" — для совместимости с ReadRolesData в ImportRolesDialog
                    var roles = em.GetRoles().ToList();
                    Scribe_Collections.Look(ref roles, "Loadouts", LookMode.Deep);
                });
                Messages.Message(
                    "EquipmentManager.Profiles.Saved".Translate(safeName),
                    MessageTypeDefOf.TaskCompletion, false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Equipment Manager: Could not save profile '{path}': {ex.Message}");
                Messages.Message(
                    "EquipmentManager.Profiles.SaveFailed".Translate(ex.Message),
                    MessageTypeDefOf.NegativeEvent, false);
                return false;
            }
        }

        // ── Вспомогательное ───────────────────────────────────────────────
        public static string MakeSafeFileName(string name)
        {
            if (name.NullOrEmpty()) { return name; }
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        }

        /// <summary>
        /// Полный путь к файлу по имени (без расширения).
        /// </summary>
        public static string GetProfilePath(string profileName)
        {
            return Path.Combine(ProfileFolder, MakeSafeFileName(profileName) + FileExtension);
        }
    }
}
