using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    // Глобальное переназначение ролей и снаряжения для всей колонии.
    // Не трогает пешек, у которых роль выставлена игроком вручную
    // (PawnRole.Automatic == false).
    internal static class GlobalReassigner
    {
        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        // Кэшируем StatDef'ы один раз, чтобы избежать поиска при каждом вызове.
        private static StatDef _shootingAccuracyPawn;
        private static StatDef ShootingAccuracyPawn =>
            _shootingAccuracyPawn ??= DefDatabase<StatDef>.GetNamedSilentFail("ShootingAccuracyPawn");

        // Урон в ближнем бою без оружия. Если игра не предоставляет MeleeDPS,
        // используем MeleeHitChance как fallback (как указано в задаче).
        private static StatDef _meleeDps;
        private static StatDef MeleeDpsStat =>
            _meleeDps ??= DefDatabase<StatDef>.GetNamedSilentFail("MeleeDPS")
                       ?? DefDatabase<StatDef>.GetNamedSilentFail("MeleeHitChance");

        public static void GlobalReassignAll(Map map)
        {
            if (map == null) { return; }
            var em = EquipmentManager;
            if (em == null) { return; }
            var mapComp = map.GetComponent<EquipmentManagerMapComponent>();
            if (mapComp == null) { return; }

            // 1) Все взрослые поселенцы колонии, которых ведёт мод.
            var allColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.Faction == Faction.OfPlayer
                            && !p.HasExtraHomeFaction()
                            && !p.HasExtraMiniFaction()
                            && p.GuestStatus == null
                            && p.DevelopmentalStage.Adult()
                            && !p.IsQuestLodger())
                .ToList();
            if (allColonists.Count == 0) { return; }

            // Разделение по способу назначения роли:
            //   • вручную (PawnRole.Automatic == false) — не трогаем,
            //     просто обновляем им снаряжение;
            //   • автоматически (или роль ещё не назначена) — переназначаем.
            var manualPawns = new List<Pawn>();
            var autoPawns   = new List<Pawn>();
            foreach (var p in allColonists)
            {
                var pr = em.GetPawnRole(p);
                if (pr != null && !pr.Automatic) { manualPawns.Add(p); }
                else                              { autoPawns.Add(p); }
            }

            // 2) Шаг 1 — переназначение ролей для авто-пешек по приоритету.
            ReassignRolesByPriority(autoPawns);

            // 3) Шаги 2 и 3 — оружие и инструменты.
            // ForceUpdateForPawn для AutoRole-пешек запускает конкурентный
            // алгоритм UpdateRoles(), который переопределит наши решения.
            // Чтобы наш приоритетный выбор «прилип», временно помечаем
            // только что назначенные роли как manual (Automatic = false),
            // прогоняем ForceUpdateForPawn, затем восстанавливаем флаг.
            //
            // Сама логика снаряжения (поиск, скоринг, резервирование,
            // PersonalLoadout, инструменты) остаётся за существующим
            // ForceUpdateForPawn — никаких новых механизмов не вводим.

            var revertToAuto = new List<Pawn>(autoPawns.Count);
            foreach (var p in autoPawns)
            {
                var pr = em.GetPawnRole(p);
                if (pr == null) { continue; }
                if (pr.Automatic)
                {
                    pr.Automatic = false;       // временно «manual» — чтобы ForceUpdateForPawn не пересчитал роль
                    revertToAuto.Add(p);
                }
            }

            try
            {
                // Обработка по группам ролей в порядке убывания приоритета;
                // внутри роли — по релевантному стату (лучшая пешка первой,
                // получает лучшее оружие первой за счёт встроенной системы
                // резервирования _pawnCache.ReservedWeapons / AssignedWeapons).
                var ordered = OrderedForEquipmentAssignment(allColonists, em);
                foreach (var p in ordered)
                {
                    mapComp.ForceUpdateForPawn(p);
                }
            }
            finally
            {
                // Восстанавливаем Automatic-флаг — иначе пешки навсегда
                // потеряют авто-режим назначения роли.
                foreach (var p in revertToAuto)
                {
                    var pr = em.GetPawnRole(p);
                    if (pr != null) { pr.Automatic = true; }
                }
            }
        }

        // ─── Шаг 1: распределение ролей по приоритету ────────────────────────
        private static void ReassignRolesByPriority(List<Pawn> autoPawns)
        {
            var em = EquipmentManager;

            // Перебираем роли в порядке убывания Priority; первая роль,
            // условиям которой пешка соответствует, и есть лучшая.
            var rolesByPriority = em.GetRoles()
                .Where(r => r.Priority > 0f)
                .OrderByDescending(r => r.Priority)
                .ToList();

            foreach (var pawn in autoPawns)
            {
                Role bestRole = null;
                foreach (var role in rolesByPriority)
                {
                    if (role.IsAvailable(pawn))
                    {
                        bestRole = role;
                        break;
                    }
                }
                em.SetPawnRole(pawn, bestRole, automatic: true);
            }
        }

        // ─── Сортировка пешек внутри роли ────────────────────────────────────
        // Лучшая (по релевантному стату) — первой.
        private static IEnumerable<Pawn> SortPawnsForRole(IEnumerable<Pawn> pawns, Role role)
        {
            if (role == null) { return pawns; }
            switch (role.PrimaryRuleType)
            {
                case Role.PrimaryWeaponType.RangedWeapon:
                {
                    var stat = ShootingAccuracyPawn;
                    return stat == null
                        ? pawns
                        : pawns.OrderByDescending(p => p.GetStatValue(stat));
                }
                case Role.PrimaryWeaponType.MeleeWeapon:
                {
                    var stat = MeleeDpsStat;
                    return stat == null
                        ? pawns
                        : pawns.OrderByDescending(p => p.GetStatValue(stat));
                }
                default:
                    return pawns;
            }
        }

        // Все пешки, упорядоченные «лучшая роль → лучшая пешка»: внешняя
        // сортировка по убыванию Priority роли, внутренняя — по статy.
        private static IEnumerable<Pawn> OrderedForEquipmentAssignment(
            IEnumerable<Pawn> pawns, EquipmentManagerGameComponent em)
        {
            var groups = pawns
                .Select(p => new { Pawn = p, Role = em.GetRole(p) })
                .GroupBy(x => x.Role)
                .OrderByDescending(g => g.Key?.Priority ?? float.MinValue);

            foreach (var g in groups)
            {
                foreach (var p in SortPawnsForRole(g.Select(x => x.Pawn), g.Key))
                {
                    yield return p;
                }
            }
        }
    }
}
