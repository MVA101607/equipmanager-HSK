using RimWorld;
using Verse;

namespace EquipmentManager
{
    public class RimworldTime
    {
        public RimworldTime(int year, int day, int hour)
        {
            Year = year;
            Day = day;
            Hour = hour;
        }

        public int Day { get; set; }
        public int Hour { get; set; }
        public int Year { get; set; }

        public static RimworldTime GetMapTime(Map map)
        {
            return map == null
                ? new RimworldTime(0, 0, 0)
                : new RimworldTime(GenLocalDate.Year(map), GenLocalDate.DayOfYear(map), (int) GenLocalDate.HourFloat(map));
        }
    }
}