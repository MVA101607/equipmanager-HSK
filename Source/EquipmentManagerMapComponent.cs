using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RimWorld.PsychicRitualRoleDef;

namespace EquipmentManager
{
    [UsedImplicitly]
    internal class EquipmentManagerMapComponent : MapComponent
    {
        private readonly RimworldTime _updateTime = new(-1, -1, -1);
        private HashSet<Pawn>       _allPawns   = new();
        private HashSet<PawnCache>  _pawnCache  = new();
        private int _weaponProcessingIndex;


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
            // Оружие должно лежать в разрешённой области пешки.
            // Если область ограничена и оружие за её пределами — не выдаём задание,
            // чтобы пешка не выбегала из убежища во время нападения.
            var allowedArea = pawn.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            if (allowedArea != null && !allowedArea[weapon.Position]) { return; }
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
            var magazines   = rule.AmmoCount > 0 ? rule.AmmoCount : 5;
            var targetCount = magSize > 1 ? magSize * magazines : magazines * 30;

            var filteredSec    = FilterAmmoByPreference(ammoDefs, rule.AmmoTypePreference);
            var candidatesSec  = filteredSec.Count > 0 ? filteredSec : ammoDefs;
            var genericDef     = CEExtendedLoadoutHelper.FindGenericAmmoDefForWeapon(weapon.def);
            var useGenericSec  = genericDef != null && rule.AmmoTypePreference == AmmoTypePreference.Any;
            var preferredSec   = candidatesSec.OrderByDescending(d => d.BaseMarketValue).FirstOrDefault();
            if (preferredSec == null) { return; }

            var pr2 = EquipmentManager.GetPawnRole(pawn.Pawn);
            pr2.ManagedPersonalLoadoutSlots ??= new HashSet<string>();

            _ = useGenericSec
                ? CEExtendedLoadoutHelper.SetSecondaryAmmoInPersonalLoadout(
                    pawn.Pawn, preferredSec, targetCount,
                    genericAmmoDef: genericDef,
                    managedSlotKeys: pr2.ManagedPersonalLoadoutSlots)
                : CEExtendedLoadoutHelper.SetSecondaryAmmoInPersonalLoadout(
                    pawn.Pawn, preferredSec, targetCount,
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

            var magSize     = weaponCache.MagSize;
            var magazines   = rule.AmmoCount > 0 ? rule.AmmoCount : 5;
            // magSize > 1: нормальное оружие. magSize <= 1: луки/арбалеты (1 стрела на выстрел).
            var targetCount = magSize > 1 ? magSize * magazines : magazines * 30;

            // Фильтруем по предпочтению типа патрона. При несовпадении — откат на Any.
            var filtered = FilterAmmoByPreference(ammoDefs, rule.AmmoTypePreference);
            var candidateDefs = filtered.Count > 0 ? filtered : ammoDefs;

            // Ищем generic ammo def для этого оружия ("GenericAmmo-{gun.defName}").
            // Any + genericDef → generic-слот (пешка сама выберет). Конкретный тип → specific-слот.
            var genericAmmoDef = CEExtendedLoadoutHelper.FindGenericAmmoDefForWeapon(weapon.def);
            var useGeneric = genericAmmoDef != null && rule.AmmoTypePreference == AmmoTypePreference.Any;
            var preferredAmmoDef = candidateDefs
                .OrderByDescending(def => def.BaseMarketValue)
                .FirstOrDefault();
            if (preferredAmmoDef == null) { return; }

            EquipmentManager.LogMessage(
                $"[EM] {pawn.Pawn.LabelShortCap}: ammo for {weapon.LabelCapNoCount}" +
                $" generic={genericAmmoDef?.defName ?? "none"}" +
                $" specific={preferredAmmoDef.defName} count={targetCount}");

            var pr2 = EquipmentManager.GetPawnRole(pawn.Pawn);
            pr2.ManagedPersonalLoadoutSlots ??= new HashSet<string>();
            _ = useGeneric
                ? CEExtendedLoadoutHelper.SetAmmoInPersonalLoadout(
                    pawn.Pawn, preferredAmmoDef, targetCount, genericAmmoDef,
                    managedSlotKeys: pr2.ManagedPersonalLoadoutSlots)
                : CEExtendedLoadoutHelper.SetAmmoInPersonalLoadout(
                    pawn.Pawn, preferredAmmoDef, targetCount,
                    managedSlotKeys: pr2.ManagedPersonalLoadoutSlots);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Хелпер фильтрации патронов по типу
        // ─────────────────────────────────────────────────────────────────────
        private static List<ThingDef> FilterAmmoByPreference(
            List<ThingDef> ammoDefs, AmmoTypePreference preference)
        {
            if (preference == AmmoTypePreference.Any) { return ammoDefs; }
            var keyword = preference switch
            {
                AmmoTypePreference.FMJ      => "_FMJ",
                AmmoTypePreference.AP       => "_AP",
                AmmoTypePreference.HP       => "_HP",
                AmmoTypePreference.HE       => "_HE",
                AmmoTypePreference.Stone    => "_Stone",
                AmmoTypePreference.Steel    => "_Steel",
                AmmoTypePreference.Plasteel => "_Plasteel",
                AmmoTypePreference.Venom    => "_Venom",
                AmmoTypePreference.Flame    => "_Flame",
                _                           => null
            };
            if (keyword == null) { return ammoDefs; }
            var result = ammoDefs
                .Where(def => def.defName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            return result.Count > 0 ? result : ammoDefs;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Вызывается из Harmony-патча перед выдачей задания пешке.
        // Проверяет, есть ли у пешки инструмент для данного WorkType.
        // Если нет — ставит EnqueuePickupJob и возвращает true
        //   (сигнал патчу: прервать текущий JobGiver, пешка идёт за инструментом).
        // Если инструмент уже есть — возвращает false (продолжить обычный поток).
        // ─────────────────────────────────────────────────────────────────────
        public bool EnsureToolForWorkType(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || workType == null) return false;

            // Пропускаем если режим пешки не включает инструменты
            var toolPawnMode = EquipmentManager.GetPawnRole(pawn)?.Mode ?? AssignMode.Both;
            if (toolPawnMode is AssignMode.Weapon or AssignMode.NoAction) return false;
            
            // Если игрок вручную назначил задание (Shift+клик) — не прерываем его
            if (pawn.jobs?.curJob != null && pawn.jobs.curJob.playerForced) return false;

            var workTypeRule = EquipmentManager
                .GetWorkTypeRules()
                .FirstOrDefault(r => r.WorkTypeDefName == workType.defName);

        //    EquipmentManager.LogMessage(
        //        $"[EM] EnsureToolForWorkType {pawn.LabelShortCap}: {workType.defName}");

            if (workTypeRule == null) return false;

            // FIX: если Loadout уже содержит tool-слот для этого типа работы —
            // CE сам обеспечит наличие инструмента, дополнительных заданий не нужно.
            var ensureManagedSlots = EquipmentManager.GetPawnRole(pawn)?.ManagedPersonalLoadoutSlots;
            if (ensureManagedSlots != null &&
                ensureManagedSlots.Any(k => k.StartsWith(CEExtendedLoadoutHelper.PrefixTool) &&
                    WorkTypeToolCache.GetGloballyAvailable(workTypeRule)
                        .Any(def => k == $"{CEExtendedLoadoutHelper.PrefixTool}{def.defName}")))
            {
                return false;
            }

            // Проверяем реальный инвентарь через кэшированный список подходящих инструментов
            var suitableTools = WorkTypeToolCache.GetSortedOnMap(workTypeRule, map).ToHashSet();

            bool alreadyCarried =
                (pawn.equipment?.AllEquipmentListForReading ?? Enumerable.Empty<Thing>())
                .Concat(pawn.inventory?.innerContainer ?? Enumerable.Empty<Thing>())
                .Any(t => suitableTools.Contains(t));

            if (alreadyCarried) return false;

            // Уже есть pickup-задание на подходящий инструмент — не дублируем
            if (pawn.jobs?.jobQueue != null &&
                pawn.jobs.jobQueue.Any(qj =>
                    qj.job?.def == JobDefOf.TakeInventory &&
                    qj.job.targetA.Thing != null &&
                    suitableTools.Contains(qj.job.targetA.Thing)))
                return true;

            var pawnCache = _pawnCache.FirstOrDefault(pc => pc.Pawn == pawn);
            if (pawnCache == null) return false;

            AssignToolsForWorkTypes(pawnCache, new List<WorkTypeDef> { workType });

            return pawn.jobs?.jobQueue != null && pawn.jobs.jobQueue.Count > 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Инструменты — OneForEveryWorkType / OneForEveryAssignedWorkType
        // Пишет в AssignedTools (отдельно от AssignedWeapons).
        // Записывает инструмент только если он реально достижим или уже у пешки.
        // ─────────────────────────────────────────────────────────────────────
        private void AssignToolsForWorkTypes(PawnCache pawn,
            List<WorkTypeDef> workTypes)
        {
            var already_got_job_to_take = false;
            foreach (var workType in workTypes)
            {
                var workTypeRule = EquipmentManager.GetWorkTypeRules()
                    .FirstOrDefault(r => r.WorkTypeDefName == workType.defName);
                if (workTypeRule == null) { continue; }

                // FIX: ManagedPersonalLoadoutSlots сохраняется в сейве (PawnRole.ExposeData).
                // Если слот tool:<defName> уже есть — Loadout настроен, CE сам следит
                // за тем чтобы пешка несла нужный инструмент. Ничего делать не нужно.
                var pawnRoleForTool = EquipmentManager.GetPawnRole(pawn.Pawn);
                var managedSlots    = pawnRoleForTool?.ManagedPersonalLoadoutSlots;
                if (managedSlots != null &&
                    managedSlots.Any(k => k.StartsWith(CEExtendedLoadoutHelper.PrefixTool) &&
                        WorkTypeToolCache.GetGloballyAvailable(workTypeRule)
                            .Any(def => k == $"{CEExtendedLoadoutHelper.PrefixTool}{def.defName}")))
                {
                    EquipmentManager.LogMessage(
                        $"[EM] AssignToolsForWorkTypes: {pawn.Pawn.LabelShortCap}" +
                        $" already has tool slot for {workType.defName} — skipping");
                    continue;
                }

                // Берём готовый отсортированный кэш — первый подходящий = лучший.
                // Исключаем инструменты, уже назначенные другим пешкам (AssignedTools).
                var best = WorkTypeToolCache.GetSortedOnMap(workTypeRule, map)
                    .Where(t => !_pawnCache.Any(pc => pc != pawn && pc.AssignedTools.ContainsKey(t)))
                    .Where(t => EquipmentUtility.CanEquip(t, pawn.Pawn))
                    .Where(t => pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap == null ||
                                pawn.Pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap[t.Position])
                    .Where(t => !pawn.Pawn.story.traits.HasTrait(TraitDefOf.Brawler) || !t.def.IsRangedWeapon)
                    .FirstOrDefault(t => pawn.AssignedTools.Keys.All(a => a.def != t.def));

                if (best == null) { continue; }

                // Инструмент уже физически у пешки?
                bool alreadyCarried =
                    pawn.Pawn.equipment?.AllEquipmentListForReading.Contains(best) == true ||
                    pawn.Pawn.inventory?.innerContainer.Contains(best) == true;

                // Инструмент доступен — записываем и выдаём задание
                pawn.AssignedTools[best] = workType.defName;
                AddToolSlot(pawn, best.def);

                if (!alreadyCarried && !already_got_job_to_take)
                {
                    already_got_job_to_take = true;
                    EnqueuePickupJob(pawn.Pawn, best);
                    var msg = "EquipmentManager.ToolAssigned".Translate(
                        pawn.Pawn.LabelShortCap,
                        best.LabelCapNoCount,
                        workType.labelShort ?? workType.label);
                    Messages.Message(msg, pawn.Pawn, MessageTypeDefOf.SilentInput, historical: false);
                }

                //break;
            }
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
            ProcessWeaponQueue();
            ProcessToolQueue();
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

            // Очищаем оружие и патроны пешкам без роли или не требующим обновления.
            // AssignedTools не трогаем — они управляются ProcessToolQueue независимо.
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

            // Сбрасываем только оружие и патроны; AssignedTools живёт независимо
            pawn.AssignedWeapons.Clear();
            pawn.AssignedAmmo.Clear();
            pawn.PurgeExpiredReservations();

            EquipmentManager.LogMessage(
                $"[EM] ProcessPawnEquipment: {pawn.Pawn.LabelShortCap}" +
                $" loadout={pawn.AssignedRole.Label}");

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

            // Инструменты (вынесли в отдельную очередь)

           /* AssignToolsForWorkTypes(pawn,
                WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                    .Where(wt => wt.visible && pawn.Pawn.workSettings.WorkIsActive(wt))
                    .ToList());*/
                
            

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Возвращает true если на карте в данный момент идёт формирование
        // каравана (присутствует lord с LordJob_FormAndSendCaravan).
        // ─────────────────────────────────────────────────────────────────────
        private static bool IsCaravanFormingOnMap(Map map)
        {
            if (map == null) { return false; }
            return map.lordManager?.lords
                .Any(l => l.LordJob is LordJob_FormAndSendCaravan) ?? false;
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
        private void ProcessWeaponQueue()
        {
            var allCandidates = _pawnCache
                .OrderBy(pc => pc.Pawn.thingIDNumber)
                .ToList();

            if (allCandidates.Count == 0) { return; }

            _weaponProcessingIndex %= allCandidates.Count;
            var pawn = allCandidates[_weaponProcessingIndex];
            _weaponProcessingIndex++;

            // Пропускаем призванных (в бою) пешек
            if (pawn.Pawn.Drafted)
            {
                EquipmentManager.LogMessage(
                    $"[EM] WeaponQueue tick: skipping {pawn.Pawn.LabelShortCap} — drafted");
                return;
            }

            // Пропускаем если формируется караван
            if (IsCaravanFormingOnMap(pawn.Pawn.Map))
            {
                EquipmentManager.LogMessage(
                    $"[EM] WeaponQueue tick: skipping {pawn.Pawn.LabelShortCap} — caravan forming");
                return;
            }

            // Пропускаем если режим пешки не включает оружие
            var pawnMode = EquipmentManager.GetPawnRole(pawn.Pawn)?.Mode ?? AssignMode.Both;
            if (pawnMode is AssignMode.Tool or AssignMode.NoAction)
            {
                EquipmentManager.LogMessage(
                    $"[EM] WeaponQueue tick: skipping {pawn.Pawn.LabelShortCap} — mode={pawnMode}");
                return;
            }

            EquipmentManager.LogMessage(
                $"[EM] WeaponQueue tick: processing {pawn.Pawn.LabelShortCap}" +
                $" (auto={pawn.AutoRole}, capable={pawn.ShouldUpdateEquipment})");

            if (pawn.AutoRole)
            {
                // Считаем текущее распределение ролей по всем пешкам кэша
                var currentCounts = new Dictionary<int, int>();
                foreach (var pc in _pawnCache)
                {
                    var rid = EquipmentManager.GetPawnRole(pc.Pawn)?.RoleId;
                    if (rid == null) { continue; }
                    _ = currentCounts.TryGetValue(rid.Value, out var cnt);
                    currentCounts[rid.Value] = cnt + 1;
                }

                var previousRole = pawn.AssignedRole;
                var newRole = GlobalReassigner.AssignRoleForSinglePawn(
                    pawn.Pawn, EquipmentManager, _pawnCache.Count, currentCounts);

                if (newRole?.Id != previousRole?.Id)
                {
                    EquipmentManager.LogMessage(
                        $"[EM] {pawn.Pawn.LabelShortCap}: role changed" +
                        $" {previousRole?.Label ?? "None"} → {newRole?.Label ?? "None"}");
                    pawn.AssignedRole = newRole;
                    EquipmentManager.SetPawnRole(pawn.Pawn, newRole, automatic: true);
                    // Сбрасываем только оружие и патроны; AssignedTools живёт независимо
                    pawn.AssignedWeapons.Clear();
                    pawn.AssignedAmmo.Clear();
                }
            }

            _ = ProcessPawnEquipment(pawn);
        }

        private void ProcessToolQueue()
        {
            var candidates = _pawnCache
                .Where(pc => !pc.Pawn.Drafted)
                .OrderBy(pc => pc.Pawn.thingIDNumber)
                .ToList();

            if (candidates.Count == 0) { return; }

            // Инструменты назначаем другой пешке, не той же самой что и оружие
            int _toolProcessingIndex = (_weaponProcessingIndex + (candidates.Count/2))% candidates.Count;
            var pawn = candidates[_toolProcessingIndex];

            if (IsCaravanFormingOnMap(pawn.Pawn.Map)) { return; }

            // Пропускаем если режим пешки не включает инструменты
            var toolPawnMode = EquipmentManager.GetPawnRole(pawn.Pawn)?.Mode ?? AssignMode.Both;
            if (toolPawnMode is AssignMode.Weapon or AssignMode.NoAction)
            {
                EquipmentManager.LogMessage(
                    $"[EM] ToolQueue tick: skipping {pawn.Pawn.LabelShortCap} — mode={toolPawnMode}");
                return;
            }

            // Очищаем устаревшие записи: инструменты, которые пешка уже не несёт
            // FIX: был баг с приоритетом операторов — !x?.Contains() == true
            // вычислялось как (!nullable) == true, что давало false при null.
            // Правильная форма: x?.Contains() != true (т.е. false или null).
            var stale = pawn.AssignedTools.Keys
                .Where(t => pawn.Pawn.equipment?.AllEquipmentListForReading.Contains(t) != true &&
                            pawn.Pawn.inventory?.innerContainer.Contains(t) != true)
                .ToList();
            foreach (var t in stale) { _ = pawn.AssignedTools.Remove(t); }

            var workTypes = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(wt => wt.visible && pawn.Pawn.workSettings.WorkIsActive(wt))
                .ToList();


            if (workTypes.Count == 0) { return; }

            EquipmentManager.LogMessage(
                $"[EM] ToolQueue tick: processing tools for {pawn.Pawn.LabelShortCap}");

            // Затем сразу же переназначаем инструменты на основе активных работ
            AssignToolsForWorkTypes(pawn, workTypes);
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
            var forceMode = EquipmentManager.GetPawnRole(pawn)?.Mode ?? AssignMode.Both;
            if (pc.AssignedRole != null)
            {
                if (forceMode is AssignMode.Both or AssignMode.Weapon)
                {
                    _ = ProcessPawnEquipment(pc);
                }
                if (forceMode is AssignMode.Both or AssignMode.Tool)
                {
                    var workTypes = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                        .Where(wt => wt.visible && pawn.workSettings.WorkIsActive(wt))
                        .ToList();
                    if (workTypes.Count > 0) { AssignToolsForWorkTypes(pc, workTypes); }
                }
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
