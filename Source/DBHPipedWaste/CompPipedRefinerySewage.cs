using RimWorld;
using UnityEngine;
using Verse;

namespace DBHPipedWaste
{
    public class CompPipedRefinerySewage : CompPipedComposterSewage
    {
        private float reservedSewage;
        private Pawn reservationPawn;

        protected override float TransferableSewage => Mathf.Max(0f, StoredSewage - ReservedSewage);

        public float AvailableForProduction => Mathf.Max(0f, StoredSewage - SewageDisposalUtility.SanitizeAmount(reservedSewage));

        public float ReservedSewage => SewageDisposalUtility.SanitizeAmount(reservedSewage);

        public Pawn ReservationPawn => reservationPawn;

        public override void CompTick()
        {
            if (reservationPawn != null && !ReservationIsValid())
            {
                ReleaseReservation(null);
            }
            base.CompTick();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            LongEventHandler.ExecuteWhenFinished(RemoveDuplicateSavedPipedBills);
        }

        private void RemoveDuplicateSavedPipedBills()
        {
            Building_WorkTable workTable = parent as Building_WorkTable;
            if (workTable?.BillStack == null)
            {
                return;
            }

            int removed = 0;
            bool dedicatedBillKept = false;
            for (int i = workTable.BillStack.Count - 1; i >= 0; i--)
            {
                Bill bill = workTable.BillStack[i];
                if (bill == null)
                {
                    continue;
                }

                bool isDedicated = PipedRefineryUtility.IsDedicatedPipedRecipe(bill.recipe);
                if (isDedicated && (!PipedRefineryUtility.PipedRecipeSupported || dedicatedBillKept))
                {
                    workTable.BillStack.Delete(bill);
                    removed++;
                    continue;
                }

                if (isDedicated)
                {
                    dedicatedBillKept = true;
                }
            }
            if (removed > 0)
            {
                Log.Message("[DBH Piped Waste] Removed " + removed + " duplicate or disabled piped refinery bill(s) from " + parent.ThingID + ".");
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref reservedSewage, "reservedSewage", 0f);
            Scribe_References.Look(ref reservationPawn, "reservationPawn");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                sewageBuffer = StoredSewage;
                reservedSewage = Mathf.Clamp(ReservedSewage, 0f, sewageBuffer);
                LongEventHandler.ExecuteWhenFinished(ValidateLoadedReservation);
            }
        }

        private void ValidateLoadedReservation()
        {
            if (!ReservationIsValid())
            {
                ReleaseReservation(null);
            }
        }

        private bool ReservationIsValid()
        {
            return reservationPawn != null && reservationPawn.CurJob != null &&
                reservationPawn.CurJob.RecipeDef == PipedRefineryUtility.PipedRecipe &&
                reservationPawn.CurJob.targetA.Thing == parent;
        }

        public bool TryReserveSewage(Pawn pawn, float amount)
        {
            amount = SewageDisposalUtility.SanitizeAmount(amount);
            if (pawn == null || amount <= 0f || AvailableForProduction < amount)
            {
                return false;
            }
            if (reservationPawn != null && reservationPawn != pawn)
            {
                return false;
            }
            reservationPawn = pawn;
            reservedSewage = amount;
            return true;
        }

        public bool TryConsumeReservation(Pawn pawn, out float consumed)
        {
            consumed = 0f;
            float reserved = SewageDisposalUtility.SanitizeAmount(reservedSewage);
            if (pawn == null || reservationPawn != pawn || reserved <= 0f || StoredSewage < reserved)
            {
                return false;
            }
            consumed = reserved;
            sewageBuffer -= consumed;
            reservedSewage = 0f;
            reservationPawn = null;
            return true;
        }

        public void RestoreConsumed(float amount)
        {
            // Rollback must restore the exact amount consumed, even when the
            // buffer was already above Capacity before the transaction began.
            // Overflow correction is handled separately by the normal
            // 60-tick relief path and must not delete sewage during rollback.
            sewageBuffer = StoredSewage + SewageDisposalUtility.SanitizeAmount(amount);
            RefreshBlockedState();
        }

        public void ReleaseReservation(Pawn pawn)
        {
            if (pawn == null || reservationPawn == pawn)
            {
                reservedSewage = 0f;
                reservationPawn = null;
            }
        }

        public override string CompInspectStringExtra()
        {
            string text = base.CompInspectStringExtra();
            if (ReservedSewage > 0f)
            {
                text += "\n" + "DBHPW_ReservedInspect".Translate(ReservedSewage.ToString("0.0"));
            }
            if (!PipedRefineryUtility.PipedRecipeSupported)
            {
                text += "\n" + "DBHPW_RefineryFallbackInspect".Translate();
            }
            return text;
        }
    }
}
