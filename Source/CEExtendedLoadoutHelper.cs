using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using CombatExtended.ExtendedLoadout;
using JetBrains.Annotations;
using RimWorld;
using Verse;

// Явные псевдонимы, чтобы избежать неоднозначности между CombatExtended.Loadout
// и любыми другими типами с тем же именем.
using CELoadout = CombatExtended.Loadout;
using CELoadoutSlot = CombatExtended.LoadoutSlot;
using CEGenericDef = CombatExtended.LoadoutGenericDef;

namespace EquipmentManager
{
    internal static class CEExtendedLoadoutHelper
    {
        // Сохранён для обратной совместимости с вызывающим кодом.
        // Зависимости CE и ExtendedLoadout обязательны — всегда true.
        public static bool IsAvailable()
        {
            return true;
        }

        // CE создаёт LoadoutGenericDef с именем "GenericAmmo-{gun.defName}" при старте игры.
        // Возвращает null если оружие не имеет CE ammoSet (ванильное или мод без CE ammo).
        public static CEGenericDef FindGenericAmmoDefForWeapon([NotNull] ThingDef weaponDef)
        {
            if (weaponDef == null) { throw new ArgumentNullException(nameof(weaponDef)); }
            return GenDefDatabase.GetDefSilentFail(
                typeof(CEGenericDef), "GenericAmmo-" + weaponDef.defName, false)
                as CEGenericDef;
        }

        // Назначить основное оружие в PersonalLoadout.
        // Удаляет управляемые слоты прошлого цикла, добавляет новый specific-слот.
        public static bool SetPrimaryWeaponInPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] ThingDef weaponDef,
            [NotNull] HashSet<string> managedSlotKeys)
        {
            try
            {
                var personalLoadout = GetPersonalLoadout(pawn);
                if (personalLoadout == null) { return false; }

                managedSlotKeys ??= new HashSet<string>();
                RemoveManagedSlots(personalLoadout, managedSlotKeys);

                personalLoadout.AddSlot(new CELoadoutSlot(weaponDef, 1));

                managedSlotKeys.Clear();
                _ = managedSlotKeys.Add($"thingdef:{weaponDef.defName}");

                NotifyAll(pawn);

                Messages.Message(
                    "EquipmentManager.WeaponEquipped".Translate(
                        pawn.Name.ToStringShort, weaponDef.LabelCap),
                    MessageTypeDefOf.SilentInput,
                    historical: false);

                return true;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[EM] SetPrimaryWeaponInPersonalLoadout failed for " +
                    pawn.LabelShortCap + ": " + ex.Message,
                    pawn.thingIDNumber ^ weaponDef.shortHash);
                return false;
            }
        }

        // Назначить патроны в PersonalLoadout.
        //   genericAmmoDef != null => generic-слот: пешка сама выбирает тип патрона калибра.
        //   genericAmmoDef == null => specific-слот: конкретный ThingDef, количество count.
        //   count = 0 при generic  => CE использует defaultCount из LoadoutGenericDef (= magazineSize).
        public static bool SetAmmoInPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] ThingDef specificAmmoDef,
            int count,
            CEGenericDef genericAmmoDef = null,
            HashSet<string> managedSlotKeys = null)
        {
            if (count <= 0 && genericAmmoDef == null) { return false; }
            try
            {
                var personalLoadout = GetPersonalLoadout(pawn);
                if (personalLoadout == null) { return false; }

                // Удаляем старые ammo-слоты (specific и generic) для этого калибра.
                var toRemove = personalLoadout.OwnSlots
                    .Where(s => s != null &&
                        (s.thingDef == specificAmmoDef ||
                         (genericAmmoDef != null && s.genericDef == genericAmmoDef)))
                    .ToList();
                foreach (var s in toRemove) { personalLoadout.RemoveSlot(s); }

                var newSlot = genericAmmoDef != null
                    ? new CELoadoutSlot(genericAmmoDef, count)
                    : new CELoadoutSlot(specificAmmoDef, count);
                personalLoadout.AddSlot(newSlot);
                // Регистрируем слот как управляемый, чтобы при смене роли он был удалён.
                if (managedSlotKeys != null)
                {
                    var key = genericAmmoDef != null
                        ? $"genericdef:{genericAmmoDef.defName}"
                        : $"thingdef:{specificAmmoDef.defName}";
                    _ = managedSlotKeys.Add(key);
                }
                NotifyAll(pawn);
                return true;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[EM] SetAmmoInPersonalLoadout failed for " +
                    pawn.LabelShortCap + ": " + ex.Message,
                    pawn.thingIDNumber ^ specificAmmoDef.shortHash ^ count);
                return false;
            }
        }

        // Добавить инструмент в PersonalLoadout.
        // Не удаляет другие слоты. Не дублирует если ThingDef уже есть.
        // managedSlotKeys накапливает ключи для очистки при смене loadout.
        public static bool AddToolToPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] ThingDef toolDef,
            [NotNull] HashSet<string> managedSlotKeys)
        {
            try
            {
                var personalLoadout = GetPersonalLoadout(pawn);
                if (personalLoadout == null) { return false; }

                managedSlotKeys ??= new HashSet<string>();
                var key = $"thingdef:{toolDef.defName}";

                // Не дублируем если слот уже есть
                if (personalLoadout.OwnSlots.Any(s => s?.thingDef == toolDef))
                {
                    _ = managedSlotKeys.Add(key);
                    NotifyAll(pawn);
                    return true;
                }

                personalLoadout.AddSlot(new CELoadoutSlot(toolDef, 1));
                _ = managedSlotKeys.Add(key);
                NotifyAll(pawn);
                return true;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[EM] AddToolToPersonalLoadout failed for " +
                    pawn.LabelShortCap + ": " + ex.Message,
                    pawn.thingIDNumber ^ toolDef.shortHash);
                return false;
            }
        }

        // ── Вспомогательные методы ────────────────────────────────────────────

        // GetLoadout(pawn, allowNull: false) — всегда создаёт Loadout_Multi если нет,
        // и всегда возвращает объект (null только если allowNull=true и слотов 0).
        // PersonalLoadout генерируется в конструкторе Loadout_Multi,
        // но для старых сейвов back-compat вызывает GeneratePersonalLoadout в GetLoadout.
        private static CELoadout GetPersonalLoadout(Pawn pawn)
        {
            if (LoadoutMulti_Manager.GetLoadout(pawn, allowNull: false) is not Loadout_Multi multiLoadout)
            {
                Log.Warning("[EM] CEExtendedLoadoutHelper: GetLoadout returned null for " +
                    pawn.LabelShortCap);
                return null;
            }

            var personal = multiLoadout.PersonalLoadout;
            if (personal == null)
            {
                // back-compat: для пешек из старых сейвов PersonalLoadout мог не создаться
                multiLoadout.GeneratePersonalLoadout(pawn);
                multiLoadout.NotifyLoadoutChanged();
                personal = multiLoadout.PersonalLoadout;
            }

            if (personal == null)
            {
                Log.Error("[EM] CEExtendedLoadoutHelper: PersonalLoadout still null for " +
                    pawn.LabelShortCap);
            }
            return personal;
        }

        private static void RemoveManagedSlots(CELoadout personalLoadout,
            HashSet<string> managedSlotKeys)
        {
            if (managedSlotKeys.Count == 0) { return; }
            var toRemove = personalLoadout.OwnSlots
                .Where(slot =>
                {
                    if (slot == null) { return false; }
                    var key = slot.thingDef != null
                        ? $"thingdef:{slot.thingDef.defName}"
                        : slot.genericDef != null
                            ? $"genericdef:{slot.genericDef.defName}"
                            : null;
                    return key != null && managedSlotKeys.Contains(key);
                })
                .ToList();
            foreach (var s in toRemove) { personalLoadout.RemoveSlot(s); }
        }

        // Уведомляем мультилоадаут пешки + все остальные для пересчёта агрегатора.
        // Именно так делает TestLoadoutHelper.
        private static void NotifyAll(Pawn pawn)
        {
            var multiLoadout = LoadoutMulti_Manager.GetLoadout(pawn, allowNull: false) as Loadout_Multi;
            multiLoadout?.NotifyLoadoutChanged();

            foreach (var lm in LoadoutMulti_Manager.LoadoutsMulti)
            {
                lm.NotifyLoadoutChanged();
            }
        }
    }
}
