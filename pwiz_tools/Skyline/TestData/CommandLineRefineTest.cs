/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class CommandLineRefineTest : AbstractUnitTestEx
    {
        private string DocumentPath { get; set; }
        private string OutPath { get; set; }

        private string Run(params string[] args)
        {
            var listArgs = new List<string>(args);
            listArgs.Insert(0, CommandArgs.ARG_IN.GetArgumentTextWithValue(DocumentPath));
            listArgs.Add(CommandArgs.ARG_OUT.GetArgumentTextWithValue(OutPath));
            listArgs.Add(CommandArgs.ARG_OVERWRITE.ArgumentText);
            return RunCommand(listArgs.ToArray());
        }

        [TestMethod]
        public void ConsoleRefineDocumentTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineRefine.zip");
            DocumentPath = InitRefineDocument("SRM_mini_single_replicate.sky");
            OutPath = Path.Combine(Path.GetDirectoryName(DocumentPath) ?? string.Empty, "test.sky");

            // First check a few refinements which should not change the document
            string minPeptides = 1.ToString();
            string output = Run(CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides));
            AssertEx.Contains(output, Resources.CommandLine_RefineDocument_Refining_document___, Resources.CommandLine_LogNewEntries_Document_unchanged);
            string minTrans = (2).ToString();
            output = Run(CommandArgs.ARG_REFINE_MIN_TRANSITIONS.GetArgumentTextWithValue(minTrans));
            AssertEx.Contains(output, Resources.CommandLine_RefineDocument_Refining_document___, Resources.CommandLine_LogNewEntries_Document_unchanged);

            // Remove the protein with only 3 peptides
            minPeptides = 4.ToString();
            output = Run(CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_MinPeptidesPerProtein, LogMessage.Quote(minPeptides));
            IsDocumentState(OutPath, 0, 3, 33, 0, 35, 301, output);

            // Remove the precursor with only 2 transitions
            minTrans = 3.ToString();
            output = Run(CommandArgs.ARG_REFINE_MIN_TRANSITIONS.GetArgumentTextWithValue(minTrans));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_MinTransitionsPepPrecursor, LogMessage.Quote(minTrans));
            IsDocumentState(OutPath, 1, 4, 35, 0, 37, 332, output);

            // Remove the heavy precursor
            output = Run(CommandArgs.ARG_REFINE_LABEL_TYPE.GetArgumentTextWithValue(IsotopeLabelType.heavy.ToString()));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_RefineLabelType, IsotopeLabelType.heavy.AuditLogText);
            IsDocumentState(OutPath, 1, 4, 37, 0, 39, 334, output);
            // Remove everything but the heavy precursor
            minPeptides = 1.ToString();
            output = Run(CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides),
                CommandArgs.ARG_REFINE_LABEL_TYPE.GetArgumentTextWithValue(IsotopeLabelType.light.ToString()));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_MinPeptidesPerProtein, LogMessage.Quote(minPeptides),
                PropertyNames.RefinementSettings_RefineLabelType, IsotopeLabelType.light.AuditLogText);
            IsDocumentState(OutPath, 0, 1, 1, 0, 1, 4, output);
            // Add back the light for the one remaining peptide
            output = RunCommand(CommandArgs.ARG_IN.GetArgumentTextWithValue(OutPath),
                CommandArgs.ARG_REFINE_LABEL_TYPE.GetArgumentTextWithValue(IsotopeLabelType.light.ToString()),
                CommandArgs.ARG_REFINE_ADD_LABEL_TYPE.ArgumentText,
                CommandArgs.ARG_SAVE.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_AddRefineLabelType,
                IsotopeLabelType.light.AuditLogText,
                CommandLine.AddedText(0, 0, 0, 0, 1, 4));
            IsDocumentState(OutPath, 0, 1, 1, 0, 2, 8);
            // Perform the operation again without protein removal
            output = Run(CommandArgs.ARG_REFINE_LABEL_TYPE.GetArgumentTextWithValue(IsotopeLabelType.light.ToString()));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_RefineLabelType, IsotopeLabelType.light.AuditLogText);
            IsDocumentState(OutPath, 1, 4, 1, 0, 1, 4, output);
            // Remove repeated peptides
            output = Run(CommandArgs.ARG_REFINE_REMOVE_REPEATS.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RemoveRepeatedPeptides);
            IsDocumentState(OutPath, 1, 4, 35, 0, 38, 332, output);
            // Remove duplicate peptides
            output = Run(CommandArgs.ARG_REFINE_REMOVE_DUPLICATES.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RemoveDuplicatePeptides);
            IsDocumentState(OutPath, 1, 4, 34, 0, 36, 324, output);
            // Remove missing library
            output = Run(CommandArgs.ARG_REFINE_MISSING_LIBRARY.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_RemoveMissingLibrary);
            IsDocumentState(OutPath, 1, 4, 18, 0, 18, 176);

            // Try settings that remove everything from the document
            minPeptides = 20.ToString();
            output = Run(CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_MinPeptidesPerProtein, LogMessage.Quote(minPeptides));
            IsDocumentState(OutPath, 0, 0, 0, 0, 0, 0, output);
            minPeptides = 1.ToString();
            minTrans = 20.ToString();
            output = Run(CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides),
                CommandArgs.ARG_REFINE_MIN_TRANSITIONS.GetArgumentTextWithValue(minTrans));
            AssertEx.Contains(output, PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_MinPeptidesPerProtein, LogMessage.Quote(minPeptides),
                PropertyNames.RefinementSettings_MinTransitionsPepPrecursor, LogMessage.Quote(minTrans));
            IsDocumentState(OutPath, 0, 0, 0, 0, 0, 0, output);

            // Refine to autoselection
            output = Run(CommandArgs.ARG_REFINE_AUTOSEL_TRANSITIONS.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_AutoPickTransitionsAll,
                CommandLine.RemovedText(0, 0, 0, 0, 0, 2),
                CommandLine.AddedText(0, 0, 0, 0, 0, 18));
            IsDocumentState(OutPath, 1, 4, 37, 0, 40, 354);
            output = Run(CommandArgs.ARG_REFINE_AUTOSEL_PRECURSORS.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_AutoPickPrecursorsAll);
            IsDocumentState(OutPath, 1, 4, 37, 0, 38, 331, output);
            output = Run(CommandArgs.ARG_REFINE_AUTOSEL_TRANSITIONS.ArgumentText,
                CommandArgs.ARG_REFINE_AUTOSEL_PRECURSORS.ArgumentText,
                CommandArgs.ARG_REFINE_AUTOSEL_PEPTIDES.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings, PropertyNames.RefinementSettings_AutoPickTransitionsAll,
                PropertyNames.RefinementSettings_AutoPickPrecursorsAll, PropertyNames.RefinementSettings_AutoPickPeptidesAll,
                CommandLine.RemovedText(0, 0, 0, 0, 2, 9),
                CommandLine.AddedText(0, 0, 24, 0, 24, 200));
            IsDocumentState(OutPath, 1, 4, 61, 0, 62, 529);
        }

        [TestMethod]
        public void ConsoleRefineResultsTest()
        {
            Settings.Default.RTCalculatorName = Settings.Default.RTScoreCalculatorList.GetDefaults().First().Name;

            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineRefine.zip");
            DocumentPath = InitRefineDocument("SRM_mini_single_replicate.sky");
            OutPath = Path.Combine(Path.GetDirectoryName(DocumentPath) ?? string.Empty, "test.sky");

            // First check a few refinements which should not change the document
            string output = Run(CommandArgs.ARG_REFINE_MIN_TIME_CORRELATION.GetArgumentTextWithValue(0.1.ToString(CultureInfo.CurrentCulture)));
            AssertEx.Contains(output, Resources.CommandLine_RefineDocument_Refining_document___, Resources.CommandLine_LogNewEntries_Document_unchanged);
            output = Run(CommandArgs.ARG_REFINE_MIN_DOTP.GetArgumentTextWithValue(0.1.ToString(CultureInfo.CurrentCulture)));
            AssertEx.Contains(output, Resources.CommandLine_RefineDocument_Refining_document___, Resources.CommandLine_LogNewEntries_Document_unchanged);
            output = Run(CommandArgs.ARG_REFINE_MIN_PEAK_FOUND_RATIO.GetArgumentTextWithValue(0.0.ToString(CultureInfo.CurrentCulture)),
                CommandArgs.ARG_REFINE_MAX_PEAK_FOUND_RATIO.GetArgumentTextWithValue(1.0.ToString(CultureInfo.CurrentCulture)));
            AssertEx.Contains(output, Resources.CommandLine_RefineDocument_Refining_document___, Resources.CommandLine_LogNewEntries_Document_unchanged);

            // Remove nodes without results
            string minPeptides = 1.ToString();
            var args = new List<string>
            {
                CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(minPeptides),
                CommandArgs.ARG_REFINE_MISSING_RESULTS.ArgumentText
            };
            output = Run(args.ToArray());
            var parts = new List<string>
            {
                PropertyNames.RefinementSettings,
                PropertyNames.RefinementSettings_MinPeptidesPerProtein, LogMessage.Quote(minPeptides),
                PropertyNames.RefinementSettings_RemoveMissingResults
            };
            AssertEx.Contains(output, parts.ToArray());
            IsResultsState(OutPath, 18, 18, 176, output);

            // Filter for dot product, ignoring nodes without results
            string minDotp = Statistics.AngleToNormalizedContrastAngle(0.9).ToString(CultureInfo.CurrentCulture);
            args[1] = CommandArgs.ARG_REFINE_MIN_DOTP.GetArgumentTextWithValue(minDotp);
            output = Run(args.ToArray());
            parts[3] = PropertyNames.RefinementSettings_DotProductThreshold;
            parts.Add(LogMessage.Quote(minDotp));
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 1, 4, 32, 0, 35, 281, output);

            // Further refine with retention time refinement
            string minTimeCorrelation = 0.95.ToString(CultureInfo.CurrentCulture);
            args.Add(CommandArgs.ARG_REFINE_MIN_TIME_CORRELATION.GetArgumentTextWithValue(minTimeCorrelation));
            output = Run(args.ToArray());
            parts.Add(PropertyNames.RefinementSettings_RTRegressionThreshold);
            parts.Add(LogMessage.Quote(minTimeCorrelation));
            AssertEx.Contains(output, parts.ToArray());
            IsResultsState(OutPath, 12, 12, 111, output);
            // And peak count ratio
            string minPeakFoundRatio = 1.0.ToString(CultureInfo.CurrentCulture);
            args.Add(CommandArgs.ARG_REFINE_MIN_PEAK_FOUND_RATIO.GetArgumentTextWithValue(minPeakFoundRatio));
            output = Run(args.ToArray());
            parts.Add(PropertyNames.RefinementSettings_MinPeakFoundRatio);
            parts.Add(LogMessage.Quote(minPeakFoundRatio));
            AssertEx.Contains(output, parts.ToArray());
            IsResultsState(OutPath, 7, 7, 54, output);
            // Pick only most intense transitions
            string maxPeakRank = 4.ToString();
            args.Add(CommandArgs.ARG_REFINE_MAX_PEAK_RANK.GetArgumentTextWithValue(maxPeakRank));
            output = Run(args.ToArray());
            parts.Add(PropertyNames.RefinementSettings_MaxPeakRank);
            parts.Add(LogMessage.Quote(maxPeakRank));
            AssertEx.Contains(output, parts.ToArray());
            IsResultsState(OutPath, 7, 7, 28, output);
            // Pick only most intense peptides
            string maxPeptideRank = 5.ToString();
            args.Add(CommandArgs.ARG_REFINE_MAX_PEPTIDE_PEAK_RANK.GetArgumentTextWithValue(maxPeptideRank));
            output = Run(args.ToArray());
            parts.Add(PropertyNames.RefinementSettings_MaxPepPeakRank);
            parts.Add(LogMessage.Quote(maxPeptideRank));
            AssertEx.Contains(output, parts.ToArray());
            IsResultsState(OutPath, 5, 5, 20, output);
            // Pick the precursors with the maximum peaks
            DocumentPath = InitRefineDocument("iPRG 2015 Study-mini.sky", 1, 0, 4, 6, 18);
            output = Run(CommandArgs.ARG_REFINE_MAX_PRECURSOR_PEAK_ONLY.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings_MaxPrecursorPeakOnly);
            IsResultsState(OutPath, 4, 4, 12, output, true);
            // Pick the precursors with the maximum peaks, not ignoring standard types
            DocumentPath = InitRefineDocument("sprg_all_charges-mini.sky", 1, 0, 3, 6, 54);
            output = Run(CommandArgs.ARG_REFINE_MAX_PRECURSOR_PEAK_ONLY.ArgumentText);
            AssertEx.Contains(output, PropertyNames.RefinementSettings_MaxPrecursorPeakOnly);
            IsResultsState(OutPath, 3, 3, 27, output, true);
        }

        [TestMethod]
        public void ConsoleRefineConsistencyTest()
        {
            string cvCutoff = 20.ToString();
            string cvCutoffDecimalPercent = 0.2.ToString(CultureInfo.CurrentCulture);
            var args = new List<string>
            {
                CommandArgs.ARG_REFINE_CV_REMOVE_ABOVE_CUTOFF.GetArgumentTextWithValue(cvCutoff)
            };
            var parts = new List<string>
            {
                PropertyNames.RefinementSettings_CVCutoff
            };
            // Remove all elements above the cv cutoff
            var testFilesDirs = new[]
            {
                new TestFilesDir(TestContext, @"TestFunctional\AreaCVHistogramTest.zip"),
                new TestFilesDir(TestContext, @"TestData\CommandLineRefine.zip"),
            };
            TestFilesDir = testFilesDirs[0];
            DocumentPath = InitRefineDocument("Rat_plasma.sky", 19, 29, 125, 125, 721);
            OutPath = Path.Combine(Path.GetDirectoryName(DocumentPath) ?? string.Empty, "test.sky");
            var output = Run(args.ToArray());
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 19, 29, 3, 0, 3, 18, output);

            // Remove empty proteins
            args.Add(CommandArgs.ARG_REFINE_MIN_PEPTIDES.GetArgumentTextWithValue(1.ToString()));
            output = Run(args.ToArray());
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 2, 1, 3, 0, 3, 18, output);
            args.RemoveAt(args.Count - 1);

            // Try the same using a decimal percentage
            output = Run(CommandArgs.ARG_REFINE_CV_REMOVE_ABOVE_CUTOFF.GetArgumentTextWithValue(cvCutoffDecimalPercent));
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 19, 29, 3, 0, 3, 18, output);

            // Normalize to medians and remove all elements above the cv cutoff
            args.Add(CommandArgs.ARG_REFINE_CV_GLOBAL_NORMALIZE.GetArgumentTextWithValue(NormalizationMethod.EQUALIZE_MEDIANS.Name));
            output = Run(args.ToArray());
            parts.Add(PropertyNames.RefinementSettings_NormalizationMethod);
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 19, 29, 10, 0, 10, 58, output);

            // Test best transitions
            args[1] = CommandArgs.ARG_REFINE_CV_TRANSITIONS.GetArgumentTextWithValue("best");
            output = Run(args.ToArray());
            parts[1] = PropertyNames.RefinementSettings_Transitions;
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 19, 29, 3, 0, 3, 18, output);

            // Test count transitions
            args[1] = CommandArgs.ARG_REFINE_CV_TRANSITIONS_COUNT.GetArgumentTextWithValue(4.ToString());
            args.Add(CommandArgs.ARG_REFINE_CV_MS_LEVEL.GetArgumentTextWithValue("products"));
            output = Run(args.ToArray());
            parts[1] = PropertyNames.RefinementSettings_CountTransitions;
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 19, 29, 3, 0, 3, 18, output);

            // Make sure error is recorded when peptide have only 1 replicate
            TestFilesDir = testFilesDirs[1];
            DocumentPath = InitRefineDocument("SRM_mini_single_replicate.sky", 1, 4, 37, 40, 338);
            output = Run(CommandArgs.ARG_REFINE_CV_REMOVE_ABOVE_CUTOFF.GetArgumentTextWithValue(cvCutoff));
            AssertEx.Contains(output, Resources.RefinementSettings_Refine_The_document_must_contain_at_least_2_replicates_to_refine_based_on_consistency_);

            // So that both TestFilesDirs get cleaned up properly
            TestFilesDirs = testFilesDirs;
        }

        [TestMethod]
        public void ConsoleRefineGroupComparisonsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineRefineGroupComparisonTest.zip");
            DocumentPath = InitRefineDocument("Rat_plasma.sky", 48, 0, 125, 125, 721);
            OutPath = Path.Combine(Path.GetDirectoryName(DocumentPath) ?? string.Empty, "gctest.sky");

            // Verify pValueCutoff and fold change cutoff work
            var pValueCutoff = 0.05.ToString(CultureInfo.CurrentCulture);
            var foldChangeCutoff = 2.ToString(CultureInfo.CurrentCulture);
            var args = new List<string>
            {
                CommandArgs.ARG_REFINE_GC_P_VALUE_CUTOFF.GetArgumentTextWithValue(pValueCutoff),
                CommandArgs.ARG_REFINE_GC_FOLD_CHANGE_CUTOFF.GetArgumentTextWithValue(foldChangeCutoff),
                CommandArgs.ARG_REFINE_GROUP_NAME.GetArgumentTextWithValue("Test Group Comparison"),
            };

            var parts = new List<string>
            {
                PropertyNames.RefinementSettings_AdjustedPValueCutoff,
                PropertyNames.RefinementSettings_FoldChangeCutoff
            };
            var output = Run(args.ToArray());
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 48, 0, 43, 0, 43, 248, output);

            // Verify only fold change cutoff works
            args.RemoveAt(0);
            foldChangeCutoff = 3.ToString();
            args[0] = CommandArgs.ARG_REFINE_GC_FOLD_CHANGE_CUTOFF.GetArgumentTextWithValue(foldChangeCutoff);
            parts.RemoveAt(0);
            output = Run(args.ToArray());
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 48, 0, 20, 0, 20, 114, output);

            // Verify only p value cutoff works
            pValueCutoff = 0.08.ToString(CultureInfo.CurrentCulture);
            args[0] = CommandArgs.ARG_REFINE_GC_P_VALUE_CUTOFF.GetArgumentTextWithValue(pValueCutoff);
            parts[0] = PropertyNames.RefinementSettings_AdjustedPValueCutoff;
            output = Run(args.ToArray());
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 48, 0, 103, 0, 103, 597, output);

            // Verify the union of two group comparisons works
            pValueCutoff = 0.05.ToString(CultureInfo.CurrentCulture);
            foldChangeCutoff = 2.ToString();
            args.Clear();
            args.Add(CommandArgs.ARG_REFINE_GC_P_VALUE_CUTOFF.GetArgumentTextWithValue(pValueCutoff));
            args.Add(CommandArgs.ARG_REFINE_GC_FOLD_CHANGE_CUTOFF.GetArgumentTextWithValue(foldChangeCutoff));
            args.Add(CommandArgs.ARG_REFINE_GROUP_NAME.GetArgumentTextWithValue("Test Group Comparison"));
            args.Add(CommandArgs.ARG_REFINE_GROUP_NAME.GetArgumentTextWithValue("Test Group Comparison 2"));
            parts.Add(PropertyNames.RefinementSettings_FoldChangeCutoff);
            output = Run(args.ToArray());
            AssertEx.Contains(output, parts.ToArray());
            IsDocumentState(OutPath, 48, 0, 44, 0, 44, 255, output);
        }

        //        [TestMethod]
        //        public void ConsoleRefineConvertToSmallMoleculesTest()
        //        {
        //            // Exercise the code that helps match heavy labeled ion formulas with unlabled
        //            Assert.AreEqual("C5H12NO2S", BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula("C5H9H'3NO2S"));
        //            Assert.IsNull(BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(""));
        //            Assert.IsNull(BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(null));
        //
        //            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        //        }
        //
        //        [TestMethod]
        //        public void ConsoleRefineConvertToSmallMoleculeMassesTest()
        //        {
        //            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        //        }
        //
        //        [TestMethod]
        //        public void ConsoleRefineConvertToSmallMoleculeMassesAndNamesTest()
        //        {
        //            InitRefineDocument(RefinementSettings.ConvertToSmallMoleculesMode.masses_and_names);
        //        }

        [TestMethod]
        public void ConsoleChangePredictTranSettingsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineRefine.zip");
            DocumentPath = InitRefineDocument("SRM_mini_single_replicate.sky");
            OutPath = Path.Combine(Path.GetDirectoryName(DocumentPath) ?? string.Empty, "test.sky");

            Run(CommandArgs.ARG_REMOVE_ALL.ArgumentText);   // Remove results

            DocumentPath = OutPath;
            OutPath = Path.Combine(Path.GetDirectoryName(DocumentPath) ?? string.Empty, "test2.sky");

            // Valid changes
            string output = Run(CommandArgs.ARG_TRAN_PREDICT_CE.GetArgumentTextWithValue("SCIEX"));
            AssertEx.Contains(output, "test2", PropertyNames.TransitionPrediction_NonNullCollisionEnergy, "Thermo", "SCIEX");
            IsDocumentUnchanged(output);
            string ceNoneText = Settings.Default.CollisionEnergyList.GetDisplayName(CollisionEnergyList.NONE);
            output = Run(CommandArgs.ARG_TRAN_PREDICT_CE.GetArgumentTextWithValue(ceNoneText));
            AssertEx.Contains(output, "test2", PropertyNames.TransitionPrediction_NonNullCollisionEnergy, "Thermo", AuditLogStrings.None);
            IsDocumentUnchanged(output);
            string dpNoneText = Settings.Default.DeclusterPotentialList.GetDisplayName(DeclusterPotentialList.NONE);
            output = Run(CommandArgs.ARG_TRAN_PREDICT_DP.GetArgumentTextWithValue("SCIEX"));
            AssertEx.Contains(output, "test2", PropertyNames.TransitionPrediction_NonNullDeclusteringPotential, AuditLogStrings.None, "SCIEX");
            IsDocumentUnchanged(output);
            string covNoneText = Settings.Default.CompensationVoltageList.GetDisplayName(CompensationVoltageList.NONE);
            output = Run(CommandArgs.ARG_TRAN_PREDICT_COV.GetArgumentTextWithValue("SCIEX"));
            AssertEx.Contains(output, "test2", PropertyNames.TransitionPrediction_NonNullCompensationVoltage, AuditLogStrings.None, "SCIEX");
            IsDocumentUnchanged(output);
            // Only None is possible for optimization libraries without setting one up
            string optLibNoneText = Settings.Default.OptimizationLibraryList.GetDisplayName(OptimizationLibrary.NONE);
            output = Run(CommandArgs.ARG_TRAN_PREDICT_OPTDB.GetArgumentTextWithValue(optLibNoneText));
            AssertEx.Contains(output, "test2", Resources.CommandLine_LogNewEntries_Document_unchanged);
            IsDocumentUnchanged(output);
        }


        [TestMethod]
        public void ConsoleArgumentInvalidValuesTest()
        {
            var testedArguments = new HashSet<CommandArgs.Argument>
            {
                CommandArgs.ARG_PANORAMA_FOLDER,
                CommandArgs.ARG_TOOL_INITIAL_DIR,
                CommandArgs.ARG_TOOL_PROGRAM_PATH
            };
            var allArgumentsSet = new HashSet<CommandArgs.Argument>(
                CommandArgs.AllArguments.Where(a => !a.InternalUse && !testedArguments.Contains(a)));

            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineRefine.zip");
            DocumentPath = InitRefineDocument("SRM_mini_single_replicate.sky");
            // Automatically test arguments that have Values
            foreach (var arg in allArgumentsSet)
            {
                if (arg.Values != null)
                    ValidateInvalidValue(arg, testedArguments);
                else if (CommandArgs.PATH_TYPE_VALUES.Contains(arg.ValueExample))
                    ValidateInvalidValuePath(arg, testedArguments);
                else if (arg.ValueExample != null && !CommandArgs.STRING_TYPE_VALUES.Contains(arg.ValueExample))
                    ValidateInvalidValueNumeric(arg, testedArguments);
                else if (arg.ValueExample == null)
                    ValidateInvalidValueBool(arg, testedArguments);
                else
                    ValidateInvalidValueString(arg, testedArguments);
            }

            // Verify that all arguments have been tested (except InternalUse ones and the ones in the testedArguments initializer)
            allArgumentsSet.ExceptWith(testedArguments);
            Assert.AreEqual(0, allArgumentsSet.Count, string.Join(", ", allArgumentsSet.Select(a => a.Name)));
        }

        private void ValidateInvalidValue(CommandArgs.Argument arg, HashSet<CommandArgs.Argument> testedArguments)
        {
            const string NO_VALUE = "NO VALUE";
            string expected = string.Format(
                Resources.ValueInvalidException_ValueInvalidException_The_value___0___is_not_valid_for_the_argument__1___Use_one_of__2_,
                NO_VALUE, arg.ArgumentText, string.Join(@", ", arg.Values));
            expected = expected.Substring(0, expected.IndexOf(@"None, ", StringComparison.Ordinal) + 6);
            AssertEx.ThrowsException<CommandArgs.ValueInvalidException>(() => arg.GetArgumentTextWithValue(NO_VALUE), expected);
            testedArguments.Add(arg);
        }

        private void ValidateInvalidValueNumeric(CommandArgs.Argument arg, HashSet<CommandArgs.Argument> testedArguments)
        {

            const string BAD_VALUE = "-1a";
            string argText = arg.GetArgumentTextWithValue(BAD_VALUE);
            var args = new[] { "--in=" + DocumentPath, argText };
            string output = RunCommand(args);
            string BadValueString(string errorFormat) => string.Format(errorFormat, BAD_VALUE, arg.ArgumentText);
            var badValueStrings = new[]
            {
                BadValueString(Resources.ValueInvalidDoubleException_ValueInvalidDoubleException_The_value___0___is_not_valid_for_the_argument__1__which_requires_a_decimal_number_),
                BadValueString(Resources.ValueInvalidDateException_ValueInvalidDateException_The_value___0___is_not_valid_for_the_argument__1__which_requires_a_date_time_value_),
                BadValueString(Resources.ValueInvalidIntException_ValueInvalidIntException_The_value___0___is_not_valid_for_the_argument__1__which_requires_an_integer_),
                BadValueString(Resources.ValueInvalidNumberListException_ValueInvalidNumberListException_The_value__0__is_not_valid_for_the_argument__1__which_requires_a_list_of_decimal_numbers_),
                BadValueString(Resources.ValueInvalidChargeListException_ValueInvalidChargeListException_The_value___0___is_not_valid_for_the_argument__1__which_requires_an_comma_separated_list_of_integers_),
                BadValueString(Resources.ValueInvalidIonTypeListException_ValueInvalidIonTypeListException_The_value___0___is_not_valid_for_the_argument__1__which_requires_an_comma_separated_list_of_fragment_ion_types__a__b__c__x__y__z__p__),
                string.Format(Resources.CommandArgs_ParseArgsInternal_Error____0___is_not_a_valid_value_for__1___It_must_be_one_of_the_following___2_,
                    BAD_VALUE, arg.ArgumentText, arg.ValueExample()),
                string.Format(Resources.ValueInvalidException_ValueInvalidException_The_value___0___is_not_valid_for_the_argument__1___Use_one_of__2_,
                    BAD_VALUE, arg.ArgumentText, arg.ValueExample()),
                string.Format(Resources.ValueInvalidMzToleranceException_ValueInvalidMzToleranceException_The_value__0__is_not_valid_for_the_argument__1__which_requires_a_value_and_a_unit__For_example___2__,
                    BAD_VALUE, arg.ArgumentText, arg.ValueExample()),
            };
            Assert.IsTrue(badValueStrings.Any(s => output.Contains(s)),
                "{0} does not contain any of:\r\n{1}", output, TextUtil.LineSeparate(badValueStrings));
            testedArguments.Add(arg);
        }

        private void ValidateInvalidValueBool(CommandArgs.Argument arg, HashSet<CommandArgs.Argument> testedArguments)
        {
            const string BAD_VALUE = "-1a";
            AssertEx.ThrowsException<CommandArgs.ValueUnexpectedException>(() => arg.GetArgumentTextWithValue(BAD_VALUE), arg.ArgumentText);
            var args = new[] { "--in=" + DocumentPath, "--overwrite", arg.ArgumentText + "=BAD_VALUE" };
            string output = RunCommand(args);
            string expected = string.Format(Resources.ValueUnexpectedException_ValueUnexpectedException_The_argument__0__should_not_have_a_value_specified, arg.ArgumentText);
            AssertEx.Contains(TextUtil.LineSeparate(args.Append(output)), expected);
            testedArguments.Add(arg);
        }

        private void ValidateInvalidValueString(CommandArgs.Argument arg, HashSet<CommandArgs.Argument> testedArguments)
        {
            var args = new[] { "--in=" + DocumentPath, "--overwrite", arg.ArgumentText /* no value */ };
            string output = RunCommand(args);
            string expected = string.Format(Resources.ValueMissingException_ValueMissingException_, arg.ArgumentText);
            AssertEx.Contains(TextUtil.LineSeparate(args.Append(output)), expected);
            testedArguments.Add(arg);
        }

        private void ValidateInvalidValuePath(CommandArgs.Argument arg, HashSet<CommandArgs.Argument> testedArguments)
        {
            const string BAD_VALUE = "Bad:\\Path\\Value";
            string argText = arg.GetArgumentTextWithValue(BAD_VALUE);
            var args = new[] { argText };
            if (arg != CommandArgs.ARG_IN)
                args = args.Append("--in=" + DocumentPath).ToArray();
            string output = TextUtil.LineSeparate(args) + RunCommand(args);
            AssertEx.Contains(output, BAD_VALUE);
            testedArguments.Add(arg);
        }

        private void IsDocumentUnchanged(string output)
        {
            IsDocumentState(OutPath, _initProt, _initList, _initPep, _initMol, _initPrec, _initTran, output);
        }

        [TestMethod]
        public void ConsoleChangeFilterSettingsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineRefine.zip");
            DocumentPath = InitRefineDocument("SRM_mini_single_replicate.sky");
            OutPath = Path.Combine(Path.GetDirectoryName(DocumentPath) ?? string.Empty, "test.sky");

            Run(CommandArgs.ARG_REMOVE_ALL.ArgumentText);   // Remove results

            DocumentPath = OutPath;
            OutPath = Path.Combine(Path.GetDirectoryName(DocumentPath) ?? string.Empty, "test2.sky");

            // Add charge 3 and 6 precursors
            string output = Run(CommandArgs.ARG_TRAN_PRECURSOR_ION_CHARGES.GetArgumentTextWithValue("2, 3, 6"));
            AssertEx.Contains(output, "test2", PropertyNames.TransitionFilter_PeptidePrecursorChargesString);
            IsDocumentState(OutPath, 1, 4, 37, 0, 110, 969, output);

            // Add fragment ion types and charges
            var args = new List<string>
            {
                CommandArgs.ARG_TRAN_FRAGMENT_ION_TYPES.GetArgumentTextWithValue("y, b, p")
            };
            output = Run(args.ToArray());
            AssertEx.Contains(output, "test2", PropertyNames.TransitionFilter_PeptideIonTypesString);
            IsDocumentState(OutPath, 1, 4, 37, 0, 40, 715, output);
            args.Add(CommandArgs.ARG_TRAN_FRAGMENT_ION_CHARGES.GetArgumentTextWithValue("1, 2"));
            output = Run(args.ToArray());
            AssertEx.Contains(output, "test2", PropertyNames.TransitionFilter_PeptideIonTypesString,
                PropertyNames.TransitionFilter_PeptideProductChargesString);
            IsDocumentState(OutPath, 1, 4, 37, 0, 40, 1459, output);

            // Error cases
            const string typesWithError = "y, b, p, u";
            output = Run(CommandArgs.ARG_TRAN_FRAGMENT_ION_TYPES.GetArgumentTextWithValue(typesWithError));
            AssertEx.Contains(output, new CommandArgs.ValueInvalidIonTypeListException(CommandArgs.ARG_TRAN_FRAGMENT_ION_TYPES, typesWithError).Message);
            const int outOfRangeCharge = Transition.MAX_PRODUCT_CHARGE + 1;
            output = Run(CommandArgs.ARG_TRAN_FRAGMENT_ION_CHARGES.GetArgumentTextWithValue(outOfRangeCharge.ToString()));
            AssertEx.Contains(output, new CommandArgs.ValueOutOfRangeIntException(CommandArgs.ARG_TRAN_FRAGMENT_ION_CHARGES, outOfRangeCharge,
                Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE).Message);
            const int outOfRangePrecursorCharge = TransitionGroup.MAX_PRECURSOR_CHARGE + 1;
            output = Run(CommandArgs.ARG_TRAN_PRECURSOR_ION_CHARGES.GetArgumentTextWithValue("1, 2, " + outOfRangePrecursorCharge));
            AssertEx.Contains(output, new CommandArgs.ValueOutOfRangeIntException(CommandArgs.ARG_TRAN_PRECURSOR_ION_CHARGES, outOfRangePrecursorCharge,
                TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE).Message);
            const string chargesWithError = "2, 3, p, 5";
            output = Run(CommandArgs.ARG_TRAN_PRECURSOR_ION_CHARGES.GetArgumentTextWithValue(chargesWithError));
            AssertEx.Contains(output, new CommandArgs.ValueInvalidChargeListException(CommandArgs.ARG_TRAN_PRECURSOR_ION_CHARGES, chargesWithError).Message);
        }

        // Start with values for SRM_mini_single_replicate.sky
        private int _initProt = 1;
        private int _initList = 4;
        private int _initPep = 37;
        private const int _initMol = 0;
        private int _initPrec = 40;
        private int _initTran = 338;

        private string InitRefineDocument(string docName)
        {
            return InitRefineDocument(docName, _initProt, _initList, _initPep, _initPrec, _initTran);
        }

        private string InitRefineDocument(string docName, int proteins, int lists, int peptides, int tranGroups, int transitions)
        {
            string docPath = TestFilesDir.GetTestPath(docName);
            _initProt = proteins;
            _initList = lists;
            _initPep = peptides;
            _initPrec = tranGroups;
            _initTran = transitions;
            IsDocumentState(docPath, _initProt, _initList, _initPep, _initMol, _initPrec, _initTran);
            return docPath;
        }

        private void IsResultsState(string docPath, int peptides, int tranGroups, int transitions,
            string output = null, bool hasProtein = false)
        {
            IsDocumentState(docPath, hasProtein ? 1 : 0, hasProtein ? 0 : 1, peptides, 0, tranGroups, transitions, output);
        }

        private void IsDocumentState(string docPath, int proteins, int lists,
            int peptides, int molecules, int tranGroups, int transitions,
            string output = null)
        {
            if (output != null)
            {
                AssertEx.Contains(output,
                    CommandLine.RemovedText(_initProt - proteins, _initList - lists, _initPep - peptides,
                        _initMol - molecules, _initPrec - tranGroups, _initTran - transitions),
                    CommandLine.AddedText(proteins - _initProt, lists - _initList, peptides - _initPep,
                        molecules - _initMol, tranGroups - _initPrec, transitions - _initTran));
            }
            var doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, null, proteins+lists, peptides+molecules, tranGroups, transitions);
        }
    }
}