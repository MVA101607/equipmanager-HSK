using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    [UsedImplicitly]
    internal class EquipmentManagerMapComponent : MapComponent
    {
        private static EquipmentManagerGameComponent _equipmentManager;
        private readonly RimworldTime _updateTime = new(-1, -1, -1);
        private HashSet<Pawn>       _allPawns   = new();
        private HashSet<PawnCache>  _pawnCache  = new();
        private int                 _pawnProcessingIndex;

        public EquipmentManagerMapComponent(Map map) : base(map) { }

        private static EquipmentManagerGameComponent EquipmentManager =>
            _equipmentManager ??= Current.Game.GetComponent<EquipmentManagerGameComponent>();

        // ─────────────────────────────────────────────────────────────────────
        // Score текущего оружия пешки для сравнения с кандидатом.
        // Возвращает 0 если пешка безоружна или тип оружия не совпадает с rule.
        // ─────────────────────────────────────────────────────────────────────
        private float GetCurrentWeaponScore(PawnCache pawn, MeleeWeaponRule rule)
        {
            var primary = pawn.Pawn.equipment?.Primary;
            if (primary == null || !primary.def.IsMeleeWeapon) { return 0f; }
            return rule.GetThingScore(primary, _updateTime);
        }

        private float GetCurrentWeaponScore(PawnCache pawn, RangedWeaponRule rule)
        {
            var primary = pawn.Pawn.equipment?.Primary;
            if (primary == null || !primary.def.IsRangedWeapon) { return 0f; }
            return rule.GetThingScore(primary, _updateTime);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Назначение дальнего основного оружия
        // ─────────────────────────────────────────────────────────────────────
        private bool AssignPrimaryRangedWeapon(PawnCache pawn)
        {
            if (pawn.AssignedLoadout.PrimaryRangedWeaponRuleId == null) { return false; }
            var rule = EquipmentManager.GetRangedWeaponRule(
                (int)pawn.AssignedLoadout.PrimaryRangedWeaponRuleId);
            if (rule == null) { return false; }

            EquipmentManager.LogMessage(
                $"[EM] AssignPrimaryRangedWeapon for {pawn.Pawn.LabelShortCap}");

            var availableWeapons = rule.GetCurrentlyAvailableItems(map, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn && pc.AssignedWeapons.ContainsKey(thing)));
            _ = availableWeapons.RemoveAll(thing =>
                !EquipmentUtility.CanEquip(thing, pawn.Pawn) ||
                (pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap != null &&
                    !pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[thing.Position]));
            if (availableWeapons.Count == 0) { return false; }

            var bestWeapon = availableWeapons
                .OrderByDescending(thing => rule.GetThingScore(thing, _updateTime))
                .ThenBy(thing => thing.GetHashCode())
                .FirstOrDefault();
            if (bestWeapon == null) { return false; }

            // Менять только если новое оружие превосходит текущее × RetentionBonus.
            var currentScore = GetCurrentWeaponScore(pawn, rule);
            var bestScore    = rule.GetThingScore(bestWeapon, _updateTime);
            if (currentScore > 0f && bestScore < currentScore * rule.RetentionBonus) { return false; }

            pawn.AssignedWeapons.Add(bestWeapon, "primary");

            var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
            if (pawnLoadout != null)
            {
                pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetPrimaryWeaponInPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnLoadout.ManagedPersonalLoadoutSlots);
            }

            UpdateAmmo(pawn, bestWeapon, rule);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Назначение ближнего основного оружия
        // ─────────────────────────────────────────────────────────────────────
        private bool AssignPrimaryMeleeWeapon(PawnCache pawn)
        {
            if (pawn.AssignedLoadout.PrimaryMeleeWeaponRuleId == null) { return false; }
            var rule = EquipmentManager.GetMeleeWeaponRule(
                (int)pawn.AssignedLoadout.PrimaryMeleeWeaponRuleId);
            if (rule == null) { return false; }

            EquipmentManager.LogMessage(
                $"[EM] AssignPrimaryMeleeWeapon for {pawn.Pawn.LabelShortCap}");

            var availableWeapons = rule.GetCurrentlyAvailableItems(map, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn && pc.AssignedWeapons.ContainsKey(thing)));
            _ = availableWeapons.RemoveAll(thing =>
                !EquipmentUtility.CanEquip(thing, pawn.Pawn) ||
                (pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap != null &&
                    !pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[thing.Position]));
            if (availableWeapons.Count == 0) { return false; }

            var bestWeapon = availableWeapons
                .OrderByDescending(thing => rule.GetThingScore(thing, _updateTime))
                .ThenBy(thing => thing.GetHashCode())
                .FirstOrDefault();
            if (bestWeapon == null) { return false; }

            var currentScore = GetCurrentWeaponScore(pawn, rule);
            var bestScore    = rule.GetThingScore(bestWeapon, _updateTime);
            if (currentScore > 0f && bestScore < currentScore * rule.RetentionBonus) { return false; }

            pawn.AssignedWeapons.Add(bestWeapon, "primary");

            var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
            if (pawnLoadout != null)
            {
                pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetPrimaryWeaponInPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnLoadout.ManagedPersonalLoadoutSlots);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Патроны
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateAmmo(PawnCache pawn, Thing weapon, RangedWeaponRule rule)
        {
            if (!CombatExtendedHelper.EnableAmmoSystem) { return; }

            var weaponCache = EquipmentManager.GetRangedWeaponCache(weapon, _updateTime);

            // Одноразовое оружие (граната, RPG) — само является боеприпасом.
            // Generic def для него не создаётся, кладём specific-слот, 5 штук.
            if (weaponCache.IsAmmo)
            {
                EquipmentManager.LogMessage(
                    $"[EM] {pawn.Pawn.LabelShortCap}: {weapon.LabelCapNoCount} is one-use ammo, assigning 5");
                _ = CEExtendedLoadoutHelper.SetAmmoInPersonalLoadout(
                    pawn.Pawn, weapon.def, 5);
                return;
            }

            // Обычное оружие с CompAmmoUser.
            var ammoDefs = weaponCache.AmmoTypes.ToList();
            if (ammoDefs.Count == 0) { return; }

            var magSize       = weaponCache.MagSize;
            var targetCount   = magSize > 0 ? magSize * 5 : rule.AmmoCount;

            // Ищем generic ammo def для этого оружия ("GenericAmmo-{gun.defName}").
            // Если есть — передаём его: пешка сама выберет лучший патрон калибра.
            // Если нет  — fallback на конкретный ThingDef (самый дорогой).
            var genericAmmoDef = CEExtendedLoadoutHelper.FindGenericAmmoDefForWeapon(weapon.def);
            var preferredAmmoDef = ammoDefs
                .OrderByDescending(def => def.BaseMarketValue)
                .FirstOrDefault();
            if (preferredAmmoDef == null) { return; }

            EquipmentManager.LogMessage(
                $"[EM] {pawn.Pawn.LabelShortCap}: ammo for {weapon.LabelCapNoCount}" +
                $" generic={genericAmmoDef?.defName ?? "none"}" +
                $" specific={preferredAmmoDef.defName} count={targetCount}");

            _ = CEExtendedLoadoutHelper.SetAmmoInPersonalLoadout(
                pawn.Pawn, preferredAmmoDef, targetCount, genericAmmoDef);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Инструменты — BestOne: один лучший для всех worktypes
        // ─────────────────────────────────────────────────────────────────────
        private void AssignBestTool(PawnCache pawn, ToolRule rule)
        {
            var workTypes = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(wt => !pawn.Pawn.WorkTypeIsDisabled(wt)).ToList();
            var available = GetFilteredToolCandidates(pawn, rule, workTypes);
            if (available.Count == 0) { return; }

            var best = available
                .OrderByDescending(t => rule.GetThingScore(t, workTypes, _updateTime))
                .ThenBy(t => t.GetHashCode())
                .FirstOrDefault();
            if (best == null) { return; }
            if (pawn.AssignedWeapons.Keys.Any(t => t.def == best.def)) { return; }

            pawn.AssignedWeapons.Add(best, "tool");
            AddToolSlot(pawn, best.def);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Инструменты — AllAvailable: все подходящие без дублей по ThingDef
        // ─────────────────────────────────────────────────────────────────────
        private void AssignAllTools(PawnCache pawn, ToolRule rule)
        {
            var workTypes = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(wt => !pawn.Pawn.WorkTypeIsDisabled(wt)).ToList();
            var available = GetFilteredToolCandidates(pawn, rule, workTypes);

            foreach (var weapon in available.Where(w =>
                pawn.AssignedWeapons.Keys.All(t => t.def != w.def)))
            {
                pawn.AssignedWeapons.Add(weapon, "tool");
                AddToolSlot(pawn, weapon.def);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Инструменты — OneForEveryWorkType / OneForEveryAssignedWorkType
        // ─────────────────────────────────────────────────────────────────────
        private void AssignToolsForWorkTypes(PawnCache pawn, ToolRule rule,
            List<WorkTypeDef> workTypes)
        {
            var available = GetFilteredToolCandidates(pawn, rule, workTypes);
            if (available.Count == 0) { return; }

            // Кэшируем результат CanPickup заранее — один раз, не N×M.
            foreach (var workType in workTypes)
            {
                var best = available
                    .OrderByDescending(t => rule.GetThingScore(t, new[] { workType }, _updateTime))
                    .ThenBy(t => t.GetHashCode())
                    .FirstOrDefault();
                if (best == null) { continue; }
                if (pawn.AssignedWeapons.Keys.Any(t => t.def == best.def)) { continue; }

                pawn.AssignedWeapons.Add(best, $"tool_{workType.defName}");
                AddToolSlot(pawn, best.def);
            }
        }

        // Общий фильтр кандидатов для всех методов инструментов.
        private List<Thing> GetFilteredToolCandidates(PawnCache pawn, ToolRule rule,
            List<WorkTypeDef> workTypes)
        {
            var candidates = rule.GetCurrentlyAvailableItems(map, workTypes, _updateTime).ToList();
            _ = candidates.RemoveAll(t =>
                _pawnCache.Any(pc => pc != pawn && pc.AssignedWeapons.ContainsKey(t)));
            _ = candidates.RemoveAll(t =>
                !EquipmentUtility.CanEquip(t, pawn.Pawn) ||
                (pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap != null &&
                    !pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[t.Position]));
            if (pawn.Pawn.story.traits.HasTrait(TraitDefOf.Brawler))
            {
                _ = candidates.RemoveAll(t => t.def.IsRangedWeapon);
            }
            return candidates;
        }

        // Добавляет tool-слот в PersonalLoadout через CEExtendedLoadoutHelper.
        private void AddToolSlot(PawnCache pawn, ThingDef toolDef)
        {
            var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
            if (pawnLoadout == null) { return; }
            pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
            _ = CEExtendedLoadoutHelper.AddToolToPersonalLoadout(
                pawn.Pawn, toolDef, pawnLoadout.ManagedPersonalLoadoutSlots);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Тактовый метод — каждые 6 игровых часов
        // ─────────────────────────────────────────────────────────────────────
        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!map.IsPlayerHome) { return; }
            if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused ||
                Find.TickManager.TicksGame % 60 != 0) { return; }

            var mapTime = RimworldTime.GetMapTime(map);
            if (_updateTime.Year == mapTime.Year &&
                _updateTime.Day == mapTime.Day &&
                _updateTime.Hour == mapTime.Hour)
            {
                return;
            }

            _updateTime.Year = mapTime.Year;
            _updateTime.Day  = mapTime.Day;
            _updateTime.Hour = mapTime.Hour;

            EquipmentManager.LogMessage(
                $"[EM] Hourly tick: year={_updateTime.Year}" +
                $" day={_updateTime.Day} hour={_updateTime.Hour:N1} ==================");

            UpdatePawnCache();
            UpdateLoadouts();
            ProcessPawnQueue();
            RemoveUnassignedWeapons();

        }

        // ─────────────────────────────────────────────────────────────────────
        // Логирование управляемых слотов (удаление — на стороне CE через PersonalLoadout)
        // ─────────────────────────────────────────────────────────────────────
        private void RemoveUnassignedWeapons()
        {
            foreach (var pawn in _pawnCache.Where(pc => pc.ShouldUpdateEquipment))
            {
                var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
                if (pawnLoadout == null) { continue; }
                pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                EquipmentManager.LogMessage(
                    $"[EM] {pawn.Pawn.LabelShortCap}: managed slots = " +
                    string.Join(", ", pawnLoadout.ManagedPersonalLoadoutSlots));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Распределение loadout-ов между пешками
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateLoadouts()
        {
            // Auto-loadout pawns must be re-evaluated every hourly pass.
            // If we keep their previous AssignedLoadout, they never enter the
            // auto-assignment branch below because it only assigns when AssignedLoadout == null.
            foreach (var pawn in _pawnCache.Where(pc => pc.AutoLoadout))
            {
                pawn.AssignedLoadout = null;
            }

            foreach (var loadout in EquipmentManager.GetLoadouts()
                         .Where(l => l.Priority > 0)
                         .OrderByDescending(l =>
                             l.PassionLimits.Count + l.PawnCapacityLimits.Count +
                             l.PawnCapacityWeights.Count + l.PawnTraits.Count +
                             l.PawnWorkCapacities.Count + l.SkillLimits.Count +
                             l.SkillWeights.Count + l.StatLimits.Count + l.StatWeights.Count)
                         .ThenByDescending(l => l.Priority))
            {
                var availablePawns = _pawnCache.Where(pc => pc.IsAvailable(loadout)).ToList();
                if (availablePawns.Count == 0) { continue; }

                var prioritySum = availablePawns.Sum(p =>
                    p.AvailableLoadouts.Keys.Sum(l => l.Priority));
                var avgPriority = prioritySum / availablePawns.Count;
                if (avgPriority <= 0f) { continue; }

                var targetCount = (int)Math.Ceiling(
                    availablePawns.Count * (loadout.Priority / avgPriority));
                var assignedCount = availablePawns.Count(pc => pc.AssignedLoadout == loadout);

                while (assignedCount < targetCount)
                {
                    var pawn = availablePawns
                        .Where(pc => pc.AssignedLoadout == null && pc.AutoLoadout)
                        .OrderByDescending(pc => pc.AvailableLoadouts[loadout])
                        .ThenBy(pc => pc.Pawn.GetHashCode())
                        .FirstOrDefault();
                    if (pawn == null) { break; }
                    pawn.AssignedLoadout = loadout;
                    assignedCount++;
                }
            }

            foreach (var pawn in _pawnCache.Where(pc => pc.AssignedLoadout == null || !pc.ShouldUpdateEquipment))
            {
                pawn.AssignedWeapons.Clear();
                pawn.AssignedAmmo.Clear();
            }

            EquipmentManager.LogMessage("[EM] Loadouts: " +
                string.Join(", ", _pawnCache
                    .Where(pc => pc.AssignedLoadout != null)
                    .Select(pc => $"{pc.Pawn.LabelShortCap}={pc.AssignedLoadout.Label}")));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Обновление кэша пешек
        // ─────────────────────────────────────────────────────────────────────
        private void UpdatePawnCache()
        {
            _allPawns ??= new HashSet<Pawn>();
            _allPawns.Clear();
            _allPawns.AddRange(map.mapPawns.FreeColonistsSpawned.Where(p =>
                p.Faction == Faction.OfPlayer &&
                !p.HasExtraHomeFaction() &&
                !p.HasExtraMiniFaction() &&
                p.GuestStatus == null));

            _pawnCache ??= new HashSet<PawnCache>();
            foreach (var pc in _pawnCache.Where(pc => !_allPawns.Contains(pc.Pawn)).ToList())
            {
                _ = _pawnCache.Remove(pc);
            }
            foreach (var pawn in _allPawns)
            {
                var pc = _pawnCache.FirstOrDefault(c => c.Pawn == pawn);
                if (pc == null)
                {
                    pc = new PawnCache(pawn);
                    _ = _pawnCache.Add(pc);
                }
                pc.Update(_updateTime);
            }

            EquipmentManager.LogMessage("[EM] Pawns: " +
                string.Join("; ", _pawnCache.Select(pc =>
                    $"{pc.Pawn.LabelShortCap}" +
                    $"({pc.AssignedLoadout?.Label ?? "None"}" +
                    $",{(pc.AutoLoadout ? "auto" : "manual")})" +
                    $"[{(pc.ShouldUpdateEquipment ? "upd" : "skip")}]")));
        }


        // ─────────────────────────────────────────────────────────────────────
        // Принудительное обновление (отладка)
        // ─────────────────────────────────────────────────────────────────────
        public void ForceUpdate()
        {
            _updateTime.Year = -1;
            _updateTime.Day  = -1;
            _updateTime.Hour = -1;
            UpdatePawnCache();
            UpdateLoadouts();

            var candidates = _pawnCache
                .Where(pc => pc.ShouldUpdateEquipment && pc.AssignedLoadout != null)
                .OrderBy(pc => pc.Pawn.thingIDNumber)
                .ToList();
            foreach (var pawn in candidates)
            {
                _ = ProcessPawnEquipment(pawn);
            }

            RemoveUnassignedWeapons();
        }
    }
}
