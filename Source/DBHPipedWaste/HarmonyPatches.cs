using System;
using System.Collections.Generic;
using DubsBadHygiene;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DBHPipedWaste
{
    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
    public static class Patch_WorkGiver_DoBill_TryFindBestBillIngredients
    {
        public static bool Prefix(
            Bill bill,
            Pawn pawn,
            Thing billGiver,
            List<ThingCount> chosen,
            List<IngredientCount> missingIngredients,
            ref bool __result)
        {
            if (bill == null || !PipedRefineryUtility.IsSewageBackedRecipe(bill.recipe) ||
                billGiver?.TryGetComp<CompPipedRefinerySewage>() == null)
            {
                return true;
            }

            chosen?.Clear();
            missingIngredients?.Clear();
            if (!PipedRefineryUtility.IsSewageBackedBill(bill, billGiver, out float requiredSewage))
            {
                __result = false;
                JobFailReason.Is("DBHPW_RefineryDisabled".Translate());
                return false;
            }

            CompPipedRefinerySewage handler = billGiver.TryGetComp<CompPipedRefinerySewage>();
            if (handler.AvailableForProduction >= requiredSewage)
            {
                __result = true;
            }
            else
            {
                __result = false;
                JobFailReason.Is("DBHPW_InsufficientSewage".Translate());
            }
            return false;
        }
    }

    /// <summary>
    /// Developer-only diagnostic hook. It deliberately does not alter the
    /// result of the DBH fixture check or any network state.
    /// </summary>
    public static class FixtureSewageDebugPatchUtility
    {
        public static void DumpIfNosewage(CompPipe pipe, AcceptanceReport result)
        {
            if (result.Accepted || result.Reason != "Nosewage".Translate().ToString())
            {
                return;
            }

            PitDebugUtility.DumpFixtureState(pipe?.parent, result, pipe?.pipeNet);
        }
    }

    [HarmonyPatch(typeof(Building_AssignableFixture), nameof(Building_AssignableFixture.Working))]
    public static class Patch_Building_AssignableFixture_Working
    {
        public static void Postfix(Building_AssignableFixture __instance, ref AcceptanceReport __result)
        {
            FixtureSewageDebugPatchUtility.DumpIfNosewage(__instance.pipe, __result);
        }
    }

    [HarmonyPatch(typeof(Building_Latrine), nameof(Building_Latrine.Working))]
    public static class Patch_Building_Latrine_Working
    {
        public static void Postfix(Building_Latrine __instance, ref AcceptanceReport __result)
        {
            FixtureSewageDebugPatchUtility.DumpIfNosewage(__instance.pipe, __result);
        }
    }

    [HarmonyPatch(typeof(Alert_BlockedSewer), nameof(Alert_BlockedSewer.GetReport))]
    public static class Patch_Alert_BlockedSewer_GetReport
    {
        public static void Postfix(ref AlertReport __result)
        {
            if (__result.AnyCulpritValid)
            {
                // A valid non-DBHPW culprit belongs to DBH's own decision.
                // Never replace or reinterpret that original report.
                foreach (var target in __result.AllCulprits)
                {
                    Thing culprit = target.Thing;
                    if (culprit == null || culprit.TryGetComp<CompPipedComposterSewage>() == null)
                    {
                        return;
                    }
                }

                // The original report was caused exclusively by DBHPW
                // production buffers. Correct that false positive, but still
                // restore a real blocked-outlet report if one was hidden by
                // the DBHPW consumers.
                if (TryFindRealBlockedOutlet(out Thing blockedOutlet))
                {
                    __result = AlertReport.CulpritIs(blockedOutlet);
                }
                else
                {
                    __result = false;
                }
                return;
            }

            // DBH reported no alert. Only add the narrow compensation for a
            // real outlet failure that its all-sewers check can hide behind an
            // available Pit.
            if (TryFindRealBlockedOutlet(out Thing uncoveredOutlet))
            {
                __result = AlertReport.CulpritIs(uncoveredOutlet);
            }
        }

        private static bool TryFindRealBlockedOutlet(out Thing culprit)
        {
            culprit = null;
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome)
                {
                    continue;
                }

                HygienePipeMapComp hygiene = map.PipeNet();
                if (hygiene?.PipeNets == null)
                {
                    continue;
                }

                foreach (PlumbingNet network in hygiene.PipeNets)
                {
                    bool hasOutlet = false;
                    bool hasAvailableOutlet = false;
                    CompSewageOutlet blocked = null;
                    if (network?.Sewers == null)
                    {
                        continue;
                    }

                    foreach (CompSewageHandler sewer in network.Sewers)
                    {
                        CompSewageOutlet outlet = sewer as CompSewageOutlet;
                        if (outlet == null)
                        {
                            continue;
                        }

                        hasOutlet = true;
                        if (outlet.Blocked)
                        {
                            blocked = blocked ?? outlet;
                        }
                        else
                        {
                            hasAvailableOutlet = true;
                        }
                    }

                    if (hasOutlet && !hasAvailableOutlet && blocked != null)
                    {
                        culprit = blocked.parent;
                        return true;
                    }
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_DoBill), nameof(JobDriver_DoBill.TryMakePreToilReservations))]
    public static class Patch_JobDriver_DoBill_TryMakePreToilReservations
    {
        public static void Postfix(JobDriver_DoBill __instance, ref bool __result)
        {
            Job job = __instance?.job;
            if (job == null || !PipedRefineryUtility.IsSewageBackedRecipe(job.RecipeDef))
            {
                return;
            }

            if (!PipedRefineryUtility.IsSewageBackedJob(job, out float requiredSewage))
            {
                __result = false;
                return;
            }
            if (!__result)
            {
                return;
            }

            CompPipedRefinerySewage handler = PipedRefineryUtility.HandlerFor(job);
            if (handler == null || !handler.TryReserveSewage(__instance.pawn, requiredSewage))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.FinishRecipeAndStartStoringProduct))]
    public static class Patch_Toils_Recipe_FinishRecipeAndStartStoringProduct
    {
        public static void Postfix(ref Toil __result)
        {
            Toil toil = __result;
            Action original = toil.initAction;
            toil.initAction = delegate
            {
                Pawn pawn = toil.actor;
                Job job = pawn?.CurJob;
                if (job == null)
                {
                    original?.Invoke();
                    return;
                }

                if (!PipedRefineryUtility.IsSewageBackedRecipe(job.RecipeDef))
                {
                    original?.Invoke();
                    return;
                }

                if (!PipedRefineryUtility.IsSewageBackedJob(job, out float requiredSewage))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                CompPipedRefinerySewage handler = PipedRefineryUtility.HandlerFor(job);
                if (handler == null ||
                    Mathf.Abs(handler.ReservedSewage - requiredSewage) > SewageNetworkUtility.AutomaticSupplyEpsilon ||
                    !handler.TryConsumeReservation(pawn, out float consumed))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                try
                {
                    original?.Invoke();
                }
                catch
                {
                    handler.RestoreConsumed(consumed);
                    throw;
                }
            };
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_JobDriver_Cleanup
    {
        public static void Postfix(JobDriver __instance)
        {
            Job job = __instance?.job;
            CompPipedRefinerySewage handler = PipedRefineryUtility.HandlerFor(job);
            handler?.ReleaseReservation(__instance.pawn);
        }
    }
}
