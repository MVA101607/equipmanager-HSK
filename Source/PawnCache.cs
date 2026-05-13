using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace EquipmentManager
{
    internal class PawnCache
    {
        private readonly RimworldTime _updateTime = new(-1, -1, -1);
        public readonly Dictionary<Thing, int>    AssignedAmmo    = new();
        public readonly Dictionary<Thing, string> AssignedWeapons = new();
        public Role AssignedRole;
        public bool AutoRole;
        public bool ShouldUpdateEquipment;

        // ── Временное резервирование оружия ───────────────────────────────
        // key   = зарезервированное оружие
        // value = игровой тик, после которого резерв истекает (6 игровых часов)
        public readonly Dictionary<Thing, int> ReservedWeapons = new();

        // 6 игровых часов = 6 * 2500 тиков
        private const int ReservationDurationTicks = 6 * GenDate.TicksPerHour;

        public PawnCache(Pawn pawn)
        {
            Pawn = pawn;
        }

        public Dictionary<Role, float> AvailableRoles { get; } = new();

        private static EquipmentManagerGameComponent EquipmentManager =>
            Current.Game.GetComponent<EquipmentManagerGameComponent>();

        public Pawn Pawn { get; }

        public bool IsAvailable(Role role)
        {
            return AvailableRoles.ContainsKey(role);
        }

        /// <summary>
        /// Зарезервировать оружие на 6 игровых часов.
        /// Сбрасывает предыдущие истёкшие резервы автоматически.
        /// </summary>
        public void ReserveWeapon(Thing weapon)
        {
            PurgeExpiredReservations();
            // Если оружие уже у пешки — резерв не нужен
            if (Pawn.equipment?.AllEquipmentListForReading.Contains(weapon) == true ||
                Pawn.inventory?.innerContainer.Contains(weapon) == true) { return; }
            ReservedWeapons[weapon] = Find.TickManager.TicksGame + ReservationDurationTicks;
        }

        /// <summary>
        /// Снять резерв когда пешка физически подняла оружие или резерв больше не нужен.
        /// </summary>
        public void ReleaseReservation(Thing weapon)
        {
            _ = ReservedWeapons.Remove(weapon);
        }

        /// <summary>
        /// Удалить истёкшие резервы.
        /// </summary>
        public void PurgeExpiredReservations()
        {
            var now = Find.TickManager.TicksGame;
            var expired = ReservedWeapons
                .Where(kv => kv.Value <= now)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var w in expired) { _ = ReservedWeapons.Remove(w); }

            // Снять резерв на оружие, которое пешка уже несёт
            var carried = ReservedWeapons.Keys
                .Where(w =>
                    Pawn.equipment?.AllEquipmentListForReading.Contains(w) == true ||
                    Pawn.inventory?.innerContainer.Contains(w) == true)
                .ToList();
            foreach (var w in carried) { _ = ReservedWeapons.Remove(w); }
        }

        public void Update(RimworldTime time)
        {
            PurgeExpiredReservations();

            // Пешка считается «в бою» если на карте есть активные враги
            // (GenHostility) либо её lord ведёт боевое задание.
            // В таком состоянии ежечасное обновление снаряжения пропускается:
            // пешка не должна бросать укрытие ради смены оружия.
            var isInCombat =
                (Pawn.Map != null &&
                 GenHostility.AnyHostileActiveThreatToPlayer(
                     Pawn.Map, countDormantPawnsAsHostile: false)) ||
                Pawn.GetLord()?.LordJob is LordJob_AssaultColony
                                          or LordJob_Siege
                                          or LordJob_StageThenAttack
                                          or LordJob_SleepThenAssaultColony
                                          or LordJob_DefendAndExpandHive
                                          or LordJob_MechanoidDefendBase
                                          or LordJob_MechanoidsDefend
                                          or LordJob_FormAndSendCaravan;
            var capable = !Pawn.Dead && !Pawn.Downed && !Pawn.InMentalState &&
                          !Pawn.InContainerEnclosed && !Pawn.Drafted &&
                          !isInCombat &&
                          !HealthAIUtility.ShouldSeekMedicalRest(Pawn);
            var pawnRole = EquipmentManager.GetPawnRole(Pawn);
            AutoRole = pawnRole?.Automatic ?? false;
            AssignedRole = AutoRole ? null : EquipmentManager.GetRole(pawnRole?.RoleId);
            var hoursPassed =
                ((time.Year - _updateTime.Year) * 60 * 24) +
                ((time.Day  - _updateTime.Day)  * 24) +
                (time.Hour  - _updateTime.Hour);
            ShouldUpdateEquipment = capable && hoursPassed > 6f;
            if (!ShouldUpdateEquipment) { return; }
            _updateTime.Year = time.Year;
            _updateTime.Day  = time.Day;
            _updateTime.Hour = time.Hour;
            AvailableRoles.Clear();
            foreach (var role in EquipmentManager.GetRoles())
            {
                if (role.IsAvailable(Pawn)) { AvailableRoles.Add(role, role.GetScore(Pawn)); }
            }
        }
    }
}
