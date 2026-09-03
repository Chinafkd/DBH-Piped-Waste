using System;
using System.Reflection;
using System.Text;
using DubsBadHygiene;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DBHPipedWaste
{
    public sealed class DBHPipedWasteSettings : ModSettings
    {
        public bool sewageIntakePumpsRequirePower = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sewageIntakePumpsRequirePower, "sewageIntakePumpsRequirePower", true);
        }
    }

    public sealed class DBHPipedWasteMod : Mod
    {
        private static DBHPipedWasteSettings settings;

        public DBHPipedWasteMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<DBHPipedWasteSettings>();
            Harmony harmony = new Harmony("Chinafkd.DBHPipedWaste");
            PatchRefineryFeatures(harmony);
            PatchFixtureFeature(harmony);
            PatchAlertFeatures(harmony);
            LongEventHandler.ExecuteWhenFinished(PipedRefineryUtility.ConfigureDefs);
        }

        private static void PatchRefineryFeatures(Harmony harmony)
        {
            RefineryPatchSpec[] patches =
            {
                new RefineryPatchSpec(
                    typeof(Patch_WorkGiver_DoBill_TryFindBestBillIngredients),
                    typeof(WorkGiver_DoBill),
                    "TryFindBestBillIngredients",
                    "bool TryFindBestBillIngredients(Bill, Pawn, Thing, List<ThingCount>, List<IngredientCount>)"),
                new RefineryPatchSpec(
                    typeof(Patch_JobDriver_DoBill_TryMakePreToilReservations),
                    typeof(JobDriver_DoBill),
                    "TryMakePreToilReservations",
                    "bool TryMakePreToilReservations(bool)"),
                new RefineryPatchSpec(
                    typeof(Patch_Toils_Recipe_FinishRecipeAndStartStoringProduct),
                    typeof(Toils_Recipe),
                    "FinishRecipeAndStartStoringProduct",
                    "Toil FinishRecipeAndStartStoringProduct(TargetIndex)"),
                new RefineryPatchSpec(
                    typeof(Patch_JobDriver_Cleanup),
                    typeof(JobDriver),
                    "Cleanup",
                    "void Cleanup(JobCondition)")
            };

            LogRefineryEnvironment();
            RefineryPatchSpec currentPatch = null;
            try
            {
                foreach (RefineryPatchSpec patch in patches)
                {
                    currentPatch = patch;
                    LogRefineryPatchCandidates(patch);
                    harmony.CreateClassProcessor(patch.PatchType).Patch();
                    Log.Message("[DBH Piped Waste] Refinery Harmony patch applied: " + patch.Description);
                }
            }
            catch (Exception exception)
            {
                RefineryPatchSpec failedPatch = currentPatch ?? patches[0];
                Log.Error(BuildRefineryPatchFailureReport(failedPatch, exception));
                PipedRefineryUtility.MarkHarmonyTargetsUnsupported(exception);
            }
        }

        private static void LogRefineryEnvironment()
        {
            string rimWorldVersion = VersionControl.CurrentVersionString;
            string dbhVersion = GetAssemblyVersion(typeof(CompSewageHandler));
            string harmonyVersion = GetAssemblyVersion(typeof(Harmony));
            Log.Message("[DBH Piped Waste] Refinery Harmony startup diagnostics: RimWorld=" + rimWorldVersion +
                ", DBH=" + dbhVersion + ", Harmony=" + harmonyVersion + ".");
        }

        private static string GetAssemblyVersion(Type type)
        {
            try
            {
                return type?.Assembly?.GetName()?.Version?.ToString() ?? "unknown";
            }
            catch (Exception exception)
            {
                return "unknown (" + exception.GetType().Name + ")";
            }
        }

        private static void LogRefineryPatchCandidates(RefineryPatchSpec patch)
        {
            MethodInfo[] candidates = patch.TargetType.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].Name != patch.MethodName)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }
                builder.Append(FormatMethod(candidates[i]));
            }

            if (builder.Length == 0)
            {
                builder.Append("<none>");
            }
            Log.Message("[DBH Piped Waste] Refinery Harmony target candidates: " + patch.Description +
                "; expected=" + patch.ExpectedSignature + "; found=" + builder + ".");
        }

        private static string BuildRefineryPatchFailureReport(RefineryPatchSpec patch, Exception exception)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[DBH Piped Waste] Refinery Harmony target setup failed.");
            builder.AppendLine("Target patch: " + patch.Description);
            builder.AppendLine("Expected signature: " + patch.ExpectedSignature);
            builder.AppendLine("RimWorld: " + VersionControl.CurrentVersionString);
            builder.AppendLine("DBH assembly: " + GetAssemblyVersion(typeof(CompSewageHandler)));
            builder.AppendLine("Harmony assembly: " + GetAssemblyVersion(typeof(Harmony)));
            builder.AppendLine("Failure: " + exception);
            builder.Append("Result: piped refinery will be disabled fail-closed.");
            return builder.ToString();
        }

        private static string FormatMethod(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            StringBuilder builder = new StringBuilder();
            builder.Append(method.ReturnType.Name).Append(" ").Append(method.Name).Append("(");
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(parameters[i].ParameterType.Name);
            }
            builder.Append(")");
            return builder.ToString();
        }

        private sealed class RefineryPatchSpec
        {
            public readonly Type PatchType;
            public readonly Type TargetType;
            public readonly string MethodName;
            public readonly string ExpectedSignature;

            public string Description => TargetType.FullName + "." + MethodName;

            public RefineryPatchSpec(Type patchType, Type targetType, string methodName, string expectedSignature)
            {
                PatchType = patchType;
                TargetType = targetType;
                MethodName = methodName;
                ExpectedSignature = expectedSignature;
            }
        }

        private static void PatchFixtureFeature(Harmony harmony)
        {
            Type[] patches =
            {
                typeof(Patch_Building_AssignableFixture_Working),
                typeof(Patch_Building_Latrine_Working)
            };
            try
            {
                foreach (Type patch in patches)
                {
                    harmony.CreateClassProcessor(patch).Patch();
                }
            }
            catch (Exception exception)
            {
                Log.Error("[DBH Piped Waste] Fixture pit-recognition patch disabled; refinery patches remain available. " + exception);
            }
        }

        private static void PatchAlertFeatures(Harmony harmony)
        {
            try
            {
                harmony.CreateClassProcessor(typeof(Patch_Alert_BlockedSewer_GetReport)).Patch();
            }
            catch (Exception exception)
            {
                Log.Error("[DBH Piped Waste] Blocked-sewer alert patch disabled; other features remain available. " + exception);
            }
        }

        public static bool PumpsRequirePower => settings == null || settings.sewageIntakePumpsRequirePower;

        private static void NotifyPowerSettingChanged()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            foreach (Map map in Find.Maps)
            {
                if (map?.listerThings?.AllThings == null)
                {
                    continue;
                }

                foreach (Thing thing in map.listerThings.AllThings)
                {
                    thing.TryGetComp<CompPipedSewageHandler>()?.RefreshPowerSetting();
                }
            }
        }

        public override string SettingsCategory()
        {
            return "DBHPW_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            bool previousValue = settings == null || settings.sewageIntakePumpsRequirePower;
            listing.CheckboxLabeled(
                "DBHPW_SettingRequirePower".Translate(),
                ref settings.sewageIntakePumpsRequirePower,
                "DBHPW_SettingRequirePowerDesc".Translate());
            if (previousValue != settings.sewageIntakePumpsRequirePower)
            {
                WriteSettings();
                NotifyPowerSettingChanged();
            }
            listing.End();
        }
    }
}
