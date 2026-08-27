using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DBHPipedWaste
{
    // Legacy save compatibility only. New refinery jobs are generated through
    // vanilla WorkGiver_DoBill and the dedicated Bill/RecipeDef.
    public class JobDriver_RefinePipedSewage : JobDriver
    {
        private Building_WorkTable Refinery => job.targetA.Thing as Building_WorkTable;
        private Bill Bill => job.bill;
        private CompPipedRefinerySewage Handler => job.targetA.Thing?.TryGetComp<CompPipedRefinerySewage>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!PipedRefineryUtility.PipedRecipeSupported ||
                !PipedRefineryUtility.IsDedicatedPipedRecipe(Bill?.recipe))
            {
                return false;
            }

            if (!pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            if (Handler == null || !Handler.TryReserveSewage(pawn, PipedRefineryUtility.SewagePerBill))
            {
                pawn.Map.reservationManager.Release(job.targetA, pawn, job);
                return false;
            }
            Bill?.Notify_DoBillStarted(pawn);
            Bill?.Notify_BillWorkStarted(pawn);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOn(() => Bill == null || Handler == null || Handler.ReservationPawn != pawn ||
                Handler.ReservedSewage < PipedRefineryUtility.SewagePerBill ||
                Handler.StoredSewage < Handler.ReservedSewage);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_Recipe.DoRecipeWork();
            yield return new Toil
            {
                initAction = FinishPipedBill,
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        private void FinishPipedBill()
        {
            if (!PipedRefineryUtility.PipedRecipeSupported ||
                !PipedRefineryUtility.IsDedicatedPipedRecipe(Bill?.recipe))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            if (Handler == null || Refinery == null || !Handler.TryConsumeReservation(pawn, out float consumed))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            Thing product = ThingMaker.MakeThing(ThingDefOf.Chemfuel);
            product.stackCount = 35;
            if (!GenPlace.TryPlaceThing(product, Refinery.InteractionCell, Map, ThingPlaceMode.Near))
            {
                Handler.RestoreConsumed(consumed);
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            Bill.Notify_IterationCompleted(pawn, new List<Thing> { product });
            Bill.Notify_BillWorkFinished(pawn);
        }
    }

    public class WorkGiver_UnloadPipedComposter : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DBHPWDefOf.DBHPW_PipedComposter);
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Building_PipedComposter composter = thing as Building_PipedComposter;
            return composter != null
                && composter.HasReadyLine
                && !thing.IsBurning()
                && !thing.IsForbidden(pawn)
                && pawn.CanReserveAndReach(thing, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            return JobMaker.MakeJob(DBHPWDefOf.DBHPW_UnloadPipedComposter, thing);
        }
    }

    public class JobDriver_UnloadPipedComposter : JobDriver
    {
        private Building_PipedComposter Composter => job.targetA.Thing as Building_PipedComposter;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(200)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .FailOn(() => Composter == null || !Composter.HasReadyLine)
                .WithProgressBarToilDelay(TargetIndex.A);
            yield return new Toil
            {
                initAction = delegate
                {
                    Composter?.UnloadReadyToMap(Map, pawn.Position);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }

    public class WorkGiver_ExtractSewage : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DBHPWDefOf.DBHPW_UndergroundSewagePit);
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompUndergroundSewageStorage storage = thing.TryGetComp<CompUndergroundSewageStorage>();
            return storage != null
                && storage.WantsExtraction
                && !pawn.Downed
                && !pawn.Drafted
                && !thing.IsBurning()
                && !thing.IsForbidden(pawn)
                && thing.Map.designationManager.DesignationOn(thing, DesignationDefOf.Deconstruct) == null
                && pawn.CanReserveAndReach(thing, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Job job = JobMaker.MakeJob(DBHPWDefOf.DBHPW_ExtractSewage, thing);
            CompUndergroundSewageStorage storage = thing.TryGetComp<CompUndergroundSewageStorage>();
            job.count = storage != null && storage.ManualExtractionRequested ? 1 : 0;
            return job;
        }
    }

    public class JobDriver_ExtractSewage : JobDriver
    {
        private ThingWithComps StorageBuilding => job.targetA.Thing as ThingWithComps;
        private CompUndergroundSewageStorage Storage => StorageBuilding?.GetComp<CompUndergroundSewageStorage>();
        private bool EmergencyEmptying => job.count == 1;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil extractionCycle = Toils_General.Label();
            yield return extractionCycle;
            Toil extract = Toils_General.Wait(120)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .FailOn(() => Storage == null || (EmergencyEmptying ? !Storage.ManualExtractionRequested || !Storage.HasWholeSewageUnit : Storage.sewageBuffer < SewageDisposalUtility.ExtractionBatchSize))
                .WithProgressBarToilDelay(TargetIndex.A);
            yield return extract;
            yield return new Toil
            {
                initAction = delegate
                {
                    bool extracted = EmergencyEmptying
                        ? Storage != null && Storage.TryEmergencyExtractBatch(Map, pawn.Position)
                        : Storage != null && Storage.TryExtractBatch(Map, pawn.Position);
                    if (!extracted)
                    {
                        EndJobWith(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return Toils_Jump.JumpIf(extractionCycle, () => EmergencyEmptying && Storage != null && Storage.ManualExtractionRequested && Storage.HasWholeSewageUnit);
        }
    }
}
