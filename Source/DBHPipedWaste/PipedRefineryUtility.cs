using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DBHPipedWaste
{
    public enum PipedRefineryValidationFailure
    {
        None,
        RecipeMissing,
        RefineryDefMissing,
        HarmonyTargetMissing,
        InvalidIngredients,
        InvalidProduct,
        RequiredCompMissing,
        ValidationException
    }

    public sealed class PipedRefineryValidationResult
    {
        public bool Supported { get; private set; }
        public PipedRefineryValidationFailure Failure { get; private set; }
        public string Reason { get; private set; }

        private PipedRefineryValidationResult(bool supported, PipedRefineryValidationFailure failure, string reason)
        {
            Supported = supported;
            Failure = failure;
            Reason = reason;
        }

        public static PipedRefineryValidationResult Success()
        {
            return new PipedRefineryValidationResult(true, PipedRefineryValidationFailure.None, null);
        }

        public static PipedRefineryValidationResult FailureResult(PipedRefineryValidationFailure failure, string reason)
        {
            return new PipedRefineryValidationResult(false, failure, reason);
        }
    }

    public static class PipedRefineryUtility
    {
        private const string PipedRecipeDefName = "DBHPW_MakeChemfuelFromPipedSewage";
        private static readonly string[] StandardRefineryRecipeDefNames =
        {
            "Make_ChemfuelFromWood",
            "Make_ChemfuelFromOrganics",
            "Make_ChemfuelFromFecalSludge"
        };
        private const float RequiredSewage = SewageDisposalUtility.ExtractionBatchSize;
        private static bool configured;
        private static bool structureSupported;
        private static bool harmonyTargetsSupported = true;
        private static string disabledReason;
        private static ThingDef pipedRefineryDef;

        public static RecipeDef PipedRecipe { get; private set; }
        public static bool PipedRecipeSupported => configured && structureSupported && harmonyTargetsSupported;
        public static string PipedRefineryDisabledReason => disabledReason;
        public static PipedRefineryValidationFailure ValidationFailure { get; private set; }
        public static float SewagePerBill => RequiredSewage;

        public static void MarkHarmonyTargetsUnsupported(Exception exception)
        {
            harmonyTargetsSupported = false;
            ValidationFailure = PipedRefineryValidationFailure.HarmonyTargetMissing;
            DisablePipedRefinery("Harmony target setup failed: " + exception);
        }

        public static void ConfigureDefs()
        {
            if (configured)
            {
                return;
            }
            configured = true;
            try
            {
                PipedRefineryValidationResult validation = ValidatePipedRefineryStructure();
                ValidationFailure = validation.Failure;
                if (!validation.Supported)
                {
                    FailStructure(validation.Reason);
                    return;
                }

                CompProperties_PipedSewageHandler properties = pipedRefineryDef.GetCompProperties<CompProperties_PipedSewageHandler>();
                properties.capacity = RequiredSewage * 3f;
                if (PipedRecipe.recipeUsers != null)
                {
                    PipedRecipe.recipeUsers.RemoveAll(user => user == pipedRefineryDef);
                }
                pipedRefineryDef.recipes = BuildRefineryRecipeList();
                structureSupported = true;
                disabledReason = null;
                Log.Message("[DBH Piped Waste] Dedicated piped refinery recipe enabled: " + RequiredSewage +
                    " sewage -> 35 chemfuel; capacity " + properties.capacity + "; recipe entries " +
                    pipedRefineryDef.recipes.Count + "; recipe users excluded to avoid duplicate registration.");
            }
            catch (Exception exception)
            {
                ValidationFailure = PipedRefineryValidationFailure.ValidationException;
                FailStructure("exception while validating recipe: " + exception);
            }
        }

        private static List<RecipeDef> BuildRefineryRecipeList()
        {
            List<RecipeDef> recipes = new List<RecipeDef>();
            foreach (string defName in StandardRefineryRecipeDefNames)
            {
                RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(defName);
                if (recipe != null && !recipes.Contains(recipe))
                {
                    recipes.Add(recipe);
                }
            }
            if (PipedRecipe != null && !recipes.Contains(PipedRecipe))
            {
                recipes.Add(PipedRecipe);
            }
            return recipes;
        }

        public static PipedRefineryValidationResult ValidatePipedRefineryStructure()
        {
            try
            {
                PipedRecipe = DefDatabase<RecipeDef>.GetNamedSilentFail(PipedRecipeDefName);
                pipedRefineryDef = DBHPWDefOf.DBHPW_PipedBiofuelRefinery;
                if (PipedRecipe == null)
                {
                    return PipedRefineryValidationResult.FailureResult(
                        PipedRefineryValidationFailure.RecipeMissing,
                        "the dedicated piped-sewage recipe is missing");
                }
                if (pipedRefineryDef == null)
                {
                    return PipedRefineryValidationResult.FailureResult(
                        PipedRefineryValidationFailure.RefineryDefMissing,
                        "the piped biofuel refinery Def is missing");
                }
                if (!harmonyTargetsSupported)
                {
                    return PipedRefineryValidationResult.FailureResult(
                        PipedRefineryValidationFailure.HarmonyTargetMissing,
                        "required Refinery Harmony targets are unavailable");
                }
                if (PipedRecipe.ingredients != null && PipedRecipe.ingredients.Count > 0)
                {
                    return PipedRefineryValidationResult.FailureResult(
                        PipedRefineryValidationFailure.InvalidIngredients,
                        "the dedicated piped-sewage recipe unexpectedly has entity ingredients");
                }
                if (PipedRecipe.products == null || PipedRecipe.products.Count != 1 ||
                    PipedRecipe.products[0].thingDef != ThingDefOf.Chemfuel || PipedRecipe.products[0].count != 35)
                {
                    return PipedRefineryValidationResult.FailureResult(
                        PipedRefineryValidationFailure.InvalidProduct,
                        "the dedicated piped-sewage recipe product is not exactly 35 chemfuel");
                }

                if (pipedRefineryDef.GetCompProperties<CompProperties_PipedSewageHandler>() == null)
                {
                    return PipedRefineryValidationResult.FailureResult(
                        PipedRefineryValidationFailure.RequiredCompMissing,
                        "piped refinery handler component is missing");
                }

                return PipedRefineryValidationResult.Success();
            }
            catch (Exception exception)
            {
                return PipedRefineryValidationResult.FailureResult(
                    PipedRefineryValidationFailure.ValidationException,
                    "exception while validating recipe: " + exception);
            }
        }

        private static void FailStructure(string reason)
        {
            DisablePipedRefinery(reason);
        }

        private static void DisablePipedRefinery(string reason)
        {
            structureSupported = false;
            disabledReason = reason;
            RemoveDedicatedRecipeFromDefs();
            Log.Error("[DBH Piped Waste] Piped refinery is disabled: " + reason + ".");
        }

        private static void RemoveDedicatedRecipeFromDefs()
        {
            if (PipedRecipe?.recipeUsers != null)
            {
                PipedRecipe.recipeUsers.RemoveAll(user => user == pipedRefineryDef);
            }
            if (pipedRefineryDef?.recipes != null)
            {
                pipedRefineryDef.recipes.RemoveAll(recipe => IsDedicatedPipedRecipe(recipe));
            }
        }

        public static bool IsDedicatedPipedRecipe(RecipeDef recipe)
        {
            return recipe != null && (recipe == PipedRecipe || recipe.defName == PipedRecipeDefName);
        }

        public static bool IsPipedBill(RecipeDef recipe, Thing billGiver)
        {
            return PipedRecipeSupported
                && IsDedicatedPipedRecipe(recipe)
                && billGiver != null
                && billGiver.TryGetComp<CompPipedRefinerySewage>() != null;
        }

        public static Bill FindDedicatedPipedBill(Thing billGiver)
        {
            Building_WorkTable workTable = billGiver as Building_WorkTable;
            if (workTable?.BillStack == null)
            {
                return null;
            }

            for (int i = 0; i < workTable.BillStack.Count; i++)
            {
                Bill bill = workTable.BillStack[i];
                if (bill != null && IsDedicatedPipedRecipe(bill.recipe))
                {
                    return bill;
                }
            }
            return null;
        }

        public static Bill FindPipedBill(Thing billGiver)
        {
            Building_WorkTable workTable = billGiver as Building_WorkTable;
            if (!PipedRecipeSupported || workTable?.BillStack == null)
            {
                return null;
            }

            for (int i = 0; i < workTable.BillStack.Count; i++)
            {
                Bill bill = workTable.BillStack[i];
                if (bill != null && IsPipedBill(bill.recipe, billGiver))
                {
                    return bill;
                }
            }
            return null;
        }

        public static CompPipedRefinerySewage HandlerFor(Job job)
        {
            return job?.targetA.Thing?.TryGetComp<CompPipedRefinerySewage>();
        }
    }
}
