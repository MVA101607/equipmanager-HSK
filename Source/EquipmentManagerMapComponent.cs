using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace EquipmentManager
{
    [UsedImplicitly]
    internal class EquipmentManagerMapComponent : MapComponent
    {
        private static EquipmentManagerGameComponent _equipmentManager;
        private readonly RimworldTime _updateTime = new(-1, -1, -1);
        private HashSet<Pawn> _allPawns = new();
        private HashSet<PawnCache> _pawnCache = new();

        public EquipmentManagerMapComponent(Map map) : base(map) { }

        private static EquipmentManagerGameComponent EquipmentManager =>
            _equipmentManager ??= Current.Game.GetComponent<EquipmentManagerGameComponent>();

        // ─────────────────────────────────────────────────────────────────────────
        // Ценность оружия, которое пешка держит в руках прямо сейчас.
        // Возвращает 0 если пешка безоружна или оружие не подходит по типу.
        // ─────────────────────────────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────────────────────────
        // Назначение дальнего основного оружия
        // ─────────────────────────────────────────────────────────────────────────
        private void AssignPrimaryRangedWeapon(PawnCache pawn)
        {
            if (pawn.AssignedLoadout.PrimaryRangedWeaponRuleId == null) { return; }
            var rule = EquipmentManager.GetRangedWeaponRule((int)pawn.AssignedLoadout.PrimaryRangedWeaponRuleId);
            if (rule == null) { return; }

            EquipmentManager.LogMessage($"[EM] AssignPrimaryRangedWeapon for {pawn.Pawn.LabelShortCap}");

            var availableWeapons = rule.GetCurrentlyAvailableItems(map, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn && pc.AssignedWeapons.ContainsKey(thing)));
            _ = availableWeapons.RemoveAll(thing =>
                !EquipmentUtility.CanEquip(thing, pawn.Pawn) ||
                (pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap != null &&
                    !pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[thing.Position]));
            if (availableWeapons.Count == 0) { return; }

            var bestWeapon = availableWeapons
                .OrderByDescending(thing => rule.GetThingScore(thing, _updateTime))
                .ThenBy(thing => thing.GetHashCode())
                .FirstOrDefault();
            if (bestWeapon == null) { return; }

            // Менять только если новое оружие лучше текущего × RetentionBonus.
            var currentScore = GetCurrentWeaponScore(pawn, rule);
            var bestScore = rule.GetThingScore(bestWeapon, _updateTime);
            if (currentScore > 0f && bestScore < currentScore * rule.RetentionBonus) { return; }

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

        // ─────────────────────────────────────────────────────────────────────────
        // Назначение ближнего основного оружия
        // ─────────────────────────────────────────────────────────────────────────
        private void AssignPrimaryMeleeWeapon(PawnCache pawn)
        {
            if (pawn.AssignedLoadout.PrimaryMeleeWeaponRuleId == null) { return; }
            var rule = EquipmentManager.GetMeleeWeaponRule((int)pawn.AssignedLoadout.PrimaryMeleeWeaponRuleId);
            if (rule == null) { return; }

            EquipmentManager.LogMessage($"[EM] AssignPrimaryMeleeWeapon for {pawn.Pawn.LabelShortCap}");

            var availableWeapons = rule.GetCurrentlyAvailableItems(map, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn && pc.AssignedWeapons.ContainsKey(thing)));
            _ = availableWeapons.RemoveAll(thing =>
                !EquipmentUtility.CanEquip(thing, pawn.Pawn) ||
                (pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap != null &&
                    !pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[thing.Position]));
            if (availableWeapons.Count == 0) { return; }

            var bestWeapon = availableWeapons
                .OrderByDescending(thing => rule.GetThingScore(thing, _updateTime))
                .ThenBy(thing => thing.GetHashCode())
                .FirstOrDefault();
            if (bestWeapon == null) { return; }

            var currentScore = GetCurrentWeaponScore(pawn, rule);
            var bestScore = rule.GetThingScore(bestWeapon, _updateTime);
            if (currentScore > 0f && bestScore < currentScore * rule.RetentionBonus) { return; }

            pawn.AssignedWeapons.Add(bestWeapon, "primary");

            var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
            if (pawnLoadout != null)
            {
                pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.SetPrimaryWeaponInPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnLoadout.ManagedPersonalLoadoutSlots);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Инструменты — режим BestOne: один лучший инструмент для всех worktype
        // ─────────────────────────────────────────────────────────────────────────
        private void AssignBestTool(PawnCache pawn, ToolRule rule)
        {
            var workTypes = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(wt => !pawn.Pawn.WorkTypeIsDisabled(wt)).ToList();
            var availableWeapons = rule.GetCurrentlyAvailableItems(map, workTypes, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn && pc.AssignedWeapons.ContainsKey(thing)));
            _ = availableWeapons.RemoveAll(thing =>
                !EquipmentUtility.CanEquip(thing, pawn.Pawn) ||
                (pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap != null &&
                    !pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[thing.Position]));
            if (pawn.Pawn.story.traits.HasTrait(TraitDefOf.Brawler))
            {
                _ = availableWeapons.RemoveAll(thing => thing.def.IsRangedWeapon);
            }

            var bestWeapon = availableWeapons
                .OrderByDescending(thing => rule.GetThingScore(thing, workTypes, _updateTime))
                .ThenBy(thing => thing.GetHashCode())
                .FirstOrDefault();
            if (bestWeapon == null) { return; }
            if (pawn.AssignedWeapons.Keys.Any(thing => thing.def == bestWeapon.def)) { return; }

            pawn.AssignedWeapons.Add(bestWeapon, "tool");

            var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
            if (pawnLoadout != null)
            {
                pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
                _ = CEExtendedLoadoutHelper.AddToolToPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnLoadout.ManagedPersonalLoadoutSlots);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Инструменты — режим AllAvailable: все подходящие инструменты
        // ─────────────────────────────────────────────────────────────────────────
        private void AssignAllTools(PawnCache pawn, ToolRule rule)
        {
            var workTypes = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(wt => !pawn.Pawn.WorkTypeIsDisabled(wt)).ToList();
            var availableWeapons = rule.GetCurrentlyAvailableItems(map, workTypes, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn && pc.AssignedWeapons.ContainsKey(thing)));
            _ = availableWeapons.RemoveAll(thing =>
                !EquipmentUtility.CanEquip(thing, pawn.Pawn) ||
                (pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap != null &&
                    !pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[thing.Position]));
            if (pawn.Pawn.story.traits.HasTrait(TraitDefOf.Brawler))
            {
                _ = availableWeapons.RemoveAll(thing => thing.def.IsRangedWeapon);
            }

            var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
            if (pawnLoadout == null) { return; }
            pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();

            foreach (var weapon in availableWeapons.Where(weapon =>
                pawn.AssignedWeapons.Keys.All(thing => thing.def != weapon.def)))
            {
                pawn.AssignedWeapons.Add(weapon, "tool");
                _ = CEExtendedLoadoutHelper.AddToolToPersonalLoadout(
                    pawn.Pawn, weapon.def, pawnLoadout.ManagedPersonalLoadoutSlots);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Инструменты — режим OneForEveryWorkType / OneForEveryAssignedWorkType:
        // лучший инструмент для каждого типа работ отдельно
        // ─────────────────────────────────────────────────────────────────────────
        private void AssignToolsForWorkTypes(PawnCache pawn, ToolRule rule, List<WorkTypeDef> workTypes)
        {
            var availableWeapons = rule.GetCurrentlyAvailableItems(map, workTypes, _updateTime).ToList();
            _ = availableWeapons.RemoveAll(thing =>
                _pawnCache.Any(pc => pc != pawn && pc.AssignedWeapons.ContainsKey(thing)));
            _ = availableWeapons.RemoveAll(thing =>
                !EquipmentUtility.CanEquip(thing, pawn.Pawn) ||
                (pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap != null &&
                    !pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[thing.Position]));
            if (pawn.Pawn.story.traits.HasTrait(TraitDefOf.Brawler))
            {
                _ = availableWeapons.RemoveAll(thing => thing.def.IsRangedWeapon);
            }

            var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
            if (pawnLoadout == null) { return; }
            pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();

            foreach (var workType in workTypes)
            {
                var bestWeapon = availableWeapons
                    .OrderByDescending(thing => rule.GetThingScore(thing, new[] { workType }, _updateTime))
                    .ThenBy(thing => thing.GetHashCode())
                    .FirstOrDefault();
                if (bestWeapon == null) { continue; }
                if (pawn.AssignedWeapons.Keys.Any(thing => thing.def == bestWeapon.def)) { continue; }

                pawn.AssignedWeapons.Add(bestWeapon, $"tool_{workType.labelShort}");
                _ = CEExtendedLoadoutHelper.AddToolToPersonalLoadout(
                    pawn.Pawn, bestWeapon.def, pawnLoadout.ManagedPersonalLoadoutSlots);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Патроны — только через CE PersonalLoadout
        // ─────────────────────────────────────────────────────────────────────────
        private void UpdateAmmo(PawnCache pawn, Thing weapon, RangedWeaponRule rule)
        {
            if (!CombatExtendedHelper.EnableAmmoSystem) { return; }
            var weaponCache = EquipmentManager.GetRangedWeaponCache(weapon, _updateTime);
            var ammoDefs = weaponCache.AmmoTypes.ToList();
            var pawnAmmo = pawn.Pawn.inventory.innerContainer.InnerListForReading
                .Where(thing => ammoDefs.Contains(thing.def)).ToList();
            var currentAmmo = pawnAmmo.Sum(thing => thing.stackCount) +
                pawn.AssignedAmmo.Where(pair => ammoDefs.Contains(pair.Key.def)).Sum(pair => pair.Value);
            EquipmentManager.LogMessage(
                $"{pawn.Pawn.LabelShortCap} ammo for {weapon.LabelCapNoCount} = {currentAmmo}");

            int targetAmmoCount;
            if (weaponCache.IsAmmo)
            { // если патроны это и есть оружие, например, гранаты
                targetAmmoCount = 5;
            }
            else
            {
                var magSize = weaponCache.MagSize;
                targetAmmoCount = magSize > 0 ? magSize * 5 : rule.AmmoCount;
            }

            var preferredAmmoDef = ammoDefs.OrderByDescending(def => def.BaseMarketValue).FirstOrDefault();
            if (preferredAmmoDef == null) { return; }

            _ = CEExtendedLoadoutHelper.SetAmmoInPersonalLoadout(pawn.Pawn, preferredAmmoDef, targetAmmoCount);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Тактовый метод — обновление каждые 6 игровых часов
        // ─────────────────────────────────────────────────────────────────────────
        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!map.IsPlayerHome) { return; }
            if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused ||
                Find.TickManager.TicksGame % 60 != 0) { return; }

            var mapTime = RimworldTime.GetMapTime(map);
            var hoursPassed =
                ((mapTime.Year - _updateTime.Year) * 60 * 24) +
                ((mapTime.Day  - _updateTime.Day)  * 24) +
                  mapTime.Hour - _updateTime.Hour;
            if (hoursPassed < 6f) { return; }

            _updateTime.Year = mapTime.Year;
            _updateTime.Day  = mapTime.Day;
            _updateTime.Hour = mapTime.Hour;

            EquipmentManager.LogMessage(
                $"Updating equipment at year={_updateTime.Year}," +
                $" day={_updateTime.Day}, hour={_updateTime.Hour:N1} ====================");

            UpdatePawnCache();
            UpdateLoadouts();
            UpdatePrimaryWeapons();
            UpdateTools();
            RemoveUnassignedWeapons();

            foreach (var pawn in _pawnCache.Where(pc => pc.AssignedWeapons.Any()))
            {
                EquipmentManager.LogMessage(
                    $"Assigned weapons for {pawn.Pawn.LabelShortCap} = " +
                    $"{string.Join(", ", pawn.AssignedWeapons.Select(p => $"{p.Key.LabelCap} ({p.Value})"))}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Синхронизация ManagedPersonalLoadoutSlots
        // ─────────────────────────────────────────────────────────────────────────
        private void RemoveUnassignedWeapons()
        {
            foreach (var pawn in _pawnCache.Where(pc => pc.ShouldUpdateEquipment))
            {
                var pawnLoadout = EquipmentManager.GetPawnLoadout(pawn.Pawn);
                if (pawnLoadout == null) { continue; }

                pawnLoadout.ManagedPersonalLoadoutSlots ??= new HashSet<string>();

                EquipmentManager.LogMessage(
                    $"[EM] {pawn.Pawn.LabelShortCap}: managed slots = " +
                    $"{string.Join(", ", pawnLoadout.ManagedPersonalLoadoutSlots)}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Назначение loadout-ов пешкам
        // ─────────────────────────────────────────────────────────────────────────
        private void UpdateLoadouts()
        {
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

                var prioritySum  = availablePawns.Sum(p => p.AvailableLoadouts.Keys.Sum(l => l.Priority));
                var avgPriority  = prioritySum / availablePawns.Count;
                if (avgPriority <= 0f) { continue; }

                var priorityShare      = loadout.Priority / avgPriority;
                var targetCount        = (int)Math.Ceiling(availablePawns.Count * priorityShare);
                var assignedPawnsCount = availablePawns.Count(pc => pc.AssignedLoadout == loadout);

                while (assignedPawnsCount < targetCount)
                {
                    var pawn = availablePawns
                        .Where(pc => pc.AssignedLoadout == null && pc.AutoLoadout)
                        .OrderByDescending(pc => pc.AvailableLoadouts[loadout])
                        .ThenBy(pc => pc.Pawn.GetHashCode())
                        .FirstOrDefault();
                    if (pawn == null) { break; }
                    pawn.AssignedLoadout = loadout;
                    assignedPawnsCount++;
                }
            }

            foreach (var pawn in _pawnCache)
            {
                pawn.AssignedWeapons.Clear();
                pawn.AssignedAmmo.Clear();
            }

            EquipmentManager.LogMessage(
                $"Equipment Manager: " +
                $"{string.Join(", ", _pawnCache.Where(pc => pc.AssignedLoadout != null).Select(pc => $"{pc.Pawn.LabelShortCap} = {pc.AssignedLoadout.Label}"))}");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Обновление кэша пешек
        // ─────────────────────────────────────────────────────────────────────────
        private void UpdatePawnCache()
        {
            _allPawns ??= new();
            _allPawns.Clear();
            _allPawns.AddRange(map.mapPawns.FreeColonistsSpawned.Where(pawn =>
                pawn.Faction == Faction.OfPlayer &&
                !pawn.HasExtraHomeFaction() &&
                !pawn.HasExtraMiniFaction() &&
                pawn.GuestStatus == null));

            _pawnCache ??= new();
            foreach (var pawn in _pawnCache.Where(pc => !_allPawns.Contains(pc.Pawn)).ToList())
            {
                _ = _pawnCache.Remove(pawn);
            }
            foreach (var pawn in _allPawns)
            {
                var pawnCache = _pawnCache.FirstOrDefault(pc => pc.Pawn == pawn);
                if (pawnCache == null)
                {
                    pawnCache = new PawnCache(pawn);
                    _ = _pawnCache.Add(pawnCache);
                }
                pawnCache.Update(_updateTime);
            }

            EquipmentManager.LogMessage(
                $"Equipment Manager: Pawns: " +
                $"{string.Join("; ", _pawnCache.Select(pc => $"{pc.Pawn.LabelShortCap} ({pc.AssignedLoadout?.Label ?? "None"}, {(pc.AutoLoadout ? "auto" : "manual")}) [{(pc.ShouldUpdateEquipment ? "updating" : "not updating")}]"))}");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Диспетчер назначения основного оружия
        // ─────────────────────────────────────────────────────────────────────────
        private void UpdatePrimaryWeapons()
        {
            var orderedPawns = _pawnCache
                .Where(pc => pc.ShouldUpdateEquipment)
                .OrderByDescending(pc =>
                    pc.AssignedLoadout?.PrimaryRuleType == Loadout.PrimaryWeaponType.RangedWeapon
                        ? pc.Pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn)
                        : pc.Pawn.GetStatValue(StatDefOf.MeleeHitChance))
                .ThenBy(pc => pc.Pawn.GetHashCode());

            foreach (var pawn in orderedPawns)
            {
                switch (pawn.AssignedLoadout.PrimaryRuleType)
                {
                    case Loadout.PrimaryWeaponType.None:
                        break;
                    case Loadout.PrimaryWeaponType.RangedWeapon:
                        AssignPrimaryRangedWeapon(pawn);
                        break;
                    case Loadout.PrimaryWeaponType.MeleeWeapon:
                        AssignPrimaryMeleeWeapon(pawn);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Диспетчер назначения инструментов
        // ─────────────────────────────────────────────────────────────────────────
        private void UpdateTools()
        {
            foreach (var pawn in _pawnCache.Where(pc => pc.ShouldUpdateEquipment))
            {
                if (pawn.AssignedLoadout.ToolRuleId == null) { continue; }
                var rule = EquipmentManager.GetToolRule((int)pawn.AssignedLoadout.ToolRuleId);
                if (rule == null) { continue; }

                switch (rule.EquipMode)
                {
                    case ItemRule.ToolEquipMode.OneForEveryWorkType:
                        AssignToolsForWorkTypes(pawn, rule,
                            WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                                .Where(wt => wt.visible && !pawn.Pawn.WorkTypeIsDisabled(wt)).ToList());
                        break;
                    case ItemRule.ToolEquipMode.OneForEveryAssignedWorkType:
                        AssignToolsForWorkTypes(pawn, rule,
                            WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                                .Where(wt => wt.visible && pawn.Pawn.workSettings.WorkIsActive(wt)).ToList());
                        break;
                    case ItemRule.ToolEquipMode.BestOne:
                        AssignBestTool(pawn, rule);
                        break;
                    case ItemRule.ToolEquipMode.AllAvailable:
                        AssignAllTools(pawn, rule);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Принудительное обновление (отладочное меню)
        // ─────────────────────────────────────────────────────────────────────────
        public void ForceUpdate()
        {
            _updateTime.Year = -1;
            _updateTime.Day  = -1;
            _updateTime.Hour = -1;
            UpdatePawnCache();
            UpdateLoadouts();
            UpdatePrimaryWeapons();
            UpdateTools();
            RemoveUnassignedWeapons();
        }
    }
}
