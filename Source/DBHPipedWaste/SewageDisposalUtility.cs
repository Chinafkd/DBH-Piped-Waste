using System;
using DubsBadHygiene;
using UnityEngine;
using Verse;

namespace DBHPipedWaste
{
    public static class SewageDisposalUtility
    {
        public const float ExtractionBatchSize = 75f;
        public const float AutoExtractionHysteresis = 250f;
        public const float ManualTransferUnit = 1f;
        public const int OverflowTimeoutTicks = 3000;

        public static float SanitizeAmount(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
        }

        public static float SanitizePercent(float value, float fallback = 0.6f)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }
            return Mathf.Clamp01(value);
        }

        public static bool IsRecoverable(DestroyMode mode)
        {
            return mode == DestroyMode.Deconstruct || mode == DestroyMode.Refund;
        }

        public static int PlaceStacks(ThingDef def, int total, IntVec3 near, Map map)
        {
            if (def == null || map == null || total <= 0)
            {
                return 0;
            }

            int placed = 0;
            int stackLimit = Math.Max(1, def.stackLimit);
            while (total > 0)
            {
                Thing thing = ThingMaker.MakeThing(def);
                thing.stackCount = Math.Min(stackLimit, total);
                int requestedStackCount = thing.stackCount;
                total -= requestedStackCount;
                if (!GenPlace.TryPlaceThing(thing, near, map, ThingPlaceMode.Near))
                {
                    Log.Error("[DBH Piped Waste] Could not place recovered " + def.defName + ".");
                    thing.Destroy();
                }
                else
                {
                    // GenPlace may merge the temporary Thing into an existing
                    // stack and clear its stackCount. Count the requested stack
                    // captured before placement, not the post-merge Thing.
                    placed += requestedStackCount;
                }
            }
            return placed;
        }

        public static bool TrySpillToSewageGrid(Map map, CellRect occupiedRect, float amount)
        {
            amount = SanitizeAmount(amount);
            if (map == null || amount <= 0f)
            {
                return false;
            }

            MapComponent_Hygiene hygiene = map.GetComponent<MapComponent_Hygiene>();
            if (hygiene == null || hygiene.SewageGrid == null)
            {
                Log.Error("[DBH Piped Waste] Could not return sewage to DBH SewageGrid.");
                return false;
            }

            float perCell = amount / Math.Max(1, occupiedRect.Area);
            foreach (IntVec3 cell in occupiedRect.Cells)
            {
                hygiene.SewageGrid.AddAt(cell, perCell);
            }
            return true;
        }

        public static void SpillToSewageGrid(Map map, CellRect occupiedRect, float amount)
        {
            TrySpillToSewageGrid(map, occupiedRect, amount);
        }
    }
}
