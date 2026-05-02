using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using CombatExtended;
using CombatExtended.ExtendedLoadout;
using LudeonTK;
using RimWorld;
using System.Linq;
using JetBrains.Annotations;
using Verse;
#pragma warning disable IDE0019
namespace EquipmentManager
{
    internal static class CEExtendedLoadoutHelper
    {
        private static bool _initialized;
        private static bool _available;

        private static Type _loadoutMultiManagerType;
        private static Type _loadoutMultiType;

        private static MethodInfo _ceGetLoadout;
        private static PropertyInfo _personalLoadoutProp;
        private static MethodInfo _notifyChanged;
        private static MethodInfo _addSlot;
        private static ConstructorInfo _slotCtor;
        private static FieldInfo _slotThingDef;
        private static PropertyInfo _slotsOnLoadout;

        public static bool IsAvailable()
        {
            if (_initialized) { return _available; }
            _initialized = true;

            _loadoutMultiManagerType =
                AccessTools.TypeByName("CombatExtended.ExtendedLoadout.LoadoutMulti_Manager");
            _loadoutMultiType =
                AccessTools.TypeByName("CombatExtended.ExtendedLoadout.Loadout_Multi");
            var loadoutType = AccessTools.TypeByName("CombatExtended.Loadout");
            var utilType = AccessTools.TypeByName("CombatExtended.Utility_Loadouts");
            var slotType = AccessTools.TypeByName("CombatExtended.LoadoutSlot");

            if (_loadoutMultiManagerType == null || _loadoutMultiType == null ||
                loadoutType == null || utilType == null || slotType == null)
            {
                Log.Message("[EM] CEExtendedLoadoutHelper init:" +
                    " _loadoutMultiManagerType=" + (_loadoutMultiManagerType != null) +
                    " _loadoutMultiType=" + (_loadoutMultiType != null) +
                    " _loadoutType=" + (loadoutType != null) +
                    " _utilType=" + (utilType != null) +
                    " _slotType=" + (slotType != null));
                return false;
            }

            // Utility_Loadouts.GetLoadout(Pawn) — перехвачен Harmony и возвращает Loadout_Multi
            _ceGetLoadout = AccessTools.Method(utilType, "GetLoadout",
                new Type[] { typeof(Pawn) });

            // Если не нашли на Utility_Loadouts — пробуем LoadoutMulti_Manager.GetLoadout(Pawn)
            _ceGetLoadout =
            AccessTools.Method(utilType, "GetLoadout", new Type[] { typeof(Pawn) }) ??
            AccessTools.Method(_loadoutMultiManagerType, "GetLoadout", new Type[] { typeof(Pawn) });

            Log.Message("[EM] CEExtendedLoadoutHelper _ceGetLoadout: " +
                (_ceGetLoadout == null
                    ? "NOT FOUND"
                    : _ceGetLoadout.DeclaringType.FullName + "::" + _ceGetLoadout.Name));

            _personalLoadoutProp = AccessTools.Property(_loadoutMultiType, "PersonalLoadout");
            _notifyChanged = AccessTools.Method(_loadoutMultiType, "NotifyLoadoutChanged");
            _addSlot = AccessTools.Method(loadoutType, "AddSlot",
                                       new Type[] { slotType });
            _slotCtor = AccessTools.Constructor(slotType,
                                       new Type[] { typeof(ThingDef), typeof(int) });
            _slotThingDef = AccessTools.Field(slotType, "_def");
            _slotsOnLoadout = AccessTools.Property(loadoutType, "Slots");



            _available = _ceGetLoadout != null &&
                         _personalLoadoutProp != null &&
                         _notifyChanged != null &&
                         _addSlot != null &&
                         _slotCtor != null &&
                         _slotThingDef != null &&
                         _slotsOnLoadout != null;

            if (_available)
            {   Log.Message("[EM] CEExtendedLoadoutHelper IsAvailable"); }
            else
            {
                Log.Message("[EM] CEExtendedLoadoutHelper members:" +
                " _personalLoadoutProp=" + (_personalLoadoutProp != null) +
                " _notifyChanged=" + (_notifyChanged != null) +
                " _addSlot=" + (_addSlot != null) +
                " _slotCtor=" + (_slotCtor != null) +
                " _slotThingDef=" + (_slotThingDef != null) +
                " _slotsOnLoadout=" + (_slotsOnLoadout != null));
            }
            return _available;
        }

        public static bool SetPrimaryWeaponInPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] ThingDef weaponDef)
        {
            if (!IsAvailable()) { return false; }
            try
            {
                // Получить Loadout_Multi пешки (создаётся автоматически, если не было)
                if (LoadoutMulti_Manager.GetLoadout(pawn, false) is not Loadout_Multi multiLoadout)
                {
                    Log.Error("[ExtendedLoadout Test] Failed to get Loadout_Multi for pawn.");
                    return false;
                }

                var personalLoadout = multiLoadout.PersonalLoadout;

                if (personalLoadout == null)
                {
                    Log.Error("[ExtendedLoadout Test] PersonalLoadout is null.");
                    return false;
                }
                // Удалить все weapon-слоты из PersonalLoadout перед записью нового
                var slotsObj = _slotsOnLoadout.GetValue(personalLoadout, null);
                var slots = slotsObj is IList list ? list : null;
                if (slots != null)
                {
                    for (var i = slots.Count - 1; i >= 0; i--)
                    {
                        var wslot = slots[i];
                        if (wslot == null) { continue; }
                        var def = _slotThingDef.GetValue(wslot) is ThingDef td ? td : null;
                        if (def != null && def.IsWeapon) { slots.RemoveAt(i); }
                    }
                }

                // Добавить слот с оружием (count = 1)
                //    AddSlot сам объединит с существующим слотом, если такой уже есть
                var slot = new LoadoutSlot(weaponDef, 1);
                personalLoadout.AddSlot(slot);

                // Обновить объединённый кэш Slots в Loadout_Multi
                multiLoadout.NotifyLoadoutChanged();

                //  Если открыт диалог управления loadout'ами — перерисовать
                //    Dialog_ManageLoadouts сам перерисовывается каждый тик,
                //    но если нужно принудительно уведомить все Loadout_Multi:
                foreach (var lm in LoadoutMulti_Manager.LoadoutsMulti)
                {
                    lm.NotifyLoadoutChanged();
                }

                // 7. Вывести сообщение 
                //Log.Message("[EM] PersonalLoadout updated: " +
                //    pawn.Name.ToStringShort + " -> " + weaponDef.LabelCap);
                Messages.Message(
                            "EquipmentManager.WeaponEquipped".Translate(
                                pawn.Name.ToStringShort, weaponDef.LabelCap),
                            MessageTypeDefOf.SilentInput,   // без звука, не прерывает игру
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
    }
}
