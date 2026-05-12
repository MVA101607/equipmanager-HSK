using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    // Глобальное переназначение ролей и снаряжения для всей колонии.
    // Не трогает пешек, у которых роль выставлена игроком вручную
    // (PawnRole.Automatic == false).
    //
    // Алгоритм распределения ролей (пропорциональный):
    //   1. Считаем сумму приоритетов всех активных ролей — это 100%.
    //   2. Вычисляем целевое количество пешек для каждой роли методом
    //      Хэмилтона (largest remainder), чтобы сумма была ровно N пешек.
    //   3. Пешкам, подходящим только под одну роль, она назначается безусловно.
    //   4. «Универсальные» пешки (подходят под несколько ролей) сортируются
    //      от наименее гибких к наиболее и назначаются туда, где дефицит
    //      (target - assigned) максимален; при равном дефиците — роль с
    //      наивысшим приоритетом.
    //   5. Если ни одна роль не подходит — пешка получает null (нет роли).
    internal static class GlobalReassigner
    {
        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        private static StatDef _shootingAccuracyPawn;
        private static StatDef ShootingAccuracyPawn =>
            _shootingAccuracyPawn ??= DefDatabase<StatDef>.GetNamedSilentFail("ShootingAccuracyPawn");

        private static StatDef _meleeDps;
        private static StatDef MeleeDpsStat =>
            _meleeDps ??= DefDatabase<StatDef>.GetNamedSilentFail("MeleeDPS")
                       ?? DefDatabase<StatDef>.GetNamedSilentFail("MeleeHitChance");

        // ─── Внешний вход: кнопка Refresh / DebugAction ──────────────────────
        // Делает полный цикл: назнач��ние ролей + выдача снаряжения всем.
        public static void GlobalReassignAll(Map map)
        {
            if (map == null) { return; }
            var em = EquipmentManager;
            if (em == null) { return; }
            var mapComp = map.GetComponent<EquipmentManagerMapComponent>();
            if (mapComp == null) { return; }

            var allColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.Faction == Faction.OfPlayer
                            && !p.HasExtraHomeFaction()
                            && !p.HasExtraMiniFaction()
                            && p.GuestStatus == null
                            && p.DevelopmentalStage.Adult()
                            && !p.IsQuestLodger())
                .ToList();
            if (allColonists.Count == 0) { return; }

            var autoPawns   = allColonists.Where(p => em.GetPawnRole(p)?.Automatic != false).ToList();
            var manualPawns = allColonists.Except(autoPawns).ToList();

            // Шаг 1: пропорциональное распределение ролей для авто-пешек.
            ReassignProportional(autoPawns, em);

            // Шаг 2: временно фиксируем авто-роли как manual, чтобы
            // ForceUpdateForPawn не пересчитал их при выдаче снаряжения.
            var revertToAuto = new List<Pawn>(autoPawns.Count);
            foreach (var p in autoPawns)
            {
                var pr = em.GetPawnRole(p);
                if (pr == null) { continue; }
                if (pr.Automatic) { pr.Automatic = false; revertToAuto.Add(p); }
            }

            try
            {
                foreach (var p in OrderedForEquipment(allColonists, em))
                {
                    mapComp.ForceUpdateForPawn(p);
                }
            }
            finally
            {
                foreach (var p in revertToAuto)
                {
                    var pr = em.GetPawnRole(p);
                    if (pr != null) { pr.Automatic = true; }
                }
            }
        }

        // ─── Пропорциональное распределение ролей ────────────────────────────
        // Вызывается:
        //   • GlobalReassignAll()         — кнопка Refresh
        //   • EquipmentManagerMapComponent.UpdateRoles() — ежечасный тик
        //   • ForceUpdateForPawn()        — принудительное обновление одной пешки
        //
        // autoPawns — список авто-пешек (Automatic == true или роль ещё не назначена).
        // Метод записывает результат через em.SetPawnRole(automatic:true).
        public static void ReassignProportional(
            List<Pawn> autoPawns,
            EquipmentManagerGameComponent em)
        {
            if (autoPawns == null || autoPawns.Count == 0) { return; }

            var activeRoles = em.GetRoles()
                .Where(r => r.Priority > 0f)
                .OrderByDescending(r => r.Priority)
                .ToList();

            if (activeRoles.Count == 0)
            {
                foreach (var p in autoPawns) { em.SetPawnRole(p, null, automatic: true); }
                return;
            }

            // Для каждой пешки — список подходящих ролей.
            var eligible = autoPawns.ToDictionary(
                p => p,
                p => activeRoles.Where(r => r.IsAvailable(p)).ToList());

            // ── Целевые квоты: метод Хэмилтона ──────────────────────────────
            var totalPriority = activeRoles.Sum(r => r.Priority);
            var   totalPawns    = autoPawns.Count;

            var exact   = activeRoles.ToDictionary(r => r,
                r => r.Priority / totalPriority * totalPawns);
            var targets = activeRoles.ToDictionary(r => r,
                r => (int)System.Math.Floor(exact[r]));

            // Распределяем остаток по наибольшим дробным частям.
            var remainder = totalPawns - targets.Values.Sum();
            foreach (var r in activeRoles
                         .OrderByDescending(r => exact[r] - System.Math.Floor(exact[r]))
                         .Take(remainder))
            {
                targets[r]++;
            }

            var assigned = activeRoles.ToDictionary(r => r, _ => 0);
            var result   = new Dictionary<Pawn, Role>(autoPawns.Count);

            // ── Шаг A: пешки с единственной подходящей ролью ────────────────
            var contested = new List<Pawn>();
            foreach (var pawn in autoPawns)
            {
                var el = eligible[pawn];
                if (el.Count == 0)
                {
                    result[pawn] = null;
                }
                else if (el.Count == 1)
                {
                    result[pawn] = el[0];
                    assigned[el[0]]++;
                }
                else
                {
                    contested.Add(pawn);
                }
            }

            // ── Шаг Б: «универсальные» пешки ────────────────────────────────
            // Сортировка: сначала те, у кого меньше вариантов ролей.
            contested.Sort((a, b) => eligible[a].Count.CompareTo(eligible[b].Count));

            foreach (var pawn in contested)
            {
                Role best         = null;
                var  bestDeficit  = int.MinValue;
                var bestPriority = float.MinValue;

                foreach (var role in eligible[pawn])
                {
                    var deficit = targets[role] - assigned[role];
                    if (deficit > bestDeficit ||
                        (deficit == bestDeficit && role.Priority > bestPriority))
                    {
                        best          = role;
                        bestDeficit   = deficit;
                        bestPriority  = role.Priority;
                    }
                }

                result[pawn] = best;
                if (best != null) { assigned[best]++; }
            }

            // Применяем.
            foreach (var p in autoPawns)
            {
                _ = result.TryGetValue(p, out var role);
                em.SetPawnRole(p, role, automatic: true);
            }
        }

        // ─── Сортировка пешек для выдачи снаряжения ──────────────────────────
        // Порядок: убывание Priority роли → убывание релевантного стата пешки.
        // Гарантирует, что лучшая пешка роли получает лучшее оружие первой.
        public static IEnumerable<Pawn> OrderedForEquipment(
            IEnumerable<Pawn> pawns,
            EquipmentManagerGameComponent em)
        {
            var groups = pawns
                .Select(p => new { Pawn = p, Role = em.GetRole(p) })
                .GroupBy(x => x.Role)
                .OrderByDescending(g => g.Key?.Priority ?? float.MinValue);

            foreach (var g in groups)
            {
                foreach (var p in SortByRoleStat(g.Select(x => x.Pawn), g.Key))
                {
                    yield return p;
                }
            }
        }

        private static IEnumerable<Pawn> SortByRoleStat(IEnumerable<Pawn> pawns, Role role)
        {
            if (role == null) { return pawns; }
            switch (role.PrimaryRuleType)
            {
                case Role.PrimaryWeaponType.RangedWeapon:
                {
                    var stat = ShootingAccuracyPawn;
                    return stat == null ? pawns
                        : pawns.OrderByDescending(p => p.GetStatValue(stat));
                }
                case Role.PrimaryWeaponType.MeleeWeapon:
                {
                    var stat = MeleeDpsStat;
                    return stat == null ? pawns
                        : pawns.OrderByDescending(p => p.GetStatValue(stat));
                }
                default: return pawns;
            }
        }
    }
}
