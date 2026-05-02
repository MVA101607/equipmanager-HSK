using SimpleSidearms.rimworld;
using System.Collections.Generic;
using Verse;

namespace EquipmentManager
{
    internal class PawnLoadout : IExposable
    {
        public bool Automatic;
        public int? LoadoutId;
        public Pawn Pawn;

        // Оружие и инструменты, добавленные этим модом в прошлом цикле.
        // Только они удаляются при обновлении — игрок может добавлять своё.
        public HashSet<ThingDefStuffDefPair> ManagedWeapons = new();

        // Слоты PersonalLoadout, которыми управляет Equipment Manager.
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
            Scribe_Collections.Look(ref ManagedWeapons, nameof(ManagedWeapons), LookMode.Value);
            Scribe_Collections.Look(ref ManagedPersonalLoadoutSlots, nameof(ManagedPersonalLoadoutSlots), LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ManagedWeapons ??= new HashSet<ThingDefStuffDefPair>();
                ManagedPersonalLoadoutSlots ??= new HashSet<string>();
            }
        }
    }
}
