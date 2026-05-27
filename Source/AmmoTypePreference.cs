namespace EquipmentManager
{
    public enum AmmoTypePreference
    {
        Any      = 0,
        // Огнестрельное
        FMJ      = 1,
        AP       = 2,
        HP       = 3,
        HE       = 4,
        // Стрелы луков И болты арбалетов (суффикс одинаков для обоих)
        Stone    = 10,
        Steel    = 11,
        Plasteel = 12,
        Venom    = 13,
        Flame    = 14,
    }
}
