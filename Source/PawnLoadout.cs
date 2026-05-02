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

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, nameof(Pawn));
            Scribe_Values.Look(ref LoadoutId, nameof(LoadoutId));
            Scribe_Values.Look(ref Automatic, nameof(Automatic));
            Scribe_Collections.Look(ref ManagedWeapons, nameof(ManagedWeapons), LookMode.Value);
        }
    }
}