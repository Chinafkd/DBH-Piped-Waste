using DubsBadHygiene;
using UnityEngine;
using Verse;

namespace DBHPipedWaste
{
    public class CompPipedComposterSewage : CompPipedSewageHandler
    {
        private int overflowUnreleasedTicks;

        public CompPipedComposterSewage()
        {
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref overflowUnreleasedTicks, "overflowUnreleasedTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                overflowUnreleasedTicks = Mathf.Max(0, overflowUnreleasedTicks);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent != null && parent.IsHashIntervalTick(60))
            {
                TryRelieveOverflow();
            }
        }

        private void TryRelieveOverflow()
        {
            sewageBuffer = StoredSewage;
            float overflow = sewageBuffer - Capacity;
            if (overflow <= SewageNetworkUtility.AutomaticSupplyEpsilon)
            {
                overflowUnreleasedTicks = 0;
                return;
            }

            // Keep the full production buffer blocked while trying to route
            // only the amount above its configured capacity.
            UpdateBlocked();
            PlumbingNet network = PipeComp?.pipeNet;
            if (network != null && network.PushSewage(overflow))
            {
                sewageBuffer = Mathf.Max(0f, sewageBuffer - overflow);
                overflowUnreleasedTicks = 0;
                return;
            }

            overflowUnreleasedTicks = Mathf.Min(
                SewageDisposalUtility.OverflowTimeoutTicks,
                overflowUnreleasedTicks + 60);
            if (overflowUnreleasedTicks < SewageDisposalUtility.OverflowTimeoutTicks || parent?.Map == null)
            {
                return;
            }

            if (SewageDisposalUtility.TrySpillToSewageGrid(parent.Map, parent.OccupiedRect(), overflow))
            {
                sewageBuffer = Mathf.Max(0f, sewageBuffer - overflow);
                overflowUnreleasedTicks = 0;
            }
        }

        protected override void SettleResources(DestroyMode mode, Map previousMap)
        {
            Building_PipedComposter composter = parent as Building_PipedComposter;
            if (composter == null)
            {
                base.SettleResources(mode, previousMap);
                return;
            }
            composter.SettleResources(mode, previousMap, sewageBuffer);
            sewageBuffer = 0f;
        }
    }
}
