using DubsBadHygiene;
using Verse;

namespace DBHPipedWaste
{
    public class CompProperties_PipedSewageHandler : CompProperties_SewageHandler
    {
        public float pumpPowerConsumption;
        public float baseMachinePowerConsumption;

        public CompProperties_PipedSewageHandler()
        {
            compClass = typeof(CompPipedSewageHandler);
        }
    }
}
