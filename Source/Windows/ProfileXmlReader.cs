using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Verse;

namespace EquipmentManager
{
    /// <summary>
    /// Общий парсер XML-профилей Equipment Manager.
    /// Используется и для загрузки <c>default_roles.xml</c> на старте мода,
    /// и для импорта пользовательских профилей в <see cref="Windows.ImportRolesDialog"/>.
    /// Формат идентичен секции GameComponent в сейве: WorkTypeRules / ToolRules /
    /// MeleeWeaponRules / RangedWeaponRules / Loadouts.
    /// </summary>
    internal static class ProfileXmlReader
    {
        public sealed class ProfileData
        {
            public readonly List<WorkTypeRule> WorkTypeRules = new();
            public readonly List<ToolRule> ToolRules = new();
            public readonly List<MeleeWeaponRule> MeleeWeaponRules = new();
            public readonly List<RangedWeaponRule> RangedWeaponRules = new();
            public readonly List<Role> Roles = new();
        }

        // ─── Public entry points ────────────────────────────────────────────────

        /// <summary>Чтение профиля из файла (default_roles.xml или сохранённый профиль).</summary>
        public static ProfileData ReadProfile(string profilePath)
        {
            var data = new ProfileData();
            if (string.IsNullOrEmpty(profilePath) || !File.Exists(profilePath)) { return data; }
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.Load(profilePath);
                var root = doc.SelectSingleNode("EquipmentManagerProfile");
                if (root == null)
                {
                    Log.Error($"Equipment Manager: Not a valid profile file: {profilePath}");
                    return data;
                }
                var section = root.SelectSingleNode("WorkTypeRules");
                if (section != null) { ReadWorkTypeRulesData(profilePath, new XmlNodeReader(section), data.WorkTypeRules); }
                section = root.SelectSingleNode("MeleeWeaponRules");
                if (section != null) { ReadMeleeWeaponRulesData(profilePath, new XmlNodeReader(section), data.MeleeWeaponRules); }
                section = root.SelectSingleNode("RangedWeaponRules");
                if (section != null) { ReadRangedWeaponRulesData(profilePath, new XmlNodeReader(section), data.RangedWeaponRules); }
                section = root.SelectSingleNode("Loadouts");
                if (section != null) { ReadRolesData(profilePath, new XmlNodeReader(section), data.Roles); }
            }
            catch (Exception ex)
            {
                Log.Warning($"Equipment Manager: Could not read profile {profilePath}{Environment.NewLine}{ex.Message}");
            }
            return data;
        }

        /// <summary>Чтение секции GameComponent из файла сейва игры.</summary>
        public static ProfileData ReadSaveGame(string savedGameFile)
        {
            var data = new ProfileData();
            try
            {
                using var xmlReader = XmlReader.Create(savedGameFile,
                    new XmlReaderSettings
                    {
                        IgnoreWhitespace = true, IgnoreComments = true, IgnoreProcessingInstructions = true
                    });
                if (!xmlReader.ReadToFollowing("savegame"))
                {
                    Log.Error($"Equipment Manager: Could not find root node in the save game file {savedGameFile}");
                    return data;
                }
                if (!xmlReader.ReadToDescendant("game"))
                {
                    Log.Error($"Equipment Manager: Could not find game data in the save game file {savedGameFile}");
                    return data;
                }
                if (!xmlReader.ReadToDescendant("components"))
                {
                    Log.Warning(
                        $"Equipment Manager: Could not find game components' data in the save game file {savedGameFile}");
                    return data;
                }
                if (!xmlReader.ReadToDescendant("li")) { return data; }
                do
                {
                    if (!xmlReader.HasAttributes || xmlReader.IsEmptyElement) { continue; }
                    while (xmlReader.MoveToNextAttribute())
                    {
                        if (xmlReader.Name != "Class") { continue; }
                        if (xmlReader.Value == typeof(EquipmentManagerGameComponent).FullName)
                        {
                            _ = xmlReader.MoveToElement();
                            var liXml = xmlReader.ReadOuterXml();
                            var liDoc = new System.Xml.XmlDocument();
                            liDoc.LoadXml(liXml);
                            var liRoot = liDoc.DocumentElement;
                            var sgSection = liRoot?.SelectSingleNode("WorkTypeRules");
                            if (sgSection != null) { ReadWorkTypeRulesData(savedGameFile, new XmlNodeReader(sgSection), data.WorkTypeRules); }
                            sgSection = liRoot?.SelectSingleNode("MeleeWeaponRules");
                            if (sgSection != null) { ReadMeleeWeaponRulesData(savedGameFile, new XmlNodeReader(sgSection), data.MeleeWeaponRules); }
                            sgSection = liRoot?.SelectSingleNode("RangedWeaponRules");
                            if (sgSection != null) { ReadRangedWeaponRulesData(savedGameFile, new XmlNodeReader(sgSection), data.RangedWeaponRules); }
                            sgSection = liRoot?.SelectSingleNode("Loadouts");
                            if (sgSection != null) { ReadRolesData(savedGameFile, new XmlNodeReader(sgSection), data.Roles); }
                            return data;
                        }
                        _ = xmlReader.MoveToElement();
                        break;
                    }
                } while (xmlReader.ReadToNextSibling("li"));
            }
            catch (Exception exception)
            {
                Log.Warning(
                    $"Equipment Manager: Could not process save game file {savedGameFile}{Environment.NewLine}{exception.Message}");
            }
            return data;
        }

        // ─── Section readers ────────────────────────────────────────────────────

        public static void ReadRolesData(string sourceFile, XmlReader xmlReader, List<Role> target)
        {
            if (xmlReader.ReadToFollowing("Loadouts"))
            {
                var node = xmlReader.ReadSubtree();
                if (node.ReadToDescendant("li"))
                {
                    do { ReadRoleData(node.ReadSubtree(), target); } while (node.ReadToNextSibling("li"));
                }
            }
            else
            {
                Log.Warning(
                    $"Equipment Manager: Could not find 'Loadouts' node in the file {sourceFile}");
            }
        }

        public static void ReadWorkTypeRulesData(string sourceFile, XmlReader xmlReader, List<WorkTypeRule> target)
        {
            if (xmlReader.ReadToFollowing("WorkTypeRules"))
            {
                var node = xmlReader.ReadSubtree();
                if (node.ReadToDescendant("li"))
                {
                    do { ReadWorkTypeRuleData(node.ReadSubtree(), target); } while (node.ReadToNextSibling("li"));
                }
            }
            else
            {
                Log.Warning(
                    $"Equipment Manager: Could not find 'WorkTypeRules' node in the file {sourceFile}");
            }
        }

        public static void ReadMeleeWeaponRulesData(string sourceFile, XmlReader xmlReader,
            List<MeleeWeaponRule> target)
        {
            if (xmlReader.ReadToFollowing("MeleeWeaponRules"))
            {
                var node = xmlReader.ReadSubtree();
                if (node.ReadToDescendant("li"))
                {
                    do { ReadMeleeWeaponRuleData(node.ReadSubtree(), target); } while (node.ReadToNextSibling("li"));
                }
            }
            else
            {
                Log.Warning(
                    $"Equipment Manager: Could not find 'MeleeWeaponRules' node in the file {sourceFile}");
            }
        }

        public static void ReadRangedWeaponRulesData(string sourceFile, XmlReader xmlReader,
            List<RangedWeaponRule> target)
        {
            if (xmlReader.ReadToFollowing("RangedWeaponRules"))
            {
                var node = xmlReader.ReadSubtree();
                if (node.ReadToDescendant("li"))
                {
                    do { ReadRangedWeaponRuleData(node.ReadSubtree(), target); } while (node.ReadToNextSibling("li"));
                }
            }
            else
            {
                Log.Warning(
                    $"Equipment Manager: Could not find 'RangedWeaponRules' node in the file {sourceFile}");
            }
        }

        // ─── Role ───────────────────────────────────────────────────────────────

        private static void ReadRoleData(XmlReader xmlReader, List<Role> target)
        {
            if (!xmlReader.ReadToFollowing("li") || !xmlReader.Read()) { return; }
            var id = 0;
            var label = string.Empty;
            var priority = 0;
            var primaryRuleType = Role.PrimaryWeaponType.None;
            int? primaryRangedWeaponRuleId = null;
            int? primaryMeleeWeaponRuleId = null;
            var rangedSidearmRules = new List<int>();
            var meleeSidearmRules = new List<int>();
            var secondaryRuleType = Role.PrimaryWeaponType.None;
            int? secondaryRangedWeaponRuleId = null;
            int? secondaryMeleeWeaponRuleId = null;
            int? toolRuleId = null;
            var pawnTraits = new Dictionary<string, bool>();
            var pawnWorkCapacities = new Dictionary<string, bool>();
            var dropUnassignedWeapons = true;
            var isDisabled = false;
            var passionLimits = new List<PassionLimit>();
            var pawnCapacityLimits = new List<PawnCapacityLimit>();
            var pawnCapacityWeights = new List<PawnCapacityWeight>();
            var skillLimits = new List<SkillLimit>();
            var skillWeights = new List<SkillWeight>();
            var statLimits = new List<StatLimit>();
            var statWeights = new List<StatWeight>();
            while (true)
            {
                if (xmlReader.NodeType != XmlNodeType.Element || xmlReader.IsEmptyElement)
                {
                    if (!xmlReader.Read()) { break; }
                    continue;
                }
                switch (xmlReader.Name)
                {
                    case "Id":
                        id = xmlReader.ReadElementContentAsInt();
                        break;
                    case "Label":
                        label = xmlReader.ReadElementContentAsString();
                        break;
                    case "Priority":
                        priority = xmlReader.ReadElementContentAsInt();
                        break;
                    case "PrimaryRuleType":
                        _ = Enum.TryParse(xmlReader.ReadElementContentAsString(), out primaryRuleType);
                        break;
                    case "PrimaryRangedWeaponRuleId":
                        primaryRangedWeaponRuleId = xmlReader.ReadElementContentAsInt();
                        break;
                    case "PrimaryMeleeWeaponRuleId":
                        primaryMeleeWeaponRuleId = xmlReader.ReadElementContentAsInt();
                        break;
                    case "RangedSidearmRules":
                        ReadIntList(xmlReader, rangedSidearmRules);
                        break;
                    case "MeleeSidearmRules":
                        ReadIntList(xmlReader, meleeSidearmRules);
                        break;
                    case "SecondaryRuleType":
                        _ = Enum.TryParse(xmlReader.ReadElementContentAsString(), out secondaryRuleType);
                        break;
                    case "SecondaryRangedWeaponRuleId":
                        secondaryRangedWeaponRuleId = xmlReader.ReadElementContentAsInt();
                        break;
                    case "SecondaryMeleeWeaponRuleId":
                        secondaryMeleeWeaponRuleId = xmlReader.ReadElementContentAsInt();
                        break;
                    case "ToolRuleId":
                        toolRuleId = xmlReader.ReadElementContentAsInt();
                        break;
                    case "PawnTraits":
                        _ = xmlReader.Read();
                        ReadStringBoolDictionary(xmlReader, pawnTraits);
                        xmlReader.ReadEndElement();
                        break;
                    case "PawnWorkCapacities":
                        _ = xmlReader.Read();
                        ReadStringBoolDictionary(xmlReader, pawnWorkCapacities);
                        xmlReader.ReadEndElement();
                        break;
                    case "DropUnassignedWeapons":
                        dropUnassignedWeapons = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    case "IsDisabled":
                        isDisabled = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    case "PassionLimits":
                        ReadList(xmlReader, passionLimits, ReadPassionLimit);
                        break;
                    case "PawnCapacityLimits":
                        ReadList(xmlReader, pawnCapacityLimits, ReadPawnCapacityLimit);
                        break;
                    case "PawnCapacityWeights":
                        ReadList(xmlReader, pawnCapacityWeights, ReadPawnCapacityWeight);
                        break;
                    case "SkillLimits":
                        ReadList(xmlReader, skillLimits, ReadSkillLimit);
                        break;
                    case "SkillWeights":
                        ReadList(xmlReader, skillWeights, ReadSkillWeight);
                        break;
                    case "StatLimits":
                        ReadList(xmlReader, statLimits, ReadStatLimit);
                        break;
                    case "StatWeights":
                        ReadList(xmlReader, statWeights, ReadStatWeight);
                        break;
                    default:
                        Log.Warning($"Equipment Manager: Unknown Role property '{xmlReader.Name}'");
                        if (!xmlReader.Read()) { break; }
                        break;
                }
            }
            var role = new Role(id, label, priority, primaryRuleType, primaryRangedWeaponRuleId,
                primaryMeleeWeaponRuleId, rangedSidearmRules, meleeSidearmRules, toolRuleId, pawnTraits,
                pawnWorkCapacities, dropUnassignedWeapons, passionLimits, pawnCapacityLimits, pawnCapacityWeights,
                skillLimits, skillWeights, statLimits, statWeights,
                secondaryRuleType, secondaryRangedWeaponRuleId, secondaryMeleeWeaponRuleId)
            {
                IsDisabled = isDisabled
            };
            target.Add(role);
        }

        // ───  Melee / Ranged / WorkType ───────────────────────────────

        private static void ReadMeleeWeaponRuleData(XmlReader xmlReader, List<MeleeWeaponRule> target)
        {
            if (!xmlReader.ReadToFollowing("li") || !xmlReader.Read()) { return; }
            var id = 0;
            var label = string.Empty;
            var isProtected = false;
            var statWeights = new List<StatWeight>();
            var statLimits = new List<StatLimit>();
            var whitelistedItemsDefNames = new HashSet<string>();
            var blacklistedItemsDefNames = new HashSet<string>();
            var equipMode = ItemRule.WeaponEquipMode.BestOne;
            bool? usableWithShields = null;
            bool? rottable = null;
            while (true)
            {
                if (xmlReader.NodeType != XmlNodeType.Element || xmlReader.IsEmptyElement)
                {
                    if (!xmlReader.Read()) { break; }
                    continue;
                }
                switch (xmlReader.Name)
                {
                    case "Id":
                        id = xmlReader.ReadElementContentAsInt();
                        break;
                    case "Label":
                        label = xmlReader.ReadElementContentAsString();
                        break;
                    case "Protected":
                        isProtected = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    case "StatWeights":
                        ReadList(xmlReader, statWeights, ReadStatWeight);
                        break;
                    case "StatLimits":
                        ReadList(xmlReader, statLimits, ReadStatLimit);
                        break;
                    case "WhitelistedItemsDefNames":
                        ReadStringSet(xmlReader, whitelistedItemsDefNames);
                        break;
                    case "BlacklistedItemsDefNames":
                        ReadStringSet(xmlReader, blacklistedItemsDefNames);
                        break;
                    case "EquipMode":
                        _ = Enum.TryParse(xmlReader.ReadElementContentAsString(), out equipMode);
                        break;
                    case "UsableWithShields":
                        usableWithShields = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    case "Rottable":
                        rottable = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    default:
                        Log.Warning($"Equipment Manager: Unknown MeleeWeaponRule property '{xmlReader.Name}'");
                        if (!xmlReader.Read()) { break; }
                        break;
                }
            }
            target.Add(new MeleeWeaponRule(id, label, isProtected, statWeights, statLimits,
                whitelistedItemsDefNames, blacklistedItemsDefNames, equipMode, usableWithShields, rottable));
        }

        private static void ReadRangedWeaponRuleData(XmlReader xmlReader, List<RangedWeaponRule> target)
        {
            if (!xmlReader.ReadToFollowing("li") || !xmlReader.Read()) { return; }
            var id = 0;
            var label = string.Empty;
            var isProtected = false;
            var statWeights = new List<StatWeight>();
            var statLimits = new List<StatLimit>();
            var whitelistedItemsDefNames = new HashSet<string>();
            var blacklistedItemsDefNames = new HashSet<string>();
            var equipMode = ItemRule.WeaponEquipMode.BestOne;
            bool? explosive = null;
            bool? manualCast = null;
            var ammoCount = 0;
            while (true)
            {
                if (xmlReader.NodeType != XmlNodeType.Element || xmlReader.IsEmptyElement)
                {
                    if (!xmlReader.Read()) { break; }
                    continue;
                }
                switch (xmlReader.Name)
                {
                    case "Id":
                        id = xmlReader.ReadElementContentAsInt();
                        break;
                    case "Label":
                        label = xmlReader.ReadElementContentAsString();
                        break;
                    case "Protected":
                        isProtected = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    case "StatWeights":
                        ReadList(xmlReader, statWeights, ReadStatWeight);
                        break;
                    case "StatLimits":
                        ReadList(xmlReader, statLimits, ReadStatLimit);
                        break;
                    case "WhitelistedItemsDefNames":
                        ReadStringSet(xmlReader, whitelistedItemsDefNames);
                        break;
                    case "BlacklistedItemsDefNames":
                        ReadStringSet(xmlReader, blacklistedItemsDefNames);
                        break;
                    case "EquipMode":
                        _ = Enum.TryParse(xmlReader.ReadElementContentAsString(), out equipMode);
                        break;
                    case "Explosive":
                        explosive = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    case "ManualCast":
                        manualCast = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    case "AmmoCount":
                        ammoCount = xmlReader.ReadElementContentAsInt();
                        break;
                    default:
                        Log.Warning($"Equipment Manager: Unknown RangedWeaponRule property '{xmlReader.Name}'");
                        if (!xmlReader.Read()) { break; }
                        break;
                }
            }
            target.Add(new RangedWeaponRule(id, label, isProtected, statWeights, statLimits,
                whitelistedItemsDefNames, blacklistedItemsDefNames, equipMode, explosive, manualCast, ammoCount));
        }

        private static void ReadWorkTypeRuleData(XmlReader xmlReader, List<WorkTypeRule> target)
        {
            if (!xmlReader.ReadToFollowing("li") || !xmlReader.Read()) { return; }
            var workTypeDefName = string.Empty;
            var statWeights = new List<StatWeight>();
            while (true)
            {
                if (xmlReader.NodeType != XmlNodeType.Element || xmlReader.IsEmptyElement)
                {
                    if (!xmlReader.Read()) { break; }
                    continue;
                }
                switch (xmlReader.Name)
                {
                    case "WorkTypeDefName":
                        workTypeDefName = xmlReader.ReadElementContentAsString();
                        break;
                    case "StatWeights":
                        ReadList(xmlReader, statWeights, ReadStatWeight);
                        break;
                    default:
                        Log.Warning($"Equipment Manager: Unknown WorkTypeRule property '{xmlReader.Name}'");
                        if (!xmlReader.Read()) { break; }
                        break;
                }
            }
            target.Add(new WorkTypeRule(workTypeDefName, statWeights));
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private static void ReadList<T>(XmlReader xmlReader, ICollection<T> target, Func<XmlReader, T> readItem)
        {
            _ = xmlReader.Read();
            while (xmlReader.Name == "li" && xmlReader.NodeType == XmlNodeType.Element)
            {
                _ = xmlReader.Read();
                target.Add(readItem(xmlReader));
                xmlReader.ReadEndElement();
            }
            xmlReader.ReadEndElement();
        }

        private static void ReadIntList(XmlReader xmlReader, ICollection<int> target)
        {
            _ = xmlReader.Read();
            while (xmlReader.Name == "li" && xmlReader.NodeType == XmlNodeType.Element)
            {
                target.Add(xmlReader.ReadElementContentAsInt());
            }
            xmlReader.ReadEndElement();
        }

        private static void ReadStringSet(XmlReader xmlReader, ISet<string> target)
        {
            _ = xmlReader.Read();
            while (xmlReader.Name == "li" && xmlReader.NodeType == XmlNodeType.Element)
            {
                _ = target.Add(xmlReader.ReadElementContentAsString());
            }
            xmlReader.ReadEndElement();
        }

        private static void ReadStringBoolDictionary(XmlReader xmlReader, IDictionary<string, bool> target)
        {
            var keys = new List<string>();
            var values = new List<string>();
            if (xmlReader.Name == "keys" && xmlReader.NodeType == XmlNodeType.Element)
            {
                if (xmlReader.IsEmptyElement) { _ = xmlReader.Read(); }
                else
                {
                    _ = xmlReader.Read();
                    while (xmlReader.Name == "li" && xmlReader.NodeType == XmlNodeType.Element)
                    {
                        keys.Add(xmlReader.ReadElementContentAsString());
                    }
                    xmlReader.ReadEndElement();
                }
            }
            if (xmlReader.Name == "values" && xmlReader.NodeType == XmlNodeType.Element)
            {
                if (xmlReader.IsEmptyElement) { _ = xmlReader.Read(); }
                else
                {
                    _ = xmlReader.Read();
                    while (xmlReader.Name == "li" && xmlReader.NodeType == XmlNodeType.Element)
                    {
                        values.Add(xmlReader.ReadElementContentAsString());
                    }
                    xmlReader.ReadEndElement();
                }
            }
            for (var i = 0; i < keys.Count && i < values.Count; i++)
            {
                target[keys[i]] = bool.Parse(values[i]);
            }
        }

        private static PassionLimit ReadPassionLimit(XmlReader xmlReader)
        {
            var skillDefName = string.Empty;
            var passionValue = PassionValue.None;
            var keepParsing = true;
            do
            {
                switch (xmlReader.Name)
                {
                    case "SkillDefName":
                        skillDefName = xmlReader.ReadElementContentAsString();
                        break;
                    case "Value":
                        _ = Enum.TryParse(xmlReader.ReadElementContentAsString(), out passionValue);
                        break;
                    default:
                        keepParsing = false;
                        break;
                }
            } while (keepParsing);
            return new PassionLimit(skillDefName) {Value = passionValue};
        }

        private static PawnCapacityLimit ReadPawnCapacityLimit(XmlReader xmlReader)
        {
            var pawnCapacityDefName = string.Empty;
            float? minValue = null;
            float? maxValue = null;
            var keepParsing = true;
            do
            {
                switch (xmlReader.Name)
                {
                    case "PawnCapacityDefName":
                        pawnCapacityDefName = xmlReader.ReadElementContentAsString();
                        break;
                    case "MinValue":
                        minValue = xmlReader.ReadElementContentAsFloat();
                        break;
                    case "MaxValue":
                        maxValue = xmlReader.ReadElementContentAsFloat();
                        break;
                    default:
                        keepParsing = false;
                        break;
                }
            } while (keepParsing);
            return new PawnCapacityLimit(pawnCapacityDefName, minValue, maxValue);
        }

        private static PawnCapacityWeight ReadPawnCapacityWeight(XmlReader xmlReader)
        {
            var pawnCapacityDefName = string.Empty;
            var weight = 0f;
            var keepParsing = true;
            do
            {
                switch (xmlReader.Name)
                {
                    case "PawnCapacityDefName":
                        pawnCapacityDefName = xmlReader.ReadElementContentAsString();
                        break;
                    case "Weight":
                        weight = xmlReader.ReadElementContentAsFloat();
                        break;
                    default:
                        keepParsing = false;
                        break;
                }
            } while (keepParsing);
            return new PawnCapacityWeight(pawnCapacityDefName, weight);
        }

        private static SkillLimit ReadSkillLimit(XmlReader xmlReader)
        {
            var skillDefName = string.Empty;
            float? minValue = null;
            float? maxValue = null;
            var keepParsing = true;
            do
            {
                switch (xmlReader.Name)
                {
                    case "SkillDefName":
                        skillDefName = xmlReader.ReadElementContentAsString();
                        break;
                    case "MinValue":
                        minValue = xmlReader.ReadElementContentAsFloat();
                        break;
                    case "MaxValue":
                        maxValue = xmlReader.ReadElementContentAsFloat();
                        break;
                    default:
                        keepParsing = false;
                        break;
                }
            } while (keepParsing);
            return new SkillLimit(skillDefName, minValue, maxValue);
        }

        private static SkillWeight ReadSkillWeight(XmlReader xmlReader)
        {
            var skillDefName = string.Empty;
            var weight = 0f;
            var keepParsing = true;
            do
            {
                switch (xmlReader.Name)
                {
                    case "SkillDefName":
                        skillDefName = xmlReader.ReadElementContentAsString();
                        break;
                    case "Weight":
                        weight = xmlReader.ReadElementContentAsFloat();
                        break;
                    default:
                        keepParsing = false;
                        break;
                }
            } while (keepParsing);
            return new SkillWeight(skillDefName, weight);
        }

        private static StatLimit ReadStatLimit(XmlReader xmlReader)
        {
            var statDefName = string.Empty;
            float? minValue = null;
            float? maxValue = null;
            var keepParsing = true;
            do
            {
                switch (xmlReader.Name)
                {
                    case "StatDefName":
                        statDefName = xmlReader.ReadElementContentAsString();
                        break;
                    case "MinValue":
                        minValue = xmlReader.ReadElementContentAsFloat();
                        break;
                    case "MaxValue":
                        maxValue = xmlReader.ReadElementContentAsFloat();
                        break;
                    default:
                        keepParsing = false;
                        break;
                }
            } while (keepParsing);
            return new StatLimit(statDefName, minValue, maxValue);
        }

        private static StatWeight ReadStatWeight(XmlReader xmlReader)
        {
            var statDefName = string.Empty;
            var isProtected = false;
            var weight = 0f;
            var keepParsing = true;
            do
            {
                switch (xmlReader.Name)
                {
                    case "StatDefName":
                        statDefName = xmlReader.ReadElementContentAsString();
                        break;
                    case "Protected":
                        isProtected = bool.Parse(xmlReader.ReadElementContentAsString());
                        break;
                    case "Weight":
                        weight = xmlReader.ReadElementContentAsFloat();
                        break;
                    default:
                        keepParsing = false;
                        break;
                }
            } while (keepParsing);
            return new StatWeight(statDefName, weight, isProtected);
        }
    }
}
