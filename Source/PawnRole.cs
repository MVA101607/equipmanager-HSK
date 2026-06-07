using System.Collections.Generic;
using Verse;

namespace EquipmentManager
{
    /// <summary>Режим автоназначения пешки: что именно менеджер обрабатывает.</summary>
    public enum AssignMode
    {
        Both     = 0,  // оружие + инструмент (по умолчанию)
        Weapon   = 1,  // только оружие
        Tool     = 2,  // только инструмент
        NoAction = 3   // ничего не делать
    }

    internal class PawnRole : IExposable
    {
        public bool Automatic;
        public int? RoleId;
        public Pawn Pawn;
        public AssignMode Mode = AssignMode.Both;

        // Слоты PersonalLoadout (CE ExtendedLoadout), которыми управляет Equipment Manager.
        // Игрок может добавлять свои слоты — их мод не трогает.
        // Формат ключей:
        //   thingdef:Gun_AK74
        //   genericdef:GenericAmmo_762x39
        public HashSet<string> ManagedPersonalLoadoutSlots = new();

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, nameof(Pawn));
            // XML-тег "LoadoutId" сохранён для совместимости с существующими сейвами
            Scribe_Values.Look(ref RoleId, "LoadoutId");
            Scribe_Values.Look(ref Automatic, nameof(Automatic));
            Scribe_Values.Look(ref Mode, nameof(Mode));
            Scribe_Collections.Look(ref ManagedPersonalLoadoutSlots, nameof(ManagedPersonalLoadoutSlots), LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ManagedPersonalLoadoutSlots ??= new HashSet<string>();
            }
        }
    }
}
