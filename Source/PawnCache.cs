using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    internal class PawnCache
    {
        private static EquipmentManagerGameComponent _equipmentManager;
        private readonly RimworldTime _updateTime = new(-1, -1, -1);
        public readonly Dictionary<Thing, int> AssignedAmmo = new();
        public readonly Dictionary<Thing, string> AssignedWeapons = new();
        public Role AssignedRole;
        public bool AutoRole;
        public bool ShouldUpdateEquipment;

        public PawnCache(Pawn pawn)
        {
            Pawn = pawn;
        }

        public Dictionary<Role, float> AvailableRoles { get; } = new Dictionary<Role, float>();

        private static EquipmentManagerGameComponent EquipmentManager =>
            _equipmentManager ??= Current.Game.GetComponent<EquipmentManagerGameComponent>();

        public Pawn Pawn { get; }

        public bool IsAvailable(Role role)
        {
            return AvailableRoles.ContainsKey(role);
        }

        public void Update(RimworldTime time)
        {
            var capable = !Pawn.Dead && !Pawn.Downed && !Pawn.InMentalState && !Pawn.InContainerEnclosed &&
                !Pawn.Drafted && !HealthAIUtility.ShouldSeekMedicalRest(Pawn);
            var pawnRole = EquipmentManager.GetPawnRole(Pawn);
            AutoRole = pawnRole?.Automatic ?? false;
            AssignedRole = AutoRole ? null : EquipmentManager.GetRole(pawnRole?.RoleId);
            var hoursPassed = ((time.Year - _updateTime.Year) * 60 * 24) + ((time.Day - _updateTime.Day) * 24) +
                time.Hour - _updateTime.Hour;
            ShouldUpdateEquipment = capable && hoursPassed > 6f;
            if (!ShouldUpdateEquipment) { return; }
            _updateTime.Year = time.Year;
            _updateTime.Day = time.Day;
            _updateTime.Hour = time.Hour;
            AvailableRoles.Clear();
            foreach (var role in EquipmentManager.GetRoles())
            {
                if (role.IsAvailable(Pawn)) { AvailableRoles.Add(role, role.GetScore(Pawn)); }
            }
        }
    }
}
