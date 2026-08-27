using System.Collections.Generic;
using DubsBadHygiene;
using UnityEngine;
using Verse;

namespace DBHPipedWaste
{
    public static class SewageNetworkUtility
    {
        public const int AutomaticSupplyCheckIntervalTicks = 30;
        public const float AutomaticSupplyMaxPulse = 10f;
        public const float AutomaticSupplyEpsilon = 0.01f;

        public static IEnumerable<CompUndergroundSewageStorage> UndergroundPits(PlumbingNet network)
        {
            if (network?.PipedThings == null)
            {
                yield break;
            }

            foreach (ThingWithComps thing in network.PipedThings)
            {
                CompUndergroundSewageStorage pit = thing?.TryGetComp<CompUndergroundSewageStorage>();
                if (pit != null)
                {
                    yield return pit;
                }
            }
        }

        public static bool TryFulfillProductionPulse(CompPipedSewageHandler requester, float pulseAmount)
        {
            if (requester == null || !requester.CanRequestAutomaticSupply)
            {
                return false;
            }

            PlumbingNet network = requester.PipeComp?.pipeNet;
            if (network?.PipedThings == null)
            {
                return false;
            }

            pulseAmount = Mathf.Min(AutomaticSupplyMaxPulse, SewageDisposalUtility.SanitizeAmount(pulseAmount));
            if (pulseAmount <= AutomaticSupplyEpsilon)
            {
                return false;
            }

            CompUndergroundSewageStorage selectedPit = SelectHighestStoragePit(network);
            if (selectedPit == null)
            {
                return false;
            }

            // Automatic supply is deliberately an internal DBHPW transfer.  The
            // requester receives directly from the fullest pit; no third-party
            // handler state is changed and the native network distributor is not used.
            return TryTransferInternal(selectedPit, requester, pulseAmount, out _);
        }

        /// <summary>
        /// Sends sewage out of a building in transfer mode using only DBHPW's own
        /// handlers. This is an inventory move, not a DBH network push: third-party
        /// handlers are neither inspected nor temporarily blocked.
        /// </summary>
        public static bool TryTransferSewage(CompPipedSewageHandler source, float requested, out float moved)
        {
            moved = 0f;
            if (source == null || source.PipeComp?.pipeNet == null || requested <= 0f)
            {
                return false;
            }

            PlumbingNet network = source.PipeComp.pipeNet;
            if (network.PipedThings == null)
            {
                return false;
            }

            List<CompPipedSewageHandler> candidates = new List<CompPipedSewageHandler>();
            int highestPriority = int.MinValue;
            foreach (ThingWithComps thing in network.PipedThings)
            {
                CompPipedSewageHandler sewer = thing?.TryGetComp<CompPipedSewageHandler>();
                if (sewer == null || sewer == source || sewer.parent == source.parent ||
                    sewer.PipeComp?.pipeNet != network || sewer.parent == null || !sewer.parent.Spawned || sewer.parent.Destroyed)
                {
                    continue;
                }

                // Refresh before priority selection. A receiver can become
                // unavailable because of power, fuel, breakdown or transfer mode;
                // such a receiver must not hide a lower-priority usable target.
                sewer.RefreshBlockedState();
                if (sewer.Blocked)
                {
                    continue;
                }
                float free = sewer.AutomaticSupplyFreeCapacity;
                if (free <= AutomaticSupplyEpsilon)
                {
                    continue;
                }

                int priority = sewer.Props.priority;
                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    candidates.Clear();
                }
                if (priority == highestPriority)
                {
                    candidates.Add(sewer);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            // Keep the existing highest-priority semantics for manual transfer,
            // but distribute only among DBHPW handlers using capacity-aware
            // internal inventory moves.
            float remaining = SewageDisposalUtility.SanitizeAmount(requested);
            while (remaining > AutomaticSupplyEpsilon && candidates.Count > 0)
            {
                List<CompPipedSewageHandler> available = new List<CompPipedSewageHandler>();
                foreach (CompPipedSewageHandler candidate in candidates)
                {
                    if (!candidate.Blocked && candidate.AutomaticSupplyFreeCapacity > AutomaticSupplyEpsilon)
                    {
                        available.Add(candidate);
                    }
                }
                if (available.Count == 0)
                {
                    break;
                }

                float share = remaining / available.Count;
                float roundMoved = 0f;
                foreach (CompPipedSewageHandler candidate in available)
                {
                    if (TryTransferInternal(source, candidate, share, out float amount))
                    {
                        roundMoved += amount;
                        remaining -= amount;
                    }
                }
                if (roundMoved <= AutomaticSupplyEpsilon)
                {
                    break;
                }
            }
            moved = SewageDisposalUtility.SanitizeAmount(requested) - remaining;
            return moved > AutomaticSupplyEpsilon;
        }

        private static CompUndergroundSewageStorage SelectHighestStoragePit(PlumbingNet network)
        {
            CompUndergroundSewageStorage selected = null;
            foreach (CompUndergroundSewageStorage pit in UndergroundPits(network))
            {
                if (!pit.CanServeAutomaticPulse || pit.PipeComp?.pipeNet != network || !network.PipedThings.Contains(pit.parent))
                {
                    continue;
                }
                if (selected == null || pit.sewageBuffer > selected.sewageBuffer ||
                    (pit.sewageBuffer == selected.sewageBuffer && string.CompareOrdinal(pit.parent.ThingID, selected.parent.ThingID) < 0))
                {
                    selected = pit;
                }
            }
            return selected;
        }

        private static bool TryTransferInternal(CompPipedSewageHandler source, CompPipedSewageHandler receiver, float requested, out float moved)
        {
            moved = 0f;
            if (source == null || receiver == null || source == receiver || source.parent == receiver.parent ||
                source.PipeComp?.pipeNet == null || source.PipeComp.pipeNet != receiver.PipeComp?.pipeNet ||
                source.parent == null || receiver.parent == null || !source.parent.Spawned || source.parent.Destroyed ||
                !receiver.parent.Spawned || receiver.parent.Destroyed)
            {
                return false;
            }

            float amount = SewageDisposalUtility.SanitizeAmount(requested);
            float sourceAvailable = SewageDisposalUtility.SanitizeAmount(source.sewageBuffer);
            float receiverCurrent = SewageDisposalUtility.SanitizeAmount(receiver.sewageBuffer);
            float receiverFree = Mathf.Max(0f, receiver.Capacity - receiverCurrent);
            amount = Mathf.Min(amount, sourceAvailable, receiverFree);
            if (amount <= AutomaticSupplyEpsilon)
            {
                return false;
            }

            // Commit both sides together only after all validation has passed.
            source.sewageBuffer = Mathf.Max(0f, sourceAvailable - amount);
            receiver.sewageBuffer = Mathf.Min(receiver.Capacity, receiverCurrent + amount);
            moved = amount;
            receiver.NotifyPotentialSewageIncrease();
            source.RefreshBlockedState();
            receiver.RefreshBlockedState();
            return true;
        }

    }
}
