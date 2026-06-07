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
    /// Перед тем как JobGiver_Work выдаёт задание, убеждаемся что у пешки
    /// есть нужный инструмент. Если нет — ставим pickup-задание в очередь
    /// и возвращаем ThinkResult.NoJob (пешка сначала идёт за инструментом).
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    [UsedImplicitly]
    internal static class JobGiverToolPatch
    {
        [UsedImplicitly]
        public static bool Prefix(Pawn pawn, JobIssueParams jobParams,
                                  ref ThinkResult __result)
        {
            if (pawn?.Map == null || !pawn.Map.IsPlayerHome)  return true;
            if (pawn.Faction != Faction.OfPlayer)             return true;
            if (!pawn.DevelopmentalStage.Adult())             return true;
            if (pawn.workSettings == null)                    return true;

            var mapComp = pawn.Map.GetComponent<EquipmentManagerMapComponent>();
            if (mapComp == null) return true;

            var workType = GetCurrentWorkType(pawn);
            if (workType == null) return true;

            var needsPickup = mapComp.EnsureToolForWorkType(pawn, workType);
            if (needsPickup)
            {
                __result = ThinkResult.NoJob;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Возвращает WorkTypeDef с наивысшим приоритетом среди активных у пешки.
        /// Это соответствует тому, что выберет JobGiver_Work в своём внутреннем цикле.
        /// </summary>
        private static WorkTypeDef GetCurrentWorkType(Pawn pawn)
        {
            return WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .FirstOrDefault(wt => wt.visible && pawn.workSettings.WorkIsActive(wt));
        }
    }
}
