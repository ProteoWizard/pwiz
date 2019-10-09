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
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
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
            output = Run(CommandArgs.ARG_TRAN_PREDICT_CE.GetArgumentTextWithValue(CollisionEnergyList.ELEMENT_NONE));
            AssertEx.Contains(output, "test2", PropertyNames.TransitionPrediction_NonNullCollisionEnergy, "Thermo", CollisionEnergyList.ELEMENT_NONE);
            IsDocumentUnchanged(output);
            output = Run(CommandArgs.ARG_TRAN_PREDICT_DP.GetArgumentTextWithValue("SCIEX"));
            AssertEx.Contains(output, "test2", PropertyNames.TransitionPrediction_NonNullDeclusteringPotential, DeclusterPotentialList.ELEMENT_NONE, "SCIEX");
            IsDocumentUnchanged(output);
            output = Run(CommandArgs.ARG_TRAN_PREDICT_COV.GetArgumentTextWithValue("SCIEX"));
            AssertEx.Contains(output, "test2", PropertyNames.TransitionPrediction_NonNullCompensationVoltage, CompensationVoltageList.ELEMENT_NONE, "SCIEX");
            IsDocumentUnchanged(output);
            // Only None is possible for optimization libraries without setting one up
            output = Run(CommandArgs.ARG_TRAN_PREDICT_OPTDB.GetArgumentTextWithValue(OptimizationLibraryList.ELEMENT_NONE));
            AssertEx.Contains(output, "test2", Resources.CommandLine_LogNewEntries_Document_unchanged);
            IsDocumentUnchanged(output);

            // Invalid changes
            ValidateInvalidValue(CommandArgs.ARG_TRAN_PREDICT_CE, Settings.Default.CollisionEnergyList);
            ValidateInvalidValue(CommandArgs.ARG_TRAN_PREDICT_DP, Settings.Default.DeclusterPotentialList);
            ValidateInvalidValue(CommandArgs.ARG_TRAN_PREDICT_COV, Settings.Default.CompensationVoltageList);
            ValidateInvalidValue(CommandArgs.ARG_TRAN_PREDICT_OPTDB, Settings.Default.OptimizationLibraryList);
        }

        private void ValidateInvalidValue<TItem>(CommandArgs.Argument arg, SettingsListBase<TItem> list) where TItem : IKeyContainer<string>, IXmlSerializable
        {
            const string NO_VALUE = "NO VALUE";
            string expected = string.Format(
                Resources.ValueInvalidException_ValueInvalidException_The_value___0___is_not_valid_for_the_argument__1___Use_one_of__2_,
                NO_VALUE, arg.ArgumentText, CommandArgs.GetDisplayNames(list));
            expected = expected.Substring(0, expected.IndexOf(@"None, ", StringComparison.Ordinal) + 6);
            AssertEx.ThrowsException<CommandArgs.ValueInvalidException>(() => Run(arg.GetArgumentTextWithValue(NO_VALUE)), expected);
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