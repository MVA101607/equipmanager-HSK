using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    internal static class CEExtendedLoadoutHelper
    {
        private static MethodInfo   _ceGetLoadout;
        private static Type         _loadoutMultiManagerType;
        private static Type         _loadoutMultiType;
        private static PropertyInfo _personalLoadoutProp;
        private static MethodInfo   _notifyChanged;
        private static MethodInfo   _addSlot;
        private static ConstructorInfo _slotCtorSpecific;
        private static ConstructorInfo _slotCtorGeneric;
        private static FieldInfo    _slotThingDef;
        private static PropertyInfo _slotsOnLoadout;
        private static Type         _loadoutGenericDefType;
        private static bool         _available;
        private static bool         _initialized;

        public static bool IsAvailable()
        {
            if (_initialized) { return _available; }
            _initialized = true;

            _loadoutMultiManagerType =
                AccessTools.TypeByName("CombatExtended.ExtendedLoadout.LoadoutMulti_Manager");
            _loadoutMultiType =
                AccessTools.TypeByName("CombatExtended.ExtendedLoadout.Loadout_Multi");
            var loadoutType         = AccessTools.TypeByName("CombatExtended.Loadout");
            var utilType            = AccessTools.TypeByName("CombatExtended.Utility_Loadouts");
            var slotType            = AccessTools.TypeByName("CombatExtended.LoadoutSlot");
            _loadoutGenericDefType  = AccessTools.TypeByName("CombatExtended.LoadoutGenericDef");

            if (_loadoutMultiManagerType == null || _loadoutMultiType == null ||
                loadoutType == null || utilType == null ||
                slotType == null || _loadoutGenericDefType == null)
            {
                Log.Warning("[EM] CEExtendedLoadoutHelper: types not found." +
                    " multiMgr="   + (_loadoutMultiManagerType != null) +
                    " multiType="  + (_loadoutMultiType != null) +
                    " loadout="    + (loadoutType != null) +
                    " util="       + (utilType != null) +
                    " slot="       + (slotType != null) +
                    " genericDef=" + (_loadoutGenericDefType != null));
                _available = false;
                return false;
            }

            _ceGetLoadout =
                AccessTools.Method(utilType, "GetLoadout", new Type[] { typeof(Pawn) }) ??
                AccessTools.Method(_loadoutMultiManagerType, "GetLoadout", new Type[] { typeof(Pawn) });

            _personalLoadoutProp = AccessTools.Property(_loadoutMultiType, "PersonalLoadout");
            _notifyChanged       = AccessTools.Method(_loadoutMultiType, "NotifyLoadoutChanged");
            _addSlot             = AccessTools.Method(loadoutType, "AddSlot",
                                       new Type[] { slotType });
            _slotCtorSpecific    = AccessTools.Constructor(slotType,
                                       new Type[] { typeof(ThingDef), typeof(int) });
            _slotCtorGeneric     = AccessTools.Constructor(slotType,
                                       new Type[] { _loadoutGenericDefType, typeof(int) });
            _slotThingDef        = AccessTools.Field(slotType, "_def");
            _slotsOnLoadout      = AccessTools.Property(loadoutType, "Slots");

            _available = _ceGetLoadout       != null &&
                         _personalLoadoutProp != null &&
                         _notifyChanged       != null &&
                         _addSlot             != null &&
                         _slotCtorSpecific    != null &&
                         _slotCtorGeneric     != null &&
                         _slotThingDef        != null &&
                         _slotsOnLoadout      != null;

            if (!_available)
            {
                Log.Warning("[EM] CEExtendedLoadoutHelper: reflection incomplete." +
                    " ceGetLoadout="     + (_ceGetLoadout != null) +
                    " personalLoadout="  + (_personalLoadoutProp != null) +
                    " notifyChanged="    + (_notifyChanged != null) +
                    " addSlot="          + (_addSlot != null) +
                    " slotCtorSpecific=" + (_slotCtorSpecific != null) +
                    " slotCtorGeneric="  + (_slotCtorGeneric != null) +
                    " slotThingDef="     + (_slotThingDef != null) +
                    " slotsOnLoadout="   + (_slotsOnLoadout != null));
            }
            return _available;
        }

        // CE создаёт LoadoutGenericDef с именем "GenericAmmo-{gun.defName}" при старте игры.
        // Возвращает null если оружие не имеет CE ammoSet (ванильное или мод без CE ammo).
        public static Def FindGenericAmmoDefForWeapon([NotNull] ThingDef weaponDef)
        {
            if (weaponDef == null) { throw new ArgumentNullException(nameof(weaponDef)); }
            if (_loadoutGenericDefType == null) { return null; }
            return GenDefDatabase.GetDefSilentFail(
                _loadoutGenericDefType, "GenericAmmo-" + weaponDef.defName, false) as Def;
        }

        // Назначить основное оружие в PersonalLoadout.
        // Удаляет управляемые слоты прошлого цикла, добавляет новый specific-слот.
        public static bool SetPrimaryWeaponInPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] ThingDef weaponDef,
            [NotNull] HashSet<string> managedSlotKeys)
        {
            if (!IsAvailable()) { return false; }
            try
            {
                var multiLoadout = GetMultiLoadout(pawn);
                if (multiLoadout == null) { return false; }
                var personalLoadout = GetPersonalLoadout(multiLoadout);
                if (personalLoadout == null) { return false; }

                managedSlotKeys ??= new HashSet<string>();
                RemoveManagedSlots(personalLoadout, managedSlotKeys);

                var slot = _slotCtorSpecific.Invoke(new object[] { weaponDef, 1 });
                _ = _addSlot.Invoke(personalLoadout, new[] { slot });

                managedSlotKeys.Clear();
                _ = managedSlotKeys.Add($"thingdef:{weaponDef.defName}");

                NotifyChanged(multiLoadout);

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
            Def genericAmmoDef = null)
        {
            if (!IsAvailable()) { return false; }
            if (count <= 0 && genericAmmoDef == null) { return false; }
            try
            {
                var multiLoadout = GetMultiLoadout(pawn);
                if (multiLoadout == null) { return false; }
                var personalLoadout = GetPersonalLoadout(multiLoadout);
                if (personalLoadout == null) { return false; }

                // Удаляем старые ammo-слоты (specific и generic) для этого калибра.
                if (_slotsOnLoadout.GetValue(personalLoadout, null) is IList slotsObj)
                {
                    for (var i = slotsObj.Count - 1; i >= 0; i--)
                    {
                        var slotObj = slotsObj[i];
                        if (slotObj == null) { continue; }
                        var def = _slotThingDef.GetValue(slotObj) as Def;
                        if (def == specificAmmoDef ||
                            (genericAmmoDef != null && def == genericAmmoDef))
                        {
                            slotsObj.RemoveAt(i);
                        }
                    }
                }

                var slot = genericAmmoDef != null
                    ? _slotCtorGeneric.Invoke(new object[] { genericAmmoDef, count })
                    : _slotCtorSpecific.Invoke(new object[] { specificAmmoDef, count });
                _ = _addSlot.Invoke(personalLoadout, new[] { slot });
                NotifyChanged(multiLoadout);
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
            if (!IsAvailable()) { return false; }
            try
            {
                var multiLoadout = GetMultiLoadout(pawn);
                if (multiLoadout == null) { return false; }
                var personalLoadout = GetPersonalLoadout(multiLoadout);
                if (personalLoadout == null) { return false; }

                managedSlotKeys ??= new HashSet<string>();
                var key = $"thingdef:{toolDef.defName}";

                if (_slotsOnLoadout.GetValue(personalLoadout, null) is IList slotsObj)
                {
                    foreach (var slotObj in slotsObj)
                    {
                        if (slotObj == null) { continue; }
                        if ((_slotThingDef.GetValue(slotObj) as Def) == toolDef)
                        {
                            _ = managedSlotKeys.Add(key);
                            NotifyChanged(multiLoadout);
                            return true;
                        }
                    }
                }

                var slot = _slotCtorSpecific.Invoke(new object[] { toolDef, 1 });
                _ = _addSlot.Invoke(personalLoadout, new[] { slot });
                _ = managedSlotKeys.Add(key);
                NotifyChanged(multiLoadout);
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

        private static object GetMultiLoadout(Pawn pawn)
        {
            var result = _ceGetLoadout.Invoke(null, new object[] { pawn });
            if (result == null)
            {
                Log.Error("[EM] CEExtendedLoadoutHelper: GetLoadout returned null for " +
                    pawn.LabelShortCap);
            }
            return result;
        }

        private static object GetPersonalLoadout(object multiLoadout)
        {
            var result = _personalLoadoutProp.GetValue(multiLoadout);
            if (result == null)
            {
                Log.Error("[EM] CEExtendedLoadoutHelper: PersonalLoadout is null.");
            }
            return result;
        }

        private static void RemoveManagedSlots(object personalLoadout,
            HashSet<string> managedSlotKeys)
        {
            if (_slotsOnLoadout.GetValue(personalLoadout, null) is not IList slotsObj || managedSlotKeys.Count == 0) { return; }
            for (var i = slotsObj.Count - 1; i >= 0; i--)
            {
                var slotObj = slotsObj[i];
                if (slotObj == null) { continue; }
                if (_slotThingDef.GetValue(slotObj) is not Def def) { continue; }
                var key = def is ThingDef td
                    ? $"thingdef:{td.defName}"
                    : $"genericdef:{def.defName}";
                if (managedSlotKeys.Contains(key)) { slotsObj.RemoveAt(i); }
            }
        }

        private static void NotifyChanged(object multiLoadout)
        {
            _ = _notifyChanged.Invoke(multiLoadout, null);
            var allMultiProp = AccessTools.Property(_loadoutMultiManagerType, "LoadoutsMulti");
            if (allMultiProp?.GetValue(null) is IEnumerable allMulti)
            {
                foreach (var lm in allMulti) { _ = _notifyChanged.Invoke(lm, null); }
            }
        }
    }
}
