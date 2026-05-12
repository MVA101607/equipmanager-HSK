using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace EquipmentManager
{
    [UsedImplicitly]
    internal class EquipmentManagerMapComponent : MapComponent
    {
        private readonly RimworldTime _updateTime = new(-1, -1, -1);
        private HashSet<Pawn>       _allPawns   = new();
        private HashSet<PawnCache>  _pawnCache  = new();
        private int                 _pawnProcessingIndex;

        public EquipmentManagerMapComponent(Map map) : base(map) { }

        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        // ─────────────────────────────────────────────────────────────────────
        // Score текущего оружия пешки для сравнения с кандидатом.
        // Возвращает 0 если пешка безоружна или тип оружия не совпадает с rule.
        // ─────────────────────────────────────────────────────────────────────
        private float GetCurrentWeaponScore(PawnCache pawn, MeleeWeaponRule rule)
        {
            // Ищем лучшее ближнее оружие среди всего носимого:
            // слот оборудования (Primary + Secondary CE) и инвентарь.
            var carried = (pawn.Pawn.equipment?.AllEquipmentListForReading
                               ?? Enumerable.Empty<Thing>())
                .Concat(pawn.Pawn.inventory?.innerContainer
                               ?? Enumerable.Empty<Thing>())
                .Where(t => t.def.IsMeleeWeapon && rule.IsAvailable(t, _updateTime))
                .OrderByDescending(t => rule.GetThingScore(t, _updateTime))
                .FirstOrDefault();
            if (carried != null)
            {
                EquipmentManager.LogMessage(
                    $"[EM] {pawn.Pawn.LabelShortCap}: current melee weapon" +
                    $" '{carried.LabelCapNoCount}'" +
                    $" score={rule.GetThingScore(carried, _updateTime):F2}");
                return rule.GetThingScore(carried, _updateTime);
            }
            return 0f;
        }

        private float GetCurrentWeaponScore(PawnCache pawn, RangedWeaponRule rule)
        {
            // Ищем лучшее дальнобойное оружие среди всего носимого:
            // слот оборудования (Primary + Secondary CE) и инвентарь.
            var carried = (pawn.Pawn.equipment?.AllEquipmentListForReading
                               ?? Enumerable.Empty<Thing>())
                .Concat(pawn.Pawn.inventory?.innerContainer
                               ?? Enumerable.Empty<Thing>())
                .Where(t => t.def.IsRangedWeapon && rule.IsAvailable(t, _updateTime))
                .OrderByDescending(t => rule.GetThingScore(t, _updateTime))
                .FirstOrDefault();
            if (carried != null)
            {
                EquipmentManager.LogMessage(
                    $"[EM] {pawn.Pawn.LabelShortCap}: current ranged weapon" +
                    $" '{carried.LabelCapNoCount}'" +
                    $" score={rule.GetThingScore(carried, _updateTime):F2}");
                return rule.GetThingScore(carried, _updateTime);
            }
            return 0f;
        }


        // ─────────────────────────────────────────────────────────────────────
        // Поставить задание «подобрать конкретный экземпляр оружия» в очередь.
        // requestQueueing=true — не прерывает текущую работу пешки.
        // ─────────────────────────────────────────────────────────────────────
        private static void EnqueuePickupJob(Pawn pawn, Thing weapon)
        {
            if (weapon == null || pawn == null) { return; }
            // Уже несёт — ничего делать не надо
            if (pawn.equipment?.AllEquipmentListForReading.Contains(weapon) == true ||
                pawn.inventory?.innerContainer.Contains(weapon) == true) { return; }
            // Очередь переполнена — пропускаем (негласный лимит ≤ 3 авто-заданий)
            if (pawn.jobs?.jobQueue != null && pawn.jobs.jobQueue.Count > 5) { return; }
            // Пешка не может дотянуться или зарезервировать предмет
            if (!pawn.CanReach(weapon, PathEndMode.Touch, pawn.NormalMaxDanger())) { return; }
            if (!pawn.CanReserve(weapon)) { return; }
            var job = JobMaker.MakeJob(JobDefOf.TakeInventory, weapon);
            job.count = 1;
            _ = pawn.jobs.TryTakeOrderedJob(job, requestQueueing: true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Назначение дальнего основного оружия
        // ─────────────────────────────────────────────────────────────────────
        private bool AssignPrimaryRangedWeapon(PawnCache pawn)
        {
            if (pawn.AssignedRole.PrimaryRangedWeaponRuleId == null) { return false; }
            var rule = EquipmentManager.GetRangedWeaponRule(
                (int)pawn.AssignedRole.PrimaryRangedWeaponRuleId);
            if (rule == null) { return false; }

            EquipmentManager.LogMessage(
                $"[EM] AssignPrimaryRangedWeapon for {pawn.Pawn.LabelShortCap}");

            var availableWeapons = rule.GetCurrentlyAvailableItems(map, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn &&
                    (pc.AssignedWeapons.ContainsKey(thing) ||
                     pc.ReservedWeapons.ContainsKey(thing) ||
                     pc.Pawn.inventory?.innerContainer.Contains(thing) == true ||
                     pc.Pawn.equipment?.AllEquipmentListForReading.Contains(thing) == true)));
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
            pawn.ReserveWeapon(bestWeapon);

            var pawnRole = EquipmentManager.GetPawnRole(pawn.Pawn);
            if (pawnRole != null)
            {
                pawnRole.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetPrimaryWeaponInPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnRole.ManagedPersonalLoadoutSlots);
            }

            UpdateAmmo(pawn, bestWeapon, rule);
            EnqueuePickupJob(pawn.Pawn, bestWeapon);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Назначение ближнего основного оружия
        // ─────────────────────────────────────────────────────────────────────
        private bool AssignPrimaryMeleeWeapon(PawnCache pawn)
        {
            if (pawn.AssignedRole.PrimaryMeleeWeaponRuleId == null) { return false; }
            var rule = EquipmentManager.GetMeleeWeaponRule(
                (int)pawn.AssignedRole.PrimaryMeleeWeaponRuleId);
            if (rule == null) { return false; }

            EquipmentManager.LogMessage(
                $"[EM] AssignPrimaryMeleeWeapon for {pawn.Pawn.LabelShortCap}");

            var availableWeapons = rule.GetCurrentlyAvailableItems(map, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn &&
                    (pc.AssignedWeapons.ContainsKey(thing) ||
                     pc.ReservedWeapons.ContainsKey(thing) ||
                     pc.Pawn.inventory?.innerContainer.Contains(thing) == true ||
                     pc.Pawn.equipment?.AllEquipmentListForReading.Contains(thing) == true)));
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
            pawn.ReserveWeapon(bestWeapon);

            var pawnRole = EquipmentManager.GetPawnRole(pawn.Pawn);
            if (pawnRole != null)
            {
                pawnRole.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetPrimaryWeaponInPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnRole.ManagedPersonalLoadoutSlots);
            }
            EnqueuePickupJob(pawn.Pawn, bestWeapon);
            return true;
        }


        // ─────────────────────────────────────────────────────────────────────
        // Назначение дальнего вторичного оружия
        // ─────────────────────────────────────────────────────────────────────
        private bool AssignSecondaryRangedWeapon(PawnCache pawn)
        {
            if (pawn.AssignedRole.SecondaryRangedWeaponRuleId == null) { return false; }
            var rule = EquipmentManager.GetRangedWeaponRule(
                (int)pawn.AssignedRole.SecondaryRangedWeaponRuleId);
            if (rule == null) { return false; }

            EquipmentManager.LogMessage(
                $"[EM] AssignSecondaryRangedWeapon for {pawn.Pawn.LabelShortCap}");

            var availableWeapons = rule.GetCurrentlyAvailableItems(map, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn &&
                    (pc.AssignedWeapons.ContainsKey(thing) ||
                     pc.ReservedWeapons.ContainsKey(thing) ||
                     pc.Pawn.inventory?.innerContainer.Contains(thing) == true ||
                     pc.Pawn.equipment?.AllEquipmentListForReading.Contains(thing) == true)));
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

            pawn.AssignedWeapons[bestWeapon] = "secondary";
            pawn.ReserveWeapon(bestWeapon);

            var pawnRole = EquipmentManager.GetPawnRole(pawn.Pawn);
            if (pawnRole != null)
            {
                pawnRole.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetSecondaryWeaponInPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnRole.ManagedPersonalLoadoutSlots);
            }

            UpdateSecondaryAmmo(pawn, bestWeapon, rule);
            EnqueuePickupJob(pawn.Pawn, bestWeapon);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Назначение ближнего вторичного оружия
        // ─────────────────────────────────────────────────────────────────────
        private bool AssignSecondaryMeleeWeapon(PawnCache pawn)
        {
            if (pawn.AssignedRole.SecondaryMeleeWeaponRuleId == null) { return false; }
            var rule = EquipmentManager.GetMeleeWeaponRule(
                (int)pawn.AssignedRole.SecondaryMeleeWeaponRuleId);
            if (rule == null) { return false; }

            EquipmentManager.LogMessage(
                $"[EM] AssignSecondaryMeleeWeapon for {pawn.Pawn.LabelShortCap}");

            var availableWeapons = rule.GetCurrentlyAvailableItems(map, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn &&
                    (pc.AssignedWeapons.ContainsKey(thing) ||
                     pc.ReservedWeapons.ContainsKey(thing) ||
                     pc.Pawn.inventory?.innerContainer.Contains(thing) == true ||
                     pc.Pawn.equipment?.AllEquipmentListForReading.Contains(thing) == true)));
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

            pawn.AssignedWeapons[bestWeapon] = "secondary";
            pawn.ReserveWeapon(bestWeapon);

            var pawnRole = EquipmentManager.GetPawnRole(pawn.Pawn);
            if (pawnRole != null)
            {
                pawnRole.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetSecondaryWeaponInPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnRole.ManagedPersonalLoadoutSlots);
            }

            EnqueuePickupJob(pawn.Pawn, bestWeapon);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Патроны для вторичного ranged-оружия
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateSecondaryAmmo(PawnCache pawn, Thing weapon, RangedWeaponRule rule)
        {
            if (!CombatExtendedHelper.EnableAmmoSystem) { return; }

            var weaponCache = EquipmentManager.GetRangedWeaponCache(weapon, _updateTime);

            if (weaponCache.IsAmmo)
            {
                var pr = EquipmentManager.GetPawnRole(pawn.Pawn);
                pr.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetSecondaryAmmoInPersonalLoadout(
                    pawn.Pawn, weapon.def, 5,
                    managedSlotKeys: pr.ManagedPersonalLoadoutSlots);
                return;
            }

            var ammoDefs  = weaponCache.AmmoTypes.ToList();
            if (ammoDefs.Count == 0) { return; }

            var magSize     = weaponCache.MagSize;
            var targetCount = magSize > 0 ? magSize * 5 : rule.AmmoCount;

            var genericDef  = CEExtendedLoadoutHelper.FindGenericAmmoDefForWeapon(weapon.def);
            var pr2         = EquipmentManager.GetPawnRole(pawn.Pawn);
            pr2.ManagedPersonalLoadoutSlots ??= new HashSet<string>();

            _ = genericDef != null
                ? CEExtendedLoadoutHelper.SetSecondaryAmmoInPersonalLoadout(
                    pawn.Pawn, ammoDefs[0], targetCount,
                    genericAmmoDef: genericDef,
                    managedSlotKeys: pr2.ManagedPersonalLoadoutSlots)
                : CEExtendedLoadoutHelper.SetSecondaryAmmoInPersonalLoadout(
                    pawn.Pawn, ammoDefs[0], targetCount,
                    managedSlotKeys: pr2.ManagedPersonalLoadoutSlots);
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

                var pr1 = EquipmentManager.GetPawnRole(pawn.Pawn);
                pr1.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetAmmoInPersonalLoadout(
                    pawn.Pawn, weapon.def, 5,
                    managedSlotKeys: pr1.ManagedPersonalLoadoutSlots);

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

            var pr2 = EquipmentManager.GetPawnRole(pawn.Pawn);
            pr2.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
            _ = CEExtendedLoadoutHelper.SetAmmoInPersonalLoadout(
                pawn.Pawn, preferredAmmoDef, targetCount, genericAmmoDef,
                managedSlotKeys: pr2.ManagedPersonalLoadoutSlots);
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
            var pawnRole = EquipmentManager.GetPawnRole(pawn.Pawn);
            if (pawnRole == null) { return; }
            pawnRole.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
            _ = CEExtendedLoadoutHelper.AddToolToPersonalLoadout(
                pawn.Pawn, toolDef, pawnRole.ManagedPersonalLoadoutSlots);
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
            UpdateRoles();
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
                var pawnRole = EquipmentManager.GetPawnRole(pawn.Pawn);
                if (pawnRole == null) { continue; }
                pawnRole.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Распределение loadout-ов между пешками
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateRoles()
        {
            // Сбрасываем AssignedRole у авто-пешек перед переназначением.
            foreach (var pc in _pawnCache.Where(pc => pc.AutoRole))
            {
                pc.AssignedRole = null;
            }

            // Собираем авто-пешки и запускаем пропорциональный алгоритм.
            var autoPawns = _pawnCache
                .Where(pc => pc.AutoRole)
                .Select(pc => pc.Pawn)
                .ToList();

            GlobalReassigner.ReassignProportional(autoPawns, EquipmentManager);

            // Синхронизируем AssignedRole в кэше с результатом GameComponent.
            foreach (var pc in _pawnCache.Where(pc => pc.AutoRole))
            {
                var pawnRole = EquipmentManager.GetPawnRole(pc.Pawn);
                pc.AssignedRole = EquipmentManager.GetRole(pawnRole?.RoleId);
            }

            // Очищаем снаряжение пешкам без роли или не требующим обновления.
            foreach (var pc in _pawnCache.Where(
                pc => pc.AssignedRole == null || !pc.ShouldUpdateEquipment))
            {
                pc.AssignedWeapons.Clear();
                pc.AssignedAmmo.Clear();
            }
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
                p.GuestStatus == null &&
                p.DevelopmentalStage.Adult()));

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

        //    EquipmentManager.LogMessage("[EM] Pawns: " +
          //      string.Join("; ", _pawnCache.Select(pc =>
            //        $"{pc.Pawn.LabelShortCap}" +
              //      $"({pc.AssignedRole?.Label ?? "None"}" +
                //    $",{(pc.AutoRole ? "auto" : "manual")})" +
                  //  $"[{(pc.ShouldUpdateEquipment ? "upd" : "skip")}]")));
        }


        // ─────────────────────────────────────────────────────────────────────
        // Принудительное обновление (отладка)
        // ─────────────────────────────────────────────────────────────────────
        // ─────────────────────────────────────────────────────────────────────
        // Обработка снаряжения одной пешки.
        // Предполагается, что AssignedRole и ShouldUpdateEquipment уже
        // выставлены корректно вызывающей стороной.
        // Возвращает true если обработка была выполнена.
        // ─────────────────────────────────────────────────────────────────────
        private bool ProcessPawnEquipment(PawnCache pawn)
        {
            if (!pawn.ShouldUpdateEquipment || pawn.AssignedRole == null) { return false; }

            pawn.AssignedWeapons.Clear();
            pawn.AssignedAmmo.Clear();
            pawn.PurgeExpiredReservations();

            EquipmentManager.LogMessage(
                $"[EM] ProcessPawnEquipment: {pawn.Pawn.LabelShortCap}" +
                $" loadout={pawn.AssignedRole.Label}");

            // Удалить устаревшие tool-слоты из PersonalLoadout ПЕРЕД новым циклом назначения.
            // Weapon/ammo-слоты чистит SetPrimaryWeaponInPersonalLoadout при замене оружия.
            var pawnRoleData = EquipmentManager.GetPawnRole(pawn.Pawn);
            if (pawnRoleData != null)
            {
                pawnRoleData.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                CEExtendedLoadoutHelper.RemoveToolSlotsFromPersonalLoadout(
                    pawn.Pawn, pawnRoleData.ManagedPersonalLoadoutSlots);
            }

            // Основное оружие
            switch (pawn.AssignedRole.PrimaryRuleType)
            {
                case Role.PrimaryWeaponType.RangedWeapon:
                    _ = AssignPrimaryRangedWeapon(pawn);
                    break;
                case Role.PrimaryWeaponType.MeleeWeapon:
                    _ = AssignPrimaryMeleeWeapon(pawn);
                    break;
                case Role.PrimaryWeaponType.None:
                default:
                    break;
            }

            // Вторичное оружие
            switch (pawn.AssignedRole.SecondaryRuleType)
            {
                case Role.PrimaryWeaponType.RangedWeapon:
                    _ = AssignSecondaryRangedWeapon(pawn);
                    break;
                case Role.PrimaryWeaponType.MeleeWeapon:
                    _ = AssignSecondaryMeleeWeapon(pawn);
                    break;
                case Role.PrimaryWeaponType.None:
                default:
                    break;
            }

            // Инструменты
            if (pawn.AssignedRole.ToolRuleId != null)
            {
                var toolRule = EquipmentManager.GetToolRule((int) pawn.AssignedRole.ToolRuleId);
                if (toolRule != null)
                {
                    switch (toolRule.EquipMode)
                    {
                        case ItemRule.ToolEquipMode.BestOne:
                            AssignBestTool(pawn, toolRule);
                            break;
                        case ItemRule.ToolEquipMode.AllAvailable:
                            AssignAllTools(pawn, toolRule);
                            break;
                        case ItemRule.ToolEquipMode.OneForEveryWorkType:
                            AssignToolsForWorkTypes(pawn, toolRule,
                                WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                                    .Where(wt => wt.visible && !pawn.Pawn.WorkTypeIsDisabled(wt))
                                    .ToList());
                            break;
                        case ItemRule.ToolEquipMode.OneForEveryAssignedWorkType:
                            AssignToolsForWorkTypes(pawn, toolRule,
                                WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                                    .Where(wt => wt.visible &&
                                                 pawn.Pawn.workSettings.WorkIsActive(wt))
                                    .ToList());
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Почасовая очередь: каждый игровой час обрабатывается одна пешка.
        //
        // Для каждой пешки в свой час:
        //   1. Если роль назначена автоматически — пересчитать AvailableRoles
        //      и проверить, не нужно ли сменить роль (пересчёт конкурентный,
        //      затрагивает все auto-пешки, но не меняет _updateTime).
        //   2. Найти лучшее оружие на карте с учётом RetentionBonus для
        //      текущего носимого оружия.
        // ─────────────────────────────────────────────────────────────────────
        private void ProcessPawnQueue()
        {
            var allCandidates = _pawnCache
                .OrderBy(pc => pc.Pawn.thingIDNumber)
                .ToList();

            if (allCandidates.Count == 0) { return; }

            _pawnProcessingIndex %= allCandidates.Count;
            var pawn = allCandidates[_pawnProcessingIndex];
            _pawnProcessingIndex++;

            EquipmentManager.LogMessage(
                $"[EM] Queue tick: processing {pawn.Pawn.LabelShortCap}" +
                $" (auto={pawn.AutoRole}, capable={pawn.ShouldUpdateEquipment})");

            if (pawn.AutoRole)
            {
                pawn.AvailableRoles.Clear();
                foreach (var role in EquipmentManager.GetRoles())
                {
                    if (role.IsAvailable(pawn.Pawn))
                    {
                        pawn.AvailableRoles.Add(role, role.GetScore(pawn.Pawn));
                    }
                }

                if (pawn.AvailableRoles.Count == 0)
                {
                    var noRoleMsg = "EquipmentManager.NoRoleAvailable".Translate(pawn.Pawn.LabelShortCap);
                    Messages.Message(noRoleMsg, pawn.Pawn, MessageTypeDefOf.RejectInput, historical: false);
                    EquipmentManager.LogMessage(
                        $"[EM] ForceUpdateForPawn: {pawn.Pawn.LabelShortCap} — no roles match!");
                    foreach (var role in EquipmentManager.GetRoles())
                    {
                        _ = role.IsAvailable(pawn.Pawn, true);
                    }
                }

                var previousRole = pawn.AssignedRole;
                pawn.AssignedRole = null;
                UpdateRoles();

                if (pawn.AssignedRole != previousRole)
                {
                    EquipmentManager.LogMessage(
                        $"[EM] {pawn.Pawn.LabelShortCap}: role changed" +
                        $" {previousRole?.Label ?? "None"} → {pawn.AssignedRole?.Label ?? "None"}");
                }

                // ── FIX: записать новую роль в GameComponent, чтобы UI обновился ──
                EquipmentManager.SetPawnRole(pawn.Pawn, pawn.AssignedRole, automatic: true);
                // ──────────────────────────────────────────────────────────────────
            }

            pawn.ShouldUpdateEquipment = true;
            _ = ProcessPawnEquipment(pawn);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Принудительное обновление всех пешек (отладка / DebugActions)
        // ─────────────────────────────────────────────────────────────────────
        public void ForceUpdate()
        {
            _updateTime.Year = -1;
            _updateTime.Day  = -1;
            _updateTime.Hour = -1;
            UpdatePawnCache();
            UpdateRoles();

            var candidates = _pawnCache
                .Where(pc => pc.ShouldUpdateEquipment && pc.AssignedRole != null)
                .OrderBy(pc => pc.Pawn.thingIDNumber)
                .ToList();
            foreach (var pc in candidates)
            {
                _ = ProcessPawnEquipment(pc);
            }

            RemoveUnassignedWeapons();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Принудительное обновление одной пешки из UI.
        // _updateTime НЕ сбрасывается — остальные пешки не затрагиваются.
        // ─────────────────────────────────────────────────────────────────────
        public void ForceUpdateForPawn(Pawn pawn)
        {
            UpdatePawnCache();

            if (!pawn.DevelopmentalStage.Adult()) { /* ... */ return; }

            var pc = _pawnCache.FirstOrDefault(c => c.Pawn == pawn);
            if (pc == null) { /* ... */ return; }

            EquipmentManager.LogMessage(
                $"[EM] ForceUpdateForPawn: {pawn.LabelShortCap}" +
                $" autoRole={pc.AutoRole}" +
                $" assignedRole={pc.AssignedRole?.Label ?? "null"}");

            if (pc.AutoRole || pc.AssignedRole == null)
            {
                // Авто-пешка или без роли — пересчитать через конкурентный алгоритм
                pc.AvailableRoles.Clear();
                foreach (var role in EquipmentManager.GetRoles())
                {
                    if (role.IsAvailable(pawn))
                    {
                        var score = role.GetScore(pawn);
                        pc.AvailableRoles.Add(role, score);
                    }
                }
                if (pc.AvailableRoles.Count == 0)
                {
                    var noRoleMsg = "EquipmentManager.NoRoleAvailable".Translate(pawn.LabelShortCap);
                    Messages.Message(noRoleMsg, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    EquipmentManager.LogMessage(
                        $"[EM] ForceUpdateForPawn: {pawn.LabelShortCap} — no roles match!");
                    foreach (var role in EquipmentManager.GetRoles())
                    {
                        _ = role.IsAvailable(pawn, true);
                    }
                }
                var previousLabel = pc.AssignedRole?.Label ?? "null";
                pc.AssignedRole = null;
                UpdateRoles();
                EquipmentManager.SetPawnRole(pawn, pc.AssignedRole, automatic: true);
                EquipmentManager.LogMessage(
                    $"[EM] ForceUpdateForPawn: {pawn.LabelShortCap}" +
                    $" loadout {previousLabel} → {pc.AssignedRole?.Label ?? "null"}");
            }
            else
            {
                // Роль выбрана игроком — UpdatePawnCache мог сбросить AssignedRole если
                // hoursPassed < 6. Восстанавливаем явно из _pawnRoles.
                var pawnRole = EquipmentManager.GetPawnRole(pawn);
                pc.AssignedRole = EquipmentManager.GetRole(pawnRole?.RoleId);
                EquipmentManager.LogMessage(
                    $"[EM] ForceUpdateForPawn: {pawn.LabelShortCap}" +
                    $" manual role restored: {pc.AssignedRole?.Label ?? "null"}");
            }

            pc.ShouldUpdateEquipment = true;
            if (pc.AssignedRole != null)
            {
                _ = ProcessPawnEquipment(pc);
            }
            else
            {
                Log.Warning($"[EM] ForceUpdateForPawn: {pawn.LabelShortCap}" +
                    " has no role — weapon assignment skipped");
            }

            RemoveUnassignedWeapons();
        }
    }
}
