using HarmonyLib;
using LudeonTK;
using RimWorld;
using Verse;
using System.Linq;

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

        [DebugAction("Equipment Manager", "Check Harmony patches", allowedGameStates = AllowedGameStates.Playing)]
        private static void CheckHarmonyPatches()
        {
            var harmony = new Harmony("LordKuper.EquipmentManager"); // тот же ID, что при инициализации
            var patched = harmony.GetPatchedMethods().ToList();

            var target = typeof(JobGiver_Work).GetMethod("TryIssueJobPackage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            if (patched.Contains(target))
            {
                Log.Message("[EM] JobGiver_Work.TryIssueJobPackage — патч АКТИВЕН");
            }
            else
            {
                Log.Warning("[EM] JobGiver_Work.TryIssueJobPackage — патч НЕ НАЙДЕН");
            }
        }
    }
}