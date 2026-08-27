using System.Collections.Generic;
using System.Linq;
using System.Text;
using DubsBadHygiene;
using RimWorld;
using UnityEngine;
using Verse;

namespace DBHPipedWaste
{
    public class FermentationLine : IExposable
    {
        public bool occupied;
        public float progress;

        public bool Ready => occupied && progress >= 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref occupied, "occupied", false);
            Scribe_Values.Look(ref progress, "progress", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!occupied)
                {
                    progress = 0f;
                }
                else if (float.IsNaN(progress) || float.IsInfinity(progress))
                {
                    progress = 0f;
                }
                else
                {
                    progress = Mathf.Clamp01(progress);
                }
            }
        }

        public void Clear()
        {
            occupied = false;
            progress = 0f;
        }
    }

    public class Building_PipedComposter : Building
    {
        public const int StandardLineCount = 5;
        public const int BatchSize = 50;
        public const int FermentationTickInterval = 250;
        public const float ProgressPerTick = 3.3333333E-06f;
        public const float MinIdealTemperature = 19f;

        public List<FermentationLine> fermentationLines = new List<FermentationLine>();

        public CompPipedComposterSewage SewageComp => GetComp<CompPipedComposterSewage>();

        public bool HasReadyLine => fermentationLines != null && fermentationLines.Any(line => line != null && line.Ready);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref fermentationLines, "fermentationLines", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureFermentationLineInvariant();
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            EnsureFermentationLineInvariant();
        }

        private void EnsureFermentationLineInvariant()
        {
            if (fermentationLines == null)
            {
                fermentationLines = new List<FermentationLine>();
            }
            for (int i = 0; i < fermentationLines.Count; i++)
            {
                if (fermentationLines[i] == null)
                {
                    fermentationLines[i] = new FermentationLine();
                }
            }
            while (fermentationLines.Count < StandardLineCount)
            {
                fermentationLines.Add(new FermentationLine());
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(FermentationTickInterval))
            {
                FermentationTick();
            }
        }

        private void FermentationTick()
        {
            EnsureFermentationLineInvariant();
            CompPipedComposterSewage sewage = SewageComp;
            if (sewage != null && !sewage.TransferMode)
            {
                bool wasCompletelyEmpty = fermentationLines.All(line => !line.occupied);
                for (int i = 0; i < StandardLineCount && sewage.sewageBuffer >= BatchSize; i++)
                {
                    FermentationLine line = fermentationLines[i];
                    if (!line.occupied)
                    {
                        if (wasCompletelyEmpty)
                        {
                            CompTemperatureRuinable temperature = GetComp<CompTemperatureRuinable>();
                            temperature?.Reset();
                            wasCompletelyEmpty = false;
                        }
                        sewage.sewageBuffer -= BatchSize;
                        line.occupied = true;
                        line.progress = 0f;
                    }
                }
            }

            float advance = FermentationTickInterval * ProgressPerTick * CurrentTemperatureFactor;
            foreach (FermentationLine line in fermentationLines)
            {
                if (line.occupied && !line.Ready)
                {
                    line.progress = Mathf.Min(1f, line.progress + advance);
                }
            }

            for (int i = fermentationLines.Count - 1; i >= StandardLineCount; i--)
            {
                if (!fermentationLines[i].occupied)
                {
                    fermentationLines.RemoveAt(i);
                }
            }
        }

        private float CurrentTemperatureFactor
        {
            get
            {
                CompProperties_TemperatureRuinable properties = def.GetCompProperties<CompProperties_TemperatureRuinable>();
                if (properties == null)
                {
                    return 1f;
                }
                float temperature = AmbientTemperature;
                if (temperature < properties.minSafeTemperature)
                {
                    return 0.1f;
                }
                if (temperature < MinIdealTemperature)
                {
                    return GenMath.LerpDouble(properties.minSafeTemperature, MinIdealTemperature, 0.1f, 1f, temperature);
                }
                return 1f;
            }
        }

        protected override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            if (signal == "RuinedByTemperature")
            {
                foreach (FermentationLine line in fermentationLines)
                {
                    line.Clear();
                }
            }
        }

        public int UnloadReadyToMap(Map map, IntVec3 near)
        {
            if (map == null || fermentationLines == null)
            {
                return 0;
            }
            int placedTotal = 0;
            foreach (FermentationLine line in fermentationLines)
            {
                if (line == null || !line.Ready)
                {
                    continue;
                }
                int placed = SewageDisposalUtility.PlaceStacks(DubDef.Biosolids, BatchSize, near, map);
                if (placed <= 0)
                {
                    break;
                }
                // A partially placed batch is consumed as well, preventing the
                // same product from being generated again on the next unload.
                line.Clear();
                placedTotal += placed;
            }
            return placedTotal;
        }

        public void SettleResources(DestroyMode mode, Map previousMap, float sewageBuffer)
        {
            int completed = 0;
            int incomplete = 0;
            foreach (FermentationLine line in fermentationLines)
            {
                if (line.Ready)
                {
                    completed += BatchSize;
                }
                else if (line.occupied)
                {
                    incomplete += BatchSize;
                }
                line.Clear();
            }

            if (SewageDisposalUtility.IsRecoverable(mode))
            {
                SewageDisposalUtility.PlaceStacks(DubDef.FecalSludge, Mathf.CeilToInt(SewageDisposalUtility.SanitizeAmount(sewageBuffer)) + incomplete, Position, previousMap);
                SewageDisposalUtility.PlaceStacks(DubDef.Biosolids, completed, Position, previousMap);
            }
            else
            {
                SewageDisposalUtility.SpillToSewageGrid(previousMap, this.OccupiedRect(), SewageDisposalUtility.SanitizeAmount(sewageBuffer) + incomplete);
                SewageDisposalUtility.PlaceStacks(DubDef.Biosolids, completed, Position, previousMap);
            }
        }

        public override string GetInspectString()
        {
            StringBuilder builder = new StringBuilder(base.GetInspectString());
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }
            builder.AppendLine("DBHPW_LinesHeader".Translate());
            for (int i = 0; i < fermentationLines.Count; i++)
            {
                FermentationLine line = fermentationLines[i];
                string state = !line.occupied
                    ? "DBHPW_LineEmpty".Translate().ToString()
                    : line.Ready ? "DBHPW_LineReady".Translate().ToString() : line.progress.ToStringPercent("F0");
                builder.AppendLine("DBHPW_LineInspect".Translate(i + 1, state));
            }
            builder.AppendLine("Temperature".Translate() + ": " + AmbientTemperature.ToStringTemperature("F0"));
            CompTemperatureRuinable temperature = GetComp<CompTemperatureRuinable>();
            if (temperature != null)
            {
                builder.Append("DBHPW_IdealTemperature".Translate(
                    MinIdealTemperature.ToStringTemperature("F0"),
                    temperature.Props.maxSafeTemperature.ToStringTemperature("F0")));
            }
            return builder.ToString().TrimEndNewlines();
        }
    }
}
