using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
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

        private static void LogAllFields(Type type)
        {
            if (type == null) { return; }
            var sb = new System.Text.StringBuilder();
            _ = sb.AppendLine("[EM] Fields of " + type.FullName + ":");
            foreach (var f in type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance))
            {
                _ = sb.Append("  ");
                _ = sb.Append(f.IsStatic ? "static " : "       ");
                _ = sb.Append(f.FieldType.Name);
                _ = sb.Append(" ");
                _ = sb.AppendLine(f.Name);
            }
            Log.Message(sb.ToString());
        }

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

            Log.Message("[EM] CEExtendedLoadoutHelper init:" +
                " _loadoutMultiManagerType=" + (_loadoutMultiManagerType != null) +
                " _loadoutMultiType=" + (_loadoutMultiType != null) +
                " _loadoutType=" + (loadoutType != null) +
                " _utilType=" + (utilType != null) +
                " _slotType=" + (slotType != null));

            

            if (_loadoutMultiManagerType == null || _loadoutMultiType == null ||
                loadoutType == null || utilType == null || slotType == null)
            {
                Log.Message("[EM] CEExtendedLoadoutHelper: required types not found, disabled.");
                _available = false;
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

            LogAllFields(slotType);

            Log.Message("[EM] CEExtendedLoadoutHelper members:" +
                " _personalLoadoutProp=" + (_personalLoadoutProp != null) +
                " _notifyChanged=" + (_notifyChanged != null) +
                " _addSlot=" + (_addSlot != null) +
                " _slotCtor=" + (_slotCtor != null) +
                " _slotThingDef=" + (_slotThingDef != null) +
                " _slotsOnLoadout=" + (_slotsOnLoadout != null));

            _available = _ceGetLoadout != null &&
                         _personalLoadoutProp != null &&
                         _notifyChanged != null &&
                         _addSlot != null &&
                         _slotCtor != null &&
                         _slotThingDef != null &&
                         _slotsOnLoadout != null;

            Log.Message("[EM] CEExtendedLoadoutHelper.IsAvailable = " + _available);
            return _available;
        }

        public static bool SetPrimaryWeaponInPersonalLoadout(
            [NotNull] Pawn pawn,
            [NotNull] ThingDef weaponDef)
        {
            if (!IsAvailable()) { return false; }
            try
            {
                // GetLoadout перехвачен ExtendedLoadout Harmony-патчем
                // и возвращает Loadout_Multi под видом Loadout
                var loadout = _ceGetLoadout.Invoke(null, new object[] { pawn });

                if (loadout == null)
                {
                    Log.Warning("[EM] GetLoadout returned null for " + pawn.LabelShortCap);
                    return false;
                }

                if (loadout.GetType() != _loadoutMultiType)
                {
                    Log.Warning("[EM] GetLoadout returned " + loadout.GetType().FullName +
                        " instead of Loadout_Multi for " + pawn.LabelShortCap +
                        ". ExtendedLoadout Harmony patch may not be active.");
                    return false;
                }

                var personalLoadout = _personalLoadoutProp.GetValue(loadout, null);
                if (personalLoadout == null)
                {
                    Log.Warning("[EM] PersonalLoadout is null for " + pawn.LabelShortCap);
                    return false;
                }

                // Удалить все weapon-слоты из PersonalLoadout перед записью нового
                var slotsObj = _slotsOnLoadout.GetValue(personalLoadout, null);
                var slots = slotsObj is IList list ? list : null;
                if (slots != null)
                {
                    for (var i = slots.Count - 1; i >= 0; i--)
                    {
                        var slot = slots[i];
                        if (slot == null) { continue; }
                        var def = _slotThingDef.GetValue(slot) is ThingDef td ? td : null;
                        if (def != null && def.IsWeapon) { slots.RemoveAt(i); }
                    }
                }

                // Добавить слот нового оружия (count=1)
                var newSlot = _slotCtor.Invoke(new object[] { weaponDef, 1 });
                _ = _addSlot.Invoke(personalLoadout, new object[] { newSlot });

                // Обновить объединённый кэш Slots в Loadout_Multi
                _ = _notifyChanged.Invoke(loadout, null);

                // Диагностика: проверяем что Slots обновились
                try
                {
                    var updatedSlotsObj = _slotsOnLoadout.GetValue(loadout, null);
                    var updatedSlots = updatedSlotsObj as IList;
                    if (updatedSlots != null)
                    {
                        var sb = new System.Text.StringBuilder();
                        _ = sb.AppendLine("[EM] Loadout_Multi.Slots after NotifyLoadoutChanged" +
                            " for " + pawn.LabelShortCap + " (" + updatedSlots.Count + " slots):");
                        foreach (var s in updatedSlots)
                        {
                            if (s == null) { continue; }
                            var d = _slotThingDef.GetValue(s) as ThingDef;
                            _ = sb.AppendLine("  " + (d == null ? "null" : d.defName));
                        }
                        Log.Message(sb.ToString());
                    }
                    else
                    {
                        Log.Warning("[EM] Loadout_Multi.Slots is null after NotifyLoadoutChanged");
                    }
                }
                catch (Exception diagEx)
                {
                    Log.Warning("[EM] Slots diagnostics failed: " + diagEx.Message);
                }

                Log.Message("[EM] PersonalLoadout updated: " +
                    pawn.LabelShortCap + " -> " + weaponDef.defName);
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
