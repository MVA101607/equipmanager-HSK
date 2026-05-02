using LudeonTK;
using RimWorld;
using Verse;

namespace EquipmentManager
{
    internal static class DebugActions
    {
        [DebugAction("Equipment Manager", "Force update weapons", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceUpdateWeapons()
        {
            var comp = Find.CurrentMap.GetComponent<EquipmentManagerMapComponent>();
            if (comp == null)
            {
                Log.Warning("[EM] EquipmentManagerMapComponent not found on current map.");
                return;
            }
            // Сбросить _updateTime чтобы условие hoursPassed >= 6 выполнилось
            comp.ForceUpdate();
            Log.Message("[EM] Force update triggered.");
        }
    }
}