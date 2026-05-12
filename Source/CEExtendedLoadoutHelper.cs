using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using CombatExtended.ExtendedLoadout;
using JetBrains.Annotations;
using RimWorld;
using Verse;

using CELoadout    = CombatExtended.Loadout;
using CELoadoutSlot = CombatExtended.LoadoutSlot;
using CEGenericDef  = CombatExtended.LoadoutGenericDef;

namespace EquipmentManager
{
    internal static class CEExtendedLoadoutHelper
    {
        // ── Схема ключей ManagedPersonalLoadoutSlots ──────────────────────────
        // weapon:<defName>              — основное оружие (specific ThingDef)
        // ammo:<defName>               — патроны primary specific
        // ammo-generic:<defName>       — патроны primary generic
        // tool:<defName>               — инструмент
        // weapon-secondary:<defName>   — вторичное оружие
        // ammo-secondary:<defName>     — патроны secondary specific
        // ammo-secondary-generic:<defN>— патроны secondary generic
        //
        // Такое разделение позволяет избирательно чистить только нужную группу слотов:
        //   • weapon+ammo         — при смене primary (SetPrimaryWeaponInPersonalLoadout)
        //   • weapon-secondary+ammo-secondary — при смене secondary
        //   • tool                — в начале каждого ProcessPawnEquipment
        // ─────────────────────────────────────────────────────────────────────

        internal const string PrefixWeapon           = "weapon:";
        internal const string PrefixAmmo             = "ammo:";
        internal const string PrefixAmmoGeneric      = "ammo-generic:";
        internal const string PrefixTool             = "tool:";
        internal const string PrefixWeaponSecondary  = "weapon-secondary:";
        internal const string PrefixAmmoSecondary    = "ammo-secondary:";
        internal const string PrefixAmmoSecondaryGen = "ammo-secondary-generic:";

        public static bool IsAvailable()
        {
            return true;
        }

        // CE создаёт LoadoutGenericDef с именем "GenericAmmo-{gun.defName}" при старте игры.
        // Возвращает null если оружие не имеет CE ammoSet.
        public static CEGenericDef FindGenericAmmoDefForWeapon([NotNull] ThingDef weaponDef)
        {
            if (weaponDef == null) { throw new ArgumentNullException(nameof(weaponDef)); }
            return GenDefDatabase.GetDefSilentFail(
                typeof(CEGenericDef), "GenericAmmo-" + weaponDef.defName, false)
                as CEGenericDef;
        }

        // ── Оружие ───────────────────────────────────────────────────────────

        // Назначить основное оружие в PersonalLoadout.
        // Чистит weapon- и ammo-слоты прошлого цикла; tool-слоты не трогает.
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
                RemoveManagedSlotsByPrefix(personalLoadout, managedSlotKeys,
                    PrefixWeapon, PrefixAmmo, PrefixAmmoGeneric);

                personalLoadout.AddSlot(new CELoadoutSlot(weaponDef, 1));
                _ = managedSlotKeys.Add($"{PrefixWeapon}{weaponDef.defName}");

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


        // Назначить вторичное оружие в PersonalLoadout.
        // Чистит только weapon-secondary- и ammo-secondary-слоты; primary не трогает.
        public static bool SetSecondaryWeaponInPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] ThingDef weaponDef,
            [NotNull] HashSet<string> managedSlotKeys)
        {
            try
            {
                var personalLoadout = GetPersonalLoadout(pawn);
                if (personalLoadout == null) { return false; }

                managedSlotKeys ??= new HashSet<string>();
                RemoveManagedSlotsByPrefix(personalLoadout, managedSlotKeys,
                    PrefixWeaponSecondary, PrefixAmmoSecondary, PrefixAmmoSecondaryGen);

                personalLoadout.AddSlot(new CELoadoutSlot(weaponDef, 1));
                _ = managedSlotKeys.Add($"{PrefixWeaponSecondary}{weaponDef.defName}");

                NotifyAll(pawn);

                Messages.Message(
                    "EquipmentManager.SecondaryWeaponEquipped".Translate(
                        pawn.Name.ToStringShort, weaponDef.LabelCap),
                    MessageTypeDefOf.SilentInput,
                    historical: false);

                return true;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[EM] SetSecondaryWeaponInPersonalLoadout failed for " +
                    pawn.LabelShortCap + ": " + ex.Message,
                    pawn.thingIDNumber ^ weaponDef.shortHash ^ 0x53EC);
                return false;
            }
        }

        // Патроны для вторичного оружия — аналог SetAmmoInPersonalLoadout,
        // но использует prefix ammo-secondary- и не трогает primary-патроны.
        public static bool SetSecondaryAmmoInPersonalLoadout(
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

                if (managedSlotKeys != null)
                {
                    var key = genericAmmoDef != null
                        ? $"{PrefixAmmoSecondaryGen}{genericAmmoDef.defName}"
                        : $"{PrefixAmmoSecondary}{specificAmmoDef.defName}";
                    _ = managedSlotKeys.Add(key);
                }

                NotifyAll(pawn);
                return true;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[EM] SetSecondaryAmmoInPersonalLoadout failed for " +
                    pawn.LabelShortCap + ": " + ex.Message,
                    pawn.thingIDNumber ^ specificAmmoDef.shortHash ^ 0x53EC ^ count);
                return false;
            }
        }

        // ── Патроны ──────────────────────────────────────────────────────────

        // genericAmmoDef != null => generic-слот (пешка выбирает тип патрона сама).
        // genericAmmoDef == null => specific-слот (конкретный ThingDef, количество count).
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

                // Удаляем старые ammo-слоты для этого калибра/типа.
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

                if (managedSlotKeys != null)
                {
                    var key = genericAmmoDef != null
                        ? $"{PrefixAmmoGeneric}{genericAmmoDef.defName}"
                        : $"{PrefixAmmo}{specificAmmoDef.defName}";
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

        // ── Инструменты ──────────────────────────────────────────────────────

        // Добавить инструмент в PersonalLoadout.
        // Не дублирует если ThingDef уже есть. Не трогает weapon/ammo-слоты.
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
                var key = $"{PrefixTool}{toolDef.defName}";

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

        // Удалить все tool-слоты из PersonalLoadout и их ключи из набора.
        // Вызывается в начале ProcessPawnEquipment перед новым циклом назначения.
        public static void RemoveToolSlotsFromPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] HashSet<string> managedSlotKeys)
        {
            if (managedSlotKeys.Count == 0) { return; }
            try
            {
                var personalLoadout = GetPersonalLoadout(pawn);
                if (personalLoadout == null) { return; }
                RemoveManagedSlotsByPrefix(personalLoadout, managedSlotKeys, PrefixTool);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[EM] RemoveToolSlotsFromPersonalLoadout failed for " +
                    pawn.LabelShortCap + ": " + ex.Message,
                    pawn.thingIDNumber);
            }
        }

        // ── Вспомогательные методы ────────────────────────────────────────────

        // Удаляет из personalLoadout слоты, чьи ключи начинаются с одного из prefixes,
        // и одновременно убирает эти ключи из managedSlotKeys.
        private static void RemoveManagedSlotsByPrefix(
            CELoadout personalLoadout,
            HashSet<string> managedSlotKeys,
            params string[] prefixes)
        {
            var targetKeys = managedSlotKeys
                .Where(k => prefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal)))
                .ToHashSet();
            if (targetKeys.Count == 0) { return; }

            var toRemove = personalLoadout.OwnSlots
                .Where(slot =>
                {
                    if (slot == null) { return false; }
                    // Строим все возможные ключи для этого слота и проверяем пересечение.
                    foreach (var k in SlotKeys(slot))
                    {
                        if (targetKeys.Contains(k)) { return true; }
                    }
                    return false;
                })
                .ToList();

            foreach (var s in toRemove) { personalLoadout.RemoveSlot(s); }
            managedSlotKeys.ExceptWith(targetKeys);
        }

        // Возвращает все возможные строковые ключи для слота (по всем четырём префиксам).
        // Для ThingDef-слота это три варианта (weapon/ammo/tool), для generic — один.
        private static IEnumerable<string> SlotKeys(CELoadoutSlot slot)
        {
            if (slot.thingDef != null)
            {
                yield return $"{PrefixWeapon}{slot.thingDef.defName}";
                yield return $"{PrefixAmmo}{slot.thingDef.defName}";
                yield return $"{PrefixTool}{slot.thingDef.defName}";
                yield return $"{PrefixWeaponSecondary}{slot.thingDef.defName}";
                yield return $"{PrefixAmmoSecondary}{slot.thingDef.defName}";
            }
            else if (slot.genericDef != null)
            {
                yield return $"{PrefixAmmoGeneric}{slot.genericDef.defName}";
                yield return $"{PrefixAmmoSecondaryGen}{slot.genericDef.defName}";
            }
        }

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
