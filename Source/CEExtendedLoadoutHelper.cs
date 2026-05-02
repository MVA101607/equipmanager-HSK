using CombatExtended;
using CombatExtended.ExtendedLoadout;
using HarmonyLib;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            [NotNull] ThingDef weaponDef,
            [NotNull] HashSet<string> managedSlotKeys)
        {
            if (!IsAvailable()) { return false; }
            try
            {
                if (LoadoutMulti_Manager.GetLoadout(pawn, false) is not Loadout_Multi multiLoadout)
                {
                    Log.Error("[EM] Failed to get Loadout_Multi for pawn.");
                    return false;
                }

                var personalLoadout = multiLoadout.PersonalLoadout;

                if (personalLoadout == null)
                {
                    Log.Error("[EM] PersonalLoadout is null.");
                    return false;
                }

                managedSlotKeys ??= new HashSet<string>();

                var slotsObj = _slotsOnLoadout.GetValue(personalLoadout, null);
                var slots = slotsObj as IList;
                if (slots != null)
                {
                    for (var i = slots.Count - 1; i >= 0; i--)
                    {
                        var slotObj = slots[i];
                        if (slotObj == null) { continue; }

                        var defObj = _slotThingDef.GetValue(slotObj);
                        if (defObj is not Def def) { continue; }

                        string key;
#pragma warning disable IDE0045 // Преобразовать в условное выражение
                        if (def is ThingDef thingDef)
                        {
                            key = $"thingdef:{thingDef.defName}";
                        }
                        else
                        {
                            key = $"genericdef:{def.defName}";
                        }
#pragma warning restore IDE0045 // Преобразовать в условное выражение

                        if (managedSlotKeys.Contains(key))
                        {
                            slots.RemoveAt(i);
                        }
                    }
                }

                var slot = new LoadoutSlot(weaponDef, 1);
                personalLoadout.AddSlot(slot);

                managedSlotKeys.Clear();
                _ = managedSlotKeys.Add($"thingdef:{weaponDef.defName}");

                multiLoadout.NotifyLoadoutChanged();

                foreach (var lm in LoadoutMulti_Manager.LoadoutsMulti)
                {
                    lm.NotifyLoadoutChanged();
                }

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


        public static bool SetAmmoInPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] ThingDef ammoDef,
            int count)
        {
            if (!IsAvailable()) { return false; }
            if (count <= 0) { return false; }

            try
            {
                if (LoadoutMulti_Manager.GetLoadout(pawn, false) is not Loadout_Multi multiLoadout)
                {
                    Log.Error("[EM] Failed to get Loadout_Multi for pawn while setting ammo.");
                    return false;
                }

                var personalLoadout = multiLoadout.PersonalLoadout;
                if (personalLoadout == null)
                {
                    Log.Error("[EM] PersonalLoadout is null while setting ammo.");
                    return false;
                }

                var slotsObj = _slotsOnLoadout.GetValue(personalLoadout, null);
                var slots = slotsObj as IList;
                if (slots != null)
                {
                    for (var i = slots.Count - 1; i >= 0; i--)
                    {
                        var slotObj = slots[i];
                        if (slotObj == null) { continue; }
                        var def = _slotThingDef.GetValue(slotObj) as ThingDef;
                        if (def == ammoDef)
                        {
                            slots.RemoveAt(i);
                        }
                    }
                }

                var slot = new LoadoutSlot(ammoDef, count);
                personalLoadout.AddSlot(slot);

                multiLoadout.NotifyLoadoutChanged();
                foreach (var lm in LoadoutMulti_Manager.LoadoutsMulti)
                {
                    lm.NotifyLoadoutChanged();
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[EM] SetAmmoInPersonalLoadout failed for " +
                    pawn.LabelShortCap + ": " + ex.Message,
                    pawn.thingIDNumber ^ ammoDef.shortHash ^ count);
                return false;
            }
        }
    }
}
