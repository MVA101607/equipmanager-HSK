using CombatExtended;
using CombatExtended.ExtendedLoadout;
using LudeonTK;
using RimWorld;
using System.Linq;
using Verse;

namespace CombatExtended.ExtendedLoadout
{

    public static class TestLoadoutHelper
    {
        /// <summary>
        /// Тестовая функция: находит первое оружие на карте, прописывает его
        /// в PersonalLoadout первого поселенца, обновляет UI.
        /// Вызвать из DevMode консоли или через [DebugAction].
        /// </summary>
        [DebugAction("Extended Loadout", "Test: Add weapon to first colonist loadout", actionType = DebugActionType.Action)]
        public static void TestAddWeaponToPersonalLoadout()
        {
            // 1. Найти первого поселенца
            var pawn = Find.CurrentMap
                .mapPawns
                .FreeColonistsSpawned  // только заспавненные на карте, не в трейдерских шипах и т.п.
                .FirstOrDefault();

            if (pawn == null)
            {
                Log.Warning("[ExtendedLoadout Test] No colonists found.");
                return;
            }

            // 2. Найти любой weaponDef на текущей карте
            var weaponDef = pawn.Map?.listerThings
                .ThingsInGroup(ThingRequestGroup.Weapon)
                .FirstOrDefault()
                ?.def;

            // Если на карте нет оружия — берём любой weaponDef из базы данных

            weaponDef ??= DefDatabase<ThingDef>.AllDefsListForReading
                .FirstOrDefault(d => d.IsWeapon && !d.destroyOnDrop);


            if (weaponDef == null)
            {
                Log.Warning("[ExtendedLoadout Test] No weapon ThingDef found anywhere.");
                return;
            }

            // 3. Получить Loadout_Multi пешки (создаётся автоматически, если не было)
            if (LoadoutMulti_Manager.GetLoadout(pawn, false) is not Loadout_Multi multiLoadout)
            {
                Log.Error("[ExtendedLoadout Test] Failed to get Loadout_Multi for pawn.");
                return;
            }

            var personalLoadout = multiLoadout.PersonalLoadout;

            if (personalLoadout == null)
            {
                Log.Error("[ExtendedLoadout Test] PersonalLoadout is null.");
                return;
            }

            // 4. Добавить слот с оружием (count = 1)
            //    AddSlot сам объединит с существующим слотом, если такой уже есть
            var slot = new LoadoutSlot(weaponDef, 1);
            personalLoadout.AddSlot(slot);

            // 5. Обновить кэш агрегированных слотов Loadout_Multi
            multiLoadout.NotifyLoadoutChanged();

            // 6. Если открыт диалог управления loadout'ами — перерисовать
            //    Dialog_ManageLoadouts сам перерисовывается каждый тик,
            //    но если нужно принудительно уведомить все Loadout_Multi:
            foreach (var lm in LoadoutMulti_Manager.LoadoutsMulti)
            {
                lm.NotifyLoadoutChanged();
            }

            // 7. Вывести сообщение в консоль
            Log.Message(
                $"[ExtendedLoadout Test] Added '{weaponDef.LabelCap}' " +
                $"to PersonalLoadout of '{pawn.Name.ToStringShort}' " +
                $"(loadout: '{personalLoadout.label}', " +
                $"total slots: {personalLoadout.SlotCount})"
            );
        }
    }
}
