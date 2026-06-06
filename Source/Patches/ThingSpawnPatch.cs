using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace EquipmentManager.Patches
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup)), UsedImplicitly]
    internal static class ThingSpawnSetupPatch
    {
        [UsedImplicitly]
        public static void Postfix(Thing __instance)
        {
            if (__instance?.def?.IsWeapon == true) { WorkTypeToolCache.InvalidateOnMap(); }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn)), UsedImplicitly]
    internal static class ThingDeSpawnPatch
    {
        [UsedImplicitly]
        public static void Prefix(Thing __instance)
        {
            if (__instance?.def?.IsWeapon == true) { WorkTypeToolCache.InvalidateOnMap(); }
        }
    }
}
