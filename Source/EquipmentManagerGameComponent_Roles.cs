using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Verse;

namespace EquipmentManager
{
    internal partial class EquipmentManagerGameComponent
    {
        private List<Role> _roles;
        private List<PawnRole> _pawnRoles;

        public Role AddRole()
        {
            _roles ??= new List<Role>(Role.DefaultRoles);
            var id = _roles.Any() ? _roles.Max(r => r.Id) + 1 : 1;
            var role = new Role(id) { Label = $"{id}" };
            _roles.Add(role);
            return role;
        }

        public void AddRole(Role role)
        {
            _roles ??= new List<Role>(Role.DefaultRoles);
            var existing = _roles.FirstOrDefault(r => r.Id == role.Id);
            if (existing != null) { _ = _roles.Remove(existing); }
            _roles.Add(role);
        }

        public Role CopyRole(Role role)
        {
            var newRole = AddRole();
            newRole.Label = $"{role.Label} 2";
            newRole.Priority = role.Priority;
            newRole.PrimaryRuleType = role.PrimaryRuleType;
            newRole.PrimaryMeleeWeaponRuleId = role.PrimaryMeleeWeaponRuleId;
            newRole.PrimaryRangedWeaponRuleId = role.PrimaryRangedWeaponRuleId;
            newRole.SecondaryRuleType = role.SecondaryRuleType;
            newRole.SecondaryMeleeWeaponRuleId = role.SecondaryMeleeWeaponRuleId;
            newRole.SecondaryRangedWeaponRuleId = role.SecondaryRangedWeaponRuleId;
            newRole.RangedSidearmRules.AddRange(role.RangedSidearmRules);
            newRole.MeleeSidearmRules.AddRange(role.MeleeSidearmRules);
            newRole.ToolRuleId = role.ToolRuleId;
            newRole.DropUnassignedWeapons = role.DropUnassignedWeapons;
            foreach (var passionLimit in role.PassionLimits)
            {
                newRole.PassionLimits.Add(new PassionLimit(passionLimit.SkillDefName) { Value = passionLimit.Value });
            }
            foreach (var pawnCapacityLimit in role.PawnCapacityLimits)
            {
                newRole.PawnCapacityLimits.Add(new PawnCapacityLimit(pawnCapacityLimit.PawnCapacityDefName,
                    pawnCapacityLimit.MinValue, pawnCapacityLimit.MaxValue));
            }
            foreach (var pawnCapacityWeight in role.PawnCapacityWeights)
            {
                newRole.PawnCapacityWeights.Add(new PawnCapacityWeight(pawnCapacityWeight.PawnCapacityDefName,
                    pawnCapacityWeight.Weight));
            }
            foreach (var pawnTrait in role.PawnTraits) { newRole.PawnTraits.Add(pawnTrait.Key, pawnTrait.Value); }
            foreach (var pawnWorkCapacity in role.PawnWorkCapacities)
            {
                newRole.PawnWorkCapacities.Add(pawnWorkCapacity.Key, pawnWorkCapacity.Value);
            }
            foreach (var skillLimit in role.SkillLimits)
            {
                newRole.SkillLimits.Add(new SkillLimit(skillLimit.SkillDefName, skillLimit.MinValue,
                    skillLimit.MaxValue));
            }
            foreach (var skillWeight in role.SkillWeights)
            {
                newRole.SkillWeights.Add(new SkillWeight(skillWeight.SkillDefName, skillWeight.Weight));
            }
            foreach (var statLimit in role.StatLimits)
            {
                newRole.StatLimits.Add(new StatLimit(statLimit.StatDefName, statLimit.MinValue, statLimit.MaxValue));
            }
            foreach (var statWeight in role.StatWeights)
            {
                newRole.StatWeights.Add(new StatWeight(statWeight.StatDefName, statWeight.Weight,
                    statWeight.Protected));
            }
            return newRole;
        }

        public void DeleteRole(Role role)
        {
            if (role == null || role.IsSystemRole) { return; }
            _pawnRoles ??= new List<PawnRole>();
            foreach (var pawnRole in _pawnRoles.Where(pr => pr.RoleId == role.Id))
            {
                pawnRole.RoleId = null;
            }
            _roles ??= new List<Role>(Role.DefaultRoles);
            _ = _roles.Remove(role);
        }

        private void ExposeData_Roles()
        {
            // XML-теги "Loadouts" и "PawnLoadouts" сохранены для совместимости с существующими сейвами
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _ = _pawnRoles?.RemoveAll(pr => pr.Pawn?.Destroyed ?? true);
                // Системные роли не пишем в сейв — они всегда генерируются из кода
                _ = _roles?.RemoveAll(r => Role.IsSystemId(r.Id));
            }
            Scribe_Collections.Look(ref _roles, "Loadouts", LookMode.Deep);
            Scribe_Collections.Look(ref _pawnRoles, "PawnLoadouts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureSystemRoles();
            }
        }

        public Role GetRole(int? id)
        {
            return id == null ? null : GetRoles().FirstOrDefault(r => r.Id == id);
        }

        public Role GetRole([NotNull] Pawn pawn)
        {
            if (pawn == null) { throw new ArgumentNullException(nameof(pawn)); }
            _pawnRoles ??= new List<PawnRole>();
            return GetRole(GetPawnRole(pawn)?.RoleId);
        }

        public IEnumerable<Role> GetRoles()
        {
            _roles ??= new List<Role>(Role.DefaultRoles);
            // Системные роли всегда первыми — независимо от сейва
            return Role.SystemRoles.Concat(_roles);
        }

        // ── Вспомогательные методы для системных ролей ──────────────────────

        /// <summary>Назначить пешке роль "OFF" — мод перестаёт её обрабатывать.</summary>
        public void SetPawnRoleOff(Pawn pawn)
        {
            SetPawnRole(pawn, GetRole(Role.SystemIdOff), automatic: false);
        }

        /// <summary>Вернуть пешку под автоматическое управление модом.</summary>
        public void SetPawnRoleAuto(Pawn pawn)
        {
            SetPawnRole(pawn, GetRole(Role.SystemIdAuto), automatic: true);
        }

        /// <summary>
        /// Истина, если пешка находится в состоянии "Авто" (мод назначит роль при следующем тике).
        /// Это: явная роль Авто (-2) ИЛИ RoleId == null при Automatic == true.
        /// </summary>
        public bool IsPawnRoleAuto(Pawn pawn)
        {
            var pr = GetPawnRole(pawn);
            if (pr == null) { return true; }
            return pr.Automatic && (pr.RoleId == null || pr.RoleId == Role.SystemIdAuto);
        }

        /// <summary>Истина, если пешка выключена из обработки (роль OFF).</summary>
        public bool IsPawnRoleOff(Pawn pawn)
        {
            var pr = GetPawnRole(pawn);
            return pr != null && !pr.Automatic && pr.RoleId == Role.SystemIdOff;
        }

        /// <summary>
        /// Удаляет системные Id из _roles (миграция со старых версий)
        /// и гарантирует, что авто-пешки с RoleId == SystemIdAuto
        /// сохраняют Automatic = true.
        /// </summary>
        private void EnsureSystemRoles()
        {
            if (_roles == null) { return; }
            _ = _roles.RemoveAll(r => Role.IsSystemId(r.Id));
            if (_pawnRoles == null) { return; }
            foreach (var pr in _pawnRoles)
            {
                if (pr.RoleId == Role.SystemIdAuto) { pr.Automatic = true; }
                if (pr.RoleId == Role.SystemIdOff)  { pr.Automatic = false; }
            }
        }

        public PawnRole GetPawnRole([NotNull] Pawn pawn)
        {
            if (pawn == null) { throw new ArgumentNullException(nameof(pawn)); }
            _pawnRoles ??= new List<PawnRole>();
            var pawnRole = _pawnRoles.FirstOrDefault(pr => pr.Pawn != null &&
                pr.Pawn.thingIDNumber == pawn.thingIDNumber);
            if (pawnRole != null) { return pawnRole; }
            pawnRole = new PawnRole { Pawn = pawn, RoleId = null, Automatic = true };
            _pawnRoles.Add(pawnRole);
            return pawnRole;
        }

        public void SetPawnRole([NotNull] Pawn pawn, Role role, bool automatic)
        {
            if (pawn == null) { throw new ArgumentNullException(nameof(pawn)); }
            _pawnRoles ??= new List<PawnRole>();
            var pawnRole = _pawnRoles.FirstOrDefault(pr => pr.Pawn != null &&
                pr.Pawn.thingIDNumber == pawn.thingIDNumber);
            if (pawnRole != null)
            {
                pawnRole.RoleId = role?.Id;
                pawnRole.Automatic = automatic;
            }
            else
            {
                _pawnRoles.Add(new PawnRole { Pawn = pawn, RoleId = role?.Id, Automatic = automatic });
            }
        }
    }
}
