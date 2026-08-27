using System;
using System.Collections.Generic;
using System.Text;
using DubsBadHygiene;
using RimWorld;
using UnityEngine;
using Verse;

namespace DBHPipedWaste
{
    public class CompUndergroundSewageStorage : CompPipedSewageHandler
    {
        private bool autoExtract;
        private float targetPercent = 0.6f;
        private bool autoExtractionActive;
        private bool manualExtractionRequested;
        private int overflowStartedTick = -1;

        protected override bool IntakeWorkingNow => true;

        public override bool IsAutomaticSupplyProductionReceiver => false;

        public bool CanServeAutomaticPulse => parent != null && parent.Spawned && !parent.Destroyed && PipeComp?.pipeNet != null &&
            !TransferMode && StoredSewage > SewageNetworkUtility.AutomaticSupplyEpsilon;

        public float TargetPercent => targetPercent;

        public bool AutoExtractionActive => autoExtractionActive;

        public bool AutoExtract => autoExtract;

        public bool ManualExtractionRequested => manualExtractionRequested;

        public bool HasWholeSewageUnit => Mathf.FloorToInt(StoredSewage) > 0;

        public void SetTargetPercent(float value)
        {
            targetPercent = SewageDisposalUtility.SanitizePercent(value);
            RefreshAutoExtractionState();
        }

        public void SetAutoExtract(bool value)
        {
            autoExtract = value;
            RefreshAutoExtractionState();
        }

        public void SetManualExtractionRequested(bool value)
        {
            manualExtractionRequested = value;
            if (!value)
            {
                autoExtractionActive = false;
            }
        }

        protected override void UpdateBlocked()
        {
            Blocked = TransferActive || Capacity <= SewageNetworkUtility.AutomaticSupplyEpsilon ||
                AutomaticSupplyFreeCapacity <= SewageNetworkUtility.AutomaticSupplyEpsilon;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoExtract, "autoExtract", false);
            Scribe_Values.Look(ref targetPercent, "targetPercent", 0.6f);
            Scribe_Values.Look(ref autoExtractionActive, "autoExtractionActive", false);
            Scribe_Values.Look(ref manualExtractionRequested, "manualExtractionRequested", false);
            Scribe_Values.Look(ref overflowStartedTick, "overflowStartedTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                targetPercent = SewageDisposalUtility.SanitizePercent(targetPercent);
                overflowStartedTick = Mathf.Max(-1, overflowStartedTick);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent != null && parent.IsHashIntervalTick(60))
            {
                TryRelieveOverflow(true);
                UpdateAutoExtractionState();
            }
        }

        public bool OverflowPendingForDebug => overflowStartedTick >= 0;

        public int OverflowAgeTicksForDebug
        {
            get
            {
                if (overflowStartedTick < 0)
                {
                    return 0;
                }
                int currentTick = Find.TickManager?.TicksGame ?? overflowStartedTick;
                return Mathf.Max(0, currentTick - overflowStartedTick);
            }
        }

        public override void NotifyPotentialSewageIncrease()
        {
            sewageBuffer = StoredSewage;
            if (sewageBuffer - Capacity <= SewageNetworkUtility.AutomaticSupplyEpsilon)
            {
                overflowStartedTick = -1;
                return;
            }

            if (overflowStartedTick < 0)
            {
                overflowStartedTick = Find.TickManager?.TicksGame ?? -1;
            }
            TryRelieveOverflow(false);
        }

        private void TryRelieveOverflow(bool advanceTimeout)
        {
            sewageBuffer = StoredSewage;
            float overflow = sewageBuffer - Capacity;
            if (overflow <= SewageNetworkUtility.AutomaticSupplyEpsilon)
            {
                overflowStartedTick = -1;
                return;
            }

            if (overflowStartedTick < 0)
            {
                overflowStartedTick = Find.TickManager?.TicksGame ?? -1;
            }

            // Keep the Pit blocked while it is over the hard limit. This also
            // prevents PushSewage from selecting the Pit itself as its outlet.
            UpdateBlocked();
            PlumbingNet network = PipeComp?.pipeNet;
            if (network != null && network.PushSewage(overflow))
            {
                sewageBuffer = Mathf.Max(0f, sewageBuffer - overflow);
                overflowStartedTick = -1;
                return;
            }

            if (!advanceTimeout)
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? overflowStartedTick;
            int elapsedTicks = Mathf.Max(0, currentTick - overflowStartedTick);
            if (elapsedTicks < SewageDisposalUtility.OverflowTimeoutTicks || parent?.Map == null)
            {
                return;
            }

            if (SewageDisposalUtility.TrySpillToSewageGrid(parent.Map, parent.OccupiedRect(), overflow))
            {
                sewageBuffer = Mathf.Max(0f, sewageBuffer - overflow);
                overflowStartedTick = -1;
            }
        }

        private void UpdateAutoExtractionState()
        {
            sewageBuffer = StoredSewage;
            if (!autoExtract)
            {
                autoExtractionActive = false;
                return;
            }
            if (Capacity <= SewageNetworkUtility.AutomaticSupplyEpsilon)
            {
                autoExtractionActive = false;
                return;
            }
            float target = Capacity * SewageDisposalUtility.SanitizePercent(targetPercent);
            float start = Mathf.Min(Capacity, target + SewageDisposalUtility.AutoExtractionHysteresis);
            if (!AutoExtractionActive && sewageBuffer >= start)
            {
                autoExtractionActive = true;
            }
            if (AutoExtractionActive && sewageBuffer - SewageDisposalUtility.ExtractionBatchSize < target)
            {
                autoExtractionActive = false;
            }
        }

        public void RefreshAutoExtractionState()
        {
            UpdateAutoExtractionState();
        }

        public bool WantsExtraction => ManualExtractionRequested ? HasWholeSewageUnit :
            StoredSewage >= SewageDisposalUtility.ExtractionBatchSize && AutoExtractionActive;

        public bool TryExtractBatch(Map map, IntVec3 near)
        {
            int requested = Mathf.RoundToInt(SewageDisposalUtility.ExtractionBatchSize);
            if (StoredSewage < requested)
            {
                return false;
            }
            int placed = SewageDisposalUtility.PlaceStacks(DubDef.FecalSludge, requested, near, map);
            if (placed <= 0)
            {
                return false;
            }
            sewageBuffer = Mathf.Max(0f, StoredSewage - placed);
            UpdateAutoExtractionState();
            return placed == requested;
        }

        public bool TryEmergencyExtractBatch(Map map, IntVec3 near)
        {
            if (!ManualExtractionRequested)
            {
                return false;
            }

            float stored = StoredSewage;
            int availableWholeUnits = Mathf.FloorToInt(stored);
            int requested = Mathf.Min(
                Mathf.RoundToInt(SewageDisposalUtility.ExtractionBatchSize),
                availableWholeUnits);
            if (requested <= 0)
            {
                // Keep any fractional remainder, but stop the emergency job;
                // it cannot produce a sludge item without a complete unit.
                SetManualExtractionRequested(false);
                return false;
            }

            int placed = SewageDisposalUtility.PlaceStacks(DubDef.FecalSludge, requested, near, map);
            if (placed <= 0)
            {
                return false;
            }

            // Only whole sewage units are converted. Any fractional remainder
            // stays in the Pit, and a partial placement consumes only what was
            // actually placed.
            sewageBuffer = Mathf.Max(0f, stored - placed);
            if (sewageBuffer <= SewageNetworkUtility.AutomaticSupplyEpsilon || Mathf.FloorToInt(sewageBuffer) <= 0)
            {
                SetManualExtractionRequested(false);
            }
            return true;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            yield return new Command_Toggle
            {
                defaultLabel = "DBHPW_ExtractNow".Translate(),
                defaultDesc = "DBHPW_ExtractNowDesc".Translate(),
                icon = ManualExtractionRequested ? TexCommand.ForbidOn : TexCommand.ForbidOff,
                isActive = () => ManualExtractionRequested,
                toggleAction = delegate
                {
                    SetManualExtractionRequested(!ManualExtractionRequested);
                }
            };
            yield return new Command_Toggle
            {
                defaultLabel = "DBHPW_AutoExtract".Translate(),
                defaultDesc = "DBHPW_AutoExtractDesc".Translate(),
                icon = AutoExtract ? TexCommand.ForbidOff : TexCommand.ForbidOn,
                isActive = () => AutoExtract,
                toggleAction = delegate
                {
                    SetAutoExtract(!AutoExtract);
                }
            };
            yield return new Command_SetSewageTarget
            {
                storage = this,
                defaultLabel = "DBHPW_TargetLevel".Translate(),
                defaultDesc = "DBHPW_TargetLevelDesc".Translate(),
                icon = TexCommand.DesirePower
            };
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DBHPW_DumpPitDebug".Translate(),
                    defaultDesc = "DBHPW_DumpPitDebugDesc".Translate(),
                    icon = TexCommand.DesirePower,
                    action = () => PitDebugUtility.DumpPitState(this)
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder builder = new StringBuilder(base.CompInspectStringExtra());
            builder.AppendLine();
            builder.Append("DBHPW_TargetInspect".Translate(targetPercent.ToStringPercent("F0")));
            if (ManualExtractionRequested)
            {
                builder.AppendLine();
                builder.Append("DBHPW_ManualPending".Translate());
            }
            else if (AutoExtractionActive)
            {
                builder.AppendLine();
                builder.Append("DBHPW_AutoActive".Translate());
            }
            return builder.ToString();
        }
    }
}
