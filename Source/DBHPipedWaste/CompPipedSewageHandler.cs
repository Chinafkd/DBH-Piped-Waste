using System.Collections.Generic;
using System.Text;
using DubsBadHygiene;
using RimWorld;
using UnityEngine;
using Verse;

namespace DBHPipedWaste
{
    public class CompPipedSewageHandler : CompSewageHandler
    {
        private bool transferMode;
        private bool resourcesSettled;
        private float configuredPowerConsumption;

        public CompProperties_PipedSewageHandler PipedProps => props as CompProperties_PipedSewageHandler;

        protected virtual bool IntakeWorkingNow
        {
            get
            {
                if (parent == null)
                {
                    return false;
                }
                if (!FlickUtility.WantsToBeOn(parent))
                {
                    return false;
                }
                if (breakdownableComp != null && breakdownableComp.BrokenDown)
                {
                    return false;
                }
                if (fuelComp != null && !fuelComp.HasFuel)
                {
                    return false;
                }
                return !DBHPipedWasteMod.PumpsRequirePower || powerComp == null || powerComp.PowerOn;
            }
        }

        public bool IsIntakeWorkingForDebug => IntakeWorkingNow;

        public bool TransferMode => transferMode;

        private float DesiredPowerConsumption
        {
            get
            {
                if (PipedProps == null)
                {
                    return 0f;
                }

                float desired = Mathf.Max(0f, PipedProps.baseMachinePowerConsumption);
                if (DBHPipedWasteMod.PumpsRequirePower)
                {
                    desired += Mathf.Max(0f, PipedProps.pumpPowerConsumption);
                }
                return Mathf.Max(0f, desired);
            }
        }

        public void SetTransferMode(bool value)
        {
            transferMode = value;
            UpdateBlocked();
        }

        protected virtual float TransferableSewage => sewageBuffer;

        protected virtual bool AdditionalBlockedReason => false;

        protected virtual bool TransferActive => TransferMode;

        public virtual bool IsAutomaticSupplyProductionReceiver => true;

        public float Capacity => Mathf.Max(0f, PipedProps?.capacity ?? 0f);

        public float StoredSewage => SewageDisposalUtility.SanitizeAmount(sewageBuffer);

        public float AutomaticSupplyFreeCapacity => Mathf.Max(0f, Capacity - StoredSewage);

        public bool CanRequestAutomaticSupply => IsAutomaticSupplyProductionReceiver && PipedProps != null && Capacity > SewageNetworkUtility.AutomaticSupplyEpsilon &&
            parent != null && parent.Spawned && !parent.Destroyed &&
            PipeComp?.pipeNet != null && !TransferMode && IntakeWorkingNow && !AdditionalBlockedReason &&
            AutomaticSupplyFreeCapacity > SewageNetworkUtility.AutomaticSupplyEpsilon;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref transferMode, "transferMode", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                sewageBuffer = SewageDisposalUtility.SanitizeAmount(sewageBuffer);
                Blocked = true;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            RefreshPowerSetting();
        }

        public void RefreshPowerSetting()
        {
            configuredPowerConsumption = DesiredPowerConsumption;
            if (powerComp != null)
            {
                powerComp.PowerOutput = -configuredPowerConsumption;
                EnsureZeroPowerState();
            }
            RefreshBlockedState();
        }

        /// <summary>
        /// A zero-load CompPowerTrader must not present the vanilla
        /// "needs power" overlay.  The component is retained on the thing
        /// so that the setting can be switched back to a powered pump at
        /// runtime, therefore we explicitly mark it as powered while its
        /// configured demand is zero.
        /// </summary>
        private void EnsureZeroPowerState()
        {
            if (powerComp != null && Mathf.Approximately(configuredPowerConsumption, 0f) && !powerComp.PowerOn)
            {
                powerComp.PowerOn = true;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent == null)
            {
                return;
            }
            if (parent.IsHashIntervalTick(60))
            {
                float desired = DesiredPowerConsumption;
                if (!Mathf.Approximately(configuredPowerConsumption, desired))
                {
                    configuredPowerConsumption = desired;
                }
                if (powerComp != null && !Mathf.Approximately(powerComp.PowerOutput, -configuredPowerConsumption))
                {
                    powerComp.PowerOutput = -configuredPowerConsumption;
                }
                EnsureZeroPowerState();
            }
            sewageBuffer = SewageDisposalUtility.SanitizeAmount(sewageBuffer);
            UpdateBlocked();
            if (parent.IsHashIntervalTick(SewageNetworkUtility.AutomaticSupplyCheckIntervalTicks) && CanRequestAutomaticSupply)
            {
                SewageNetworkUtility.TryFulfillProductionPulse(this, SewageNetworkUtility.AutomaticSupplyMaxPulse);
            }
            if (parent.IsHashIntervalTick(10) && TransferActive)
            {
                TryTransferOneUnit();
            }
        }

        protected virtual void UpdateBlocked()
        {
            Blocked = TransferActive || !IntakeWorkingNow || AutomaticSupplyFreeCapacity <= SewageNetworkUtility.AutomaticSupplyEpsilon || AdditionalBlockedReason;
        }

        public void RefreshBlockedState()
        {
            sewageBuffer = SewageDisposalUtility.SanitizeAmount(sewageBuffer);
            UpdateBlocked();
        }

        public virtual void NotifyPotentialSewageIncrease()
        {
        }

        private void TryTransferOneUnit()
        {
            Blocked = true;
            float amount = Mathf.Min(SewageDisposalUtility.ManualTransferUnit, SewageDisposalUtility.SanitizeAmount(TransferableSewage));
            if (amount <= 0f)
            {
                return;
            }

            // TryTransferSewage commits both source and receiver atomically.
            SewageNetworkUtility.TryTransferSewage(this, amount, out _);
        }

        private void OpenDebugSewageInjector()
        {
            Find.WindowStack.Add(new Dialog_Slider(
                value => "DBHPW_DebugInjectAmount".Translate(value),
                1,
                10000,
                DebugInjectSewage,
                Mathf.RoundToInt(SewageDisposalUtility.ExtractionBatchSize)));
        }

        private void DebugInjectSewage(int amount)
        {
            PlumbingNet network = PipeComp?.pipeNet;
            if (network == null)
            {
                Messages.Message("DBHPW_DebugInjectNoNetwork".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (network.PushSewage(amount))
            {
                Messages.Message("DBHPW_DebugInjectSuccess".Translate(amount), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("DBHPW_DebugInjectNoReceiver".Translate(amount), MessageTypeDefOf.RejectInput);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            yield return new Command_Toggle
            {
                defaultLabel = "DBHPW_TransferMode".Translate(),
                defaultDesc = "DBHPW_TransferModeDesc".Translate(),
                icon = TransferMode ? TexCommand.ForbidOn : TexCommand.ForbidOff,
                isActive = () => TransferMode,
                toggleAction = delegate
                {
                    SetTransferMode(!TransferMode);
                }
            };
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DBHPW_DebugInjectSewage".Translate(),
                    defaultDesc = "DBHPW_DebugInjectSewageDesc".Translate(),
                    icon = TexCommand.DesirePower,
                    action = OpenDebugSewageInjector
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("DBHPW_BufferInspect".Translate(sewageBuffer.ToString("0.0"), PipedProps.capacity.ToString("0.0")));
            if (TransferMode)
            {
                builder.AppendLine();
                builder.Append("DBHPW_TransferActive".Translate());
            }
            return builder.ToString();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (!resourcesSettled)
            {
                resourcesSettled = true;
                SettleResources(mode, previousMap);
            }
            base.PostDestroy(mode, previousMap);
        }

        protected virtual void SettleResources(DestroyMode mode, Map previousMap)
        {
            float amount = SewageDisposalUtility.SanitizeAmount(sewageBuffer);
            if (SewageDisposalUtility.IsRecoverable(mode))
            {
                SewageDisposalUtility.PlaceStacks(DubDef.FecalSludge, Mathf.CeilToInt(amount), parent.Position, previousMap);
            }
            else
            {
                SewageDisposalUtility.SpillToSewageGrid(previousMap, parent.OccupiedRect(), amount);
            }
            sewageBuffer = 0f;
        }
    }
}
