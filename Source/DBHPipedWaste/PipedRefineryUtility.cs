using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string FecalSludgeDefName = "FecalSludge";
        private const string VanillaBiofuelRefineryDefName = "BiofuelRefinery";
        private const string BurnPitDefName = "BurnPit";
        private const string DBHFecalChemfuelRecipeDefName = "Make_ChemfuelFromFecalSludge";
        private const float RequiredSewage = SewageDisposalUtility.ExtractionBatchSize;
        private static readonly HashSet<RecipeDef> sewageSubstitutionRecipes = new HashSet<RecipeDef>();
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
                List<RecipeDef> refineryRecipes = BuildRefineryRecipeList();
                foreach (RecipeDef recipe in refineryRecipes)
                {
                    recipe.recipeUsers?.RemoveAll(user => user == pipedRefineryDef);
                }
                pipedRefineryDef.recipes = refineryRecipes;
                properties.capacity = Math.Max(properties.capacity, MaximumConfiguredSewageRequirement());
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
            sewageSubstitutionRecipes.Clear();

            ThingDef vanillaRefinery = DefDatabase<ThingDef>.GetNamedSilentFail(VanillaBiofuelRefineryDefName);
            List<RecipeDef> inheritedRecipes = new List<RecipeDef>();
            AddRecipesAvailableOn(inheritedRecipes, vanillaRefinery);
            foreach (RecipeDef recipe in inheritedRecipes)
            {
                AddRecipe(recipes, recipe);
            }
            Log.Message("[DBH Piped Waste] Automatically inherited " + inheritedRecipes.Count +
                " BiofuelRefinery recipe(s): " + FormatRecipeNames(inheritedRecipes) + ".");

            ThingDef fecalSludge = DefDatabase<ThingDef>.GetNamedSilentFail(FecalSludgeDefName);
            ThingDef burnPit = DefDatabase<ThingDef>.GetNamedSilentFail(BurnPitDefName);
            if (fecalSludge != null)
            {
                List<RecipeDef> convertible = DefDatabase<RecipeDef>.AllDefsListForReading
                    .Where(recipe => recipe.defName != DBHFecalChemfuelRecipeDefName)
                    .Where(recipe => IsRecipeAvailableOn(recipe, vanillaRefinery) || IsRecipeAvailableOn(recipe, burnPit))
                    .Where(recipe => CanSafelySubstituteSewage(recipe, fecalSludge))
                    .OrderBy(recipe => recipe.defName)
                    .ToList();

                foreach (RecipeDef recipe in convertible)
                {
                    AddRecipe(recipes, recipe);
                    sewageSubstitutionRecipes.Add(recipe);
                }

                Log.Message("[DBH Piped Waste] Automatic sewage recipe discovery mounted " + convertible.Count +
                    " compatible recipe(s): " + FormatRecipeNames(convertible) + ".");
            }

            AddRecipe(recipes, PipedRecipe);
            return recipes;
        }

        private static void AddRecipesAvailableOn(List<RecipeDef> destination, ThingDef workTable)
        {
            if (workTable == null)
            {
                return;
            }

            if (workTable.recipes != null)
            {
                foreach (RecipeDef recipe in workTable.recipes)
                {
                    AddRecipe(destination, recipe);
                }
            }

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe.recipeUsers != null && recipe.recipeUsers.Contains(workTable))
                {
                    AddRecipe(destination, recipe);
                }
            }
        }

        private static bool IsRecipeAvailableOn(RecipeDef recipe, ThingDef workTable)
        {
            return recipe != null && workTable != null &&
                ((workTable.recipes != null && workTable.recipes.Contains(recipe)) ||
                 (recipe.recipeUsers != null && recipe.recipeUsers.Contains(workTable)));
        }

        private static bool CanSafelySubstituteSewage(RecipeDef recipe, ThingDef fecalSludge)
        {
            if (recipe == null || recipe.workerClass != typeof(RecipeWorker) ||
                recipe.UsesUnfinishedThing || !recipe.specialProducts.NullOrEmpty() ||
                recipe.products.NullOrEmpty() || recipe.products.Any(product => product.thingDef?.MadeFromStuff == true) ||
                recipe.ingredients.NullOrEmpty() || recipe.fixedIngredientFilter == null ||
                recipe.ingredients.Any(ingredient => ingredient?.filter == null ||
                    !ingredient.filter.Allows(fecalSludge) ||
                    (!ingredient.IsFixedIngredient && !recipe.fixedIngredientFilter.Allows(fecalSludge))))
            {
                return false;
            }

            return TryCalculateSewageRequirement(recipe, null, fecalSludge, out _);
        }

        private static void AddRecipe(List<RecipeDef> destination, RecipeDef recipe)
        {
            if (recipe != null && !destination.Contains(recipe))
            {
                destination.Add(recipe);
            }
        }

        private static string FormatRecipeNames(IEnumerable<RecipeDef> recipes)
        {
            string[] names = recipes.Select(recipe => recipe.defName).ToArray();
            return names.Length > 0 ? string.Join(", ", names) : "<none>";
        }

        private static float MaximumConfiguredSewageRequirement()
        {
            float maximum = RequiredSewage;
            ThingDef fecalSludge = DefDatabase<ThingDef>.GetNamedSilentFail(FecalSludgeDefName);
            foreach (RecipeDef recipe in sewageSubstitutionRecipes)
            {
                if (TryCalculateSewageRequirement(recipe, null, fecalSludge, out float amount))
                {
                    maximum = Math.Max(maximum, amount);
                }
            }
            return maximum;
        }

        private static bool TryCalculateSewageRequirement(
            RecipeDef recipe,
            Bill bill,
            ThingDef fecalSludge,
            out float requiredSewage)
        {
            requiredSewage = 0f;
            if (recipe?.ingredients == null || fecalSludge == null)
            {
                return false;
            }

            float valuePerUnit = recipe.IngredientValueGetter.ValuePerUnitOf(fecalSludge);
            if (float.IsNaN(valuePerUnit) || float.IsInfinity(valuePerUnit) || valuePerUnit <= 0f)
            {
                return false;
            }

            foreach (IngredientCount ingredient in recipe.ingredients)
            {
                if (ingredient == null)
                {
                    return false;
                }

                int amount = ingredient.CountRequiredOfFor(fecalSludge, recipe, bill);
                if (amount <= 0)
                {
                    return false;
                }
                requiredSewage += amount;
            }

            requiredSewage = SewageDisposalUtility.SanitizeAmount(requiredSewage);
            return requiredSewage > SewageNetworkUtility.AutomaticSupplyEpsilon;
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

        public static bool IsSewageBackedRecipe(RecipeDef recipe)
        {
            return IsDedicatedPipedRecipe(recipe) || sewageSubstitutionRecipes.Contains(recipe);
        }

        public static bool IsSewageBackedBill(Bill bill, Thing billGiver, out float requiredSewage)
        {
            requiredSewage = 0f;
            if (!PipedRecipeSupported || bill == null || billGiver == null ||
                billGiver.TryGetComp<CompPipedRefinerySewage>() == null ||
                !IsSewageBackedRecipe(bill.recipe))
            {
                return false;
            }

            if (IsDedicatedPipedRecipe(bill.recipe))
            {
                requiredSewage = RequiredSewage;
                return true;
            }

            ThingDef fecalSludge = DefDatabase<ThingDef>.GetNamedSilentFail(FecalSludgeDefName);
            return TryCalculateSewageRequirement(bill.recipe, bill, fecalSludge, out requiredSewage);
        }

        public static bool IsSewageBackedJob(Job job, out float requiredSewage)
        {
            requiredSewage = 0f;
            return job != null && job.bill != null && job.RecipeDef == job.bill.recipe &&
                IsSewageBackedBill(job.bill, job.targetA.Thing, out requiredSewage);
        }

        public static CompPipedRefinerySewage HandlerFor(Job job)
        {
            return job?.targetA.Thing?.TryGetComp<CompPipedRefinerySewage>();
        }
    }
}
