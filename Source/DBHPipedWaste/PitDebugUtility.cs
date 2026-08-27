using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using DubsBadHygiene;
using Verse;

namespace DBHPipedWaste
{
    /// <summary>
    /// Developer-only diagnostics for separating Pit membership, Blocked state,
    /// and PlumbingNet identity problems. Nothing in this class changes gameplay.
    /// </summary>
    public static class PitDebugUtility
    {
        private static readonly Dictionary<string, int> LastDumpTickByFixture = new Dictionary<string, int>();

        public static bool Enabled => Prefs.DevMode;

        public static void DumpFixtureState(Thing fixture, AcceptanceReport result, PlumbingNet network)
        {
            if (!Enabled || !ShouldDump(fixture?.ThingID ?? "<unknown>"))
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[DBH Piped Waste] === FIXTURE NOSEWAGE DEBUG ===");
            builder.AppendLine("Fixture: " + DescribeThing(fixture));
            builder.AppendLine("Working accepted: " + result.Accepted + "; reason: " + (result.Reason ?? "<none>"));
            AppendNetwork(builder, fixture?.TryGetComp<CompPipe>(), network);
            AppendPits(builder, network);
            AppendSewers(builder, network);
            Log.Message(builder.ToString());
        }

        public static void DumpPitState(CompUndergroundSewageStorage pit)
        {
            if (!Enabled || pit == null || !ShouldDump(pit.parent?.ThingID ?? "<unknown-pit>"))
            {
                return;
            }

            PlumbingNet network = pit.PipeComp?.pipeNet;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[DBH Piped Waste] === MANUAL PIT DEBUG ===");
            builder.AppendLine("Focus pit: " + DescribeThing(pit.parent));
            AppendNetwork(builder, pit.PipeComp, network);
            AppendPits(builder, network);
            AppendSewers(builder, network);
            Log.Message(builder.ToString());
        }

        private static bool ShouldDump(string key)
        {
            int tick = Find.TickManager?.TicksGame ?? -1;
            if (LastDumpTickByFixture.TryGetValue(key, out int previousTick) && previousTick == tick)
            {
                return false;
            }
            if (LastDumpTickByFixture.Count > 512)
            {
                LastDumpTickByFixture.Clear();
            }
            LastDumpTickByFixture[key] = tick;
            return true;
        }

        private static void AppendNetwork(StringBuilder builder, CompPipe fixturePipe, PlumbingNet network)
        {
            builder.AppendLine("Fixture CompPipe hash: " + ObjectHash(fixturePipe));
            builder.AppendLine("Fixture pipeNet hash: " + ObjectHash(fixturePipe?.pipeNet));
            builder.AppendLine("Fixture pipeNet NetID: " + (fixturePipe?.pipeNet?.NetID.ToString() ?? "<none>"));
            builder.AppendLine("Selected network hash: " + ObjectHash(network));
            builder.AppendLine("Selected network NetID: " + (network?.NetID.ToString() ?? "<none>"));
        }

        private static void AppendPits(StringBuilder builder, PlumbingNet network)
        {
            builder.AppendLine("--- Pits ---");
            if (network?.PipedThings == null)
            {
                builder.AppendLine("PipedThings: <none>");
                return;
            }

            foreach (CompUndergroundSewageStorage pit in SewageNetworkUtility.UndergroundPits(network))
            {
                bool inPipedThings = network.PipedThings.Contains(pit.parent);
                bool inSewers = network.Sewers != null && network.Sewers.Contains(pit);
                builder.AppendLine("Pit " + DescribeThing(pit.parent) +
                    "; runtime=" + pit.GetType().FullName +
                    "; compPipeHash=" + ObjectHash(pit.PipeComp) +
                    "; pitNetHash=" + ObjectHash(pit.PipeComp?.pipeNet) +
                    "; pitNetID=" + (pit.PipeComp?.pipeNet?.NetID.ToString() ?? "<none>") +
                    "; ReferenceEquals(selectedNet,pitNet)=" + ReferenceEquals(network, pit.PipeComp?.pipeNet) +
                    "; inPipedThings=" + inPipedThings +
                    "; inSewers=" + inSewers +
                    "; Blocked=" + pit.Blocked +
                    "; WorkingNow=" + pit.WorkingNow +
                    "; IntakeWorkingNow=" + pit.IsIntakeWorkingForDebug +
                    "; buffer=" + pit.StoredSewage +
                    "; capacity=" + pit.Capacity +
                    "; free=" + pit.AutomaticSupplyFreeCapacity +
                    "; overflowPending=" + pit.OverflowPendingForDebug +
                    "; overflowAgeTicks=" + pit.OverflowAgeTicksForDebug +
                    "; transferMode=" + pit.TransferMode);
                AppendAllComps(builder, pit.parent);
            }
        }

        private static void AppendAllComps(StringBuilder builder, Thing thing)
        {
            builder.AppendLine("  AllComps:");
            ThingWithComps thingWithComps = thing as ThingWithComps;
            if (thingWithComps?.AllComps == null)
            {
                builder.AppendLine("    <none>");
                return;
            }
            foreach (ThingComp comp in thingWithComps.AllComps)
            {
                builder.AppendLine("    " + comp.GetType().FullName +
                    "; assembly=" + comp.GetType().Assembly.FullName +
                    "; isCompSewageHandler=" + (comp is CompSewageHandler));
            }
        }

        private static void AppendSewers(StringBuilder builder, PlumbingNet network)
        {
            builder.AppendLine("--- Sewers (" + (network?.Sewers?.Count ?? 0) + ") ---");
            if (network?.Sewers == null)
            {
                return;
            }
            foreach (CompSewageHandler handler in network.Sewers)
            {
                CompPipedSewageHandler pipedHandler = handler as CompPipedSewageHandler;
                builder.AppendLine("  " + DescribeThing(handler?.parent) +
                    "; runtime=" + handler?.GetType().FullName +
                    "; priority=" + (handler?.Props?.priority.ToString() ?? "<none>") +
                    "; Blocked=" + (handler?.Blocked.ToString() ?? "<none>") +
                    "; buffer=" + (handler?.sewageBuffer.ToString() ?? "<none>") +
                    "; capacity=" + (pipedHandler?.PipedProps?.capacity.ToString() ?? "<none>"));
            }
        }

        private static string DescribeThing(Thing thing)
        {
            return thing == null ? "<none>" : thing.ThingID + " (" + thing.GetType().FullName + ")";
        }

        private static int ObjectHash(object value)
        {
            return value == null ? 0 : RuntimeHelpers.GetHashCode(value);
        }
    }
}
