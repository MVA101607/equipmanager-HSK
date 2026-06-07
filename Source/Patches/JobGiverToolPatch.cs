using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace EquipmentManager.Patches
{
    /// <summary>
    /// Перед тем как JobGiver выдаёт задание, убеждаемся, что у пешки
    /// есть нужный инструмент. Если нет — ставим pickup-задание в очередь
    /// и возвращаем null (пешка сначала идёт за инструментом).
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    [UsedImplicitly]
    internal static class JobGiverToolPatch
    {
        [UsedImplicitly]

        public static bool Prefix(Pawn pawn, JobIssueParams jobParams,
                                  ref ThinkResult __result)
        {
            // Только свои, взрослые пешки на домашней карте
            if (pawn?.Map == null || !pawn.Map.IsPlayerHome)
            {
                return true;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                return true;
            }

            if (!pawn.DevelopmentalStage.Adult())
            {
                return true;
            }

            if (pawn.workSettings == null)
            {
                return true;
            }

            var mapComp = pawn.Map.GetComponent<EquipmentManagerMapComponent>();
            if (mapComp == null)
            {
                return true;
            }

            // Определяем тип работы, которую JobGiver_Work собирается выдать
            // через WorkGiver, связанный с текущим рабочим списком
            var workType = GetCurrentWorkType(pawn);
            if (workType == null)
            {
                return true;
            }

            // Делегируем проверку и выдачу pickup-задания в MapComponent
            var needsPickup = mapComp.EnsureToolForWorkType(pawn, workType);
            if (needsPickup)
            {
                // Пешка получила pickup-задание; текущий JobGiver пропускаем
                __result = ThinkResult.NoJob;
                return false;
            }

            return true; // инструмент есть — продолжаем обычный поток
        }

        /// <summary>
        /// Определяет WorkTypeDef, для которого JobGiver_Work сейчас подбирает задание.
        /// Использует <see cref="Pawn_WorkSettings"/> для определения наивысшего
        /// активного WorkType в порядке приоритета.
        /// </summary>
        private static WorkTypeDef GetCurrentWorkType(Pawn pawn)
        {
            return WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .FirstOrDefault(wt => wt.visible && pawn.workSettings.WorkIsActive(wt));
        }
    }
}