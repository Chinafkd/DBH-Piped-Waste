using RimWorld;
using Verse;

namespace DBHPipedWaste
{
    [DefOf]
    public static class DBHPWDefOf
    {
        public static ThingDef DBHPW_PipedComposter;
        public static ThingDef DBHPW_PipedBiofuelRefinery;
        public static ThingDef DBHPW_UndergroundSewagePit;
        public static JobDef DBHPW_UnloadPipedComposter;
        public static JobDef DBHPW_ExtractSewage;
        public static JobDef DBHPW_RefinePipedSewage;

        static DBHPWDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DBHPWDefOf));
        }
    }
}
