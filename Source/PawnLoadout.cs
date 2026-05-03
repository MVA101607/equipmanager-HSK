using System.Collections.Generic;
using Verse;

namespace EquipmentManager
{
    internal class PawnLoadout : IExposable
    {
        public bool Automatic;
        public int? LoadoutId;
        public Pawn Pawn;

        // Слоты PersonalLoadout (CE ExtendedLoadout), которыми управляет Equipment Manager.
        // Игрок может добавлять свои слоты — их мод не трогает.
        // Формат ключей:
        //   thingdef:Gun_AK74
        //   genericdef:GenericAmmo_762x39
        public HashSet<string> ManagedPersonalLoadoutSlots = new();

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, nameof(Pawn));
            Scribe_Values.Look(ref LoadoutId, nameof(LoadoutId));
            Scribe_Values.Look(ref Automatic, nameof(Automatic));
            Scribe_Collections.Look(ref ManagedPersonalLoadoutSlots, nameof(ManagedPersonalLoadoutSlots), LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ManagedPersonalLoadoutSlots ??= new HashSet<string>();
            }
        }
    }
}
