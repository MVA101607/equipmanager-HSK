using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace EquipmentManager.Patches
{
    /// <summary>
    /// Используем Postfix: позволяем игре найти конкретную цель для работы,
    /// вытаскиваем из неё WorkTypeDef и проверяем инструмент.
    /// Если инструмента нет — отменяем работу, пешка идет экипироваться.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    [UsedImplicitly]
    internal static class JobGiverToolPatch
    {
        [UsedImplicitly]
        public static void Postfix(Pawn pawn, JobIssueParams jobParams, ref ThinkResult __result)
        {
            // Если игра не нашла работу (пешка просто гуляет/отдыхает), нам проверять нечего
            if (!__result.IsValid || __result.Job == null) return;

            if (pawn?.Map == null || !pawn.Map.IsPlayerHome) return;
            if (pawn.Faction != Faction.OfPlayer) return;
            if (!pawn.DevelopmentalStage.Adult()) return;

            // Вытаскиваем WorkGiverDef из конкретного назначенного задания
            var workGiver = __result.Job.workGiverDef;
            if (workGiver == null) return;

            // Получаем точный тип работы (Mining, PlantCutting, Construction и т.д.)
            var workType = workGiver.workType;
            if (workType == null) return;

            var mapComp = pawn.Map.GetComponent<EquipmentManagerMapComponent>();
            if (mapComp == null) return;

            // Проверяем, есть ли нужный инструмент именно для текущей задачи
            var needsPickup = mapComp.EnsureToolForWorkType(pawn, workType);
            if (needsPickup)
            {
                // Отменяем текущую работу. Пешка сначала пойдет за инструментом
                __result = ThinkResult.NoJob;
            }
        }
    }
}