/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.MSstats.Normalization
{
    [TestClass]
    public class MsStatsNormalizationTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestGenerateMsStatsInput()
        {
            SrmDocument testDocument = OpenTestDocument();
            var memoryDocumentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(memoryDocumentContainer.SetDocument(testDocument, memoryDocumentContainer.Document));
            SkylineDataSchema skylineDataSchema = new SkylineDataSchema(memoryDocumentContainer, DataSchemaLocalizer.INVARIANT);
            var view = ReportSharing.DeserializeReportList(OpenTestFile("MSstats_report.skyr")).First().ViewSpecLayout;
            var viewContext = new DocumentGridViewContext(skylineDataSchema);
            StringWriter stringWriter = new StringWriter();
            IProgressStatus progressStatus = new ProgressStatus();
            viewContext.Export(CancellationToken.None, new SilentProgressMonitor(), ref progressStatus,
                viewContext.GetViewInfo(ViewGroup.BUILT_IN, view.ViewSpec), stringWriter,
                viewContext.GetCsvWriter());
            string expectedReport = new StreamReader(OpenTestFile("BrudererSubset_MSstatsInput.csv")).ReadToEnd();
            AssertEx.NoDiff(expectedReport, stringWriter.ToString());
        }

        [TestMethod]
        public void TestAbundancesWithNoNormalizationAsSmallMolecules()
        {
            RunTestAbundancesWithNoNormalization(true);
        }

        [TestMethod]
        public void TestAbundancesWithNoNormalization()
        {
            RunTestAbundancesWithNoNormalization(false);
        }

        private void RunTestAbundancesWithNoNormalization(bool asSmallMolecules)
        {
            SrmDocument testDocument = OpenTestDocument(asSmallMolecules);
            if (testDocument == null)
            {
                Assume.IsTrue(asSmallMolecules);
                return;
            } 
            var expected = ReadDataProcessedRows(new StreamReader(OpenTestFile("BrudererSubsetNoNormalization_dataProcessedData.csv")));
            VerifyAbundances(testDocument, asSmallMolecules, expected,
                transitionChromInfo =>
                {
                    if (transitionChromInfo.IsEmpty || transitionChromInfo.IsTruncated.GetValueOrDefault())
                    {
                        return null;
                    }
                    if (transitionChromInfo.Area < 1)
                    {
                        return 0;
                    }
                    return Math.Log(transitionChromInfo.Area, 2.0);
                });
        }



        [TestMethod]
        public void TestFoldChangeWithNoNormalizationAsSmallMolecules()
        {
            RunTestFoldChangeWithNoNormalization(true);
        }

        [TestMethod]
        public void TestFoldChangeWithNoNormalization()
        {
            RunTestFoldChangeWithNoNormalization(false);
        }

        private void RunTestFoldChangeWithNoNormalization(bool asSmallMolecules)
        {
            SrmDocument testDocument = OpenTestDocument(asSmallMolecules);
            if (testDocument == null)
            {
                Assume.IsTrue(asSmallMolecules);
                return;
            }
            var expectedResults = MsStatsTestUtil.ReadExpectedResults(typeof(MsStatsNormalizationTest),
                "BrudererSubsetNoNormalization_TestingResult.csv");
            GroupComparisonDef groupComparisonDef = new GroupComparisonDef("test")
                .ChangeControlValue("S2")
                .ChangeCaseValue("S1")
                .ChangeControlAnnotation("Condition")
                .ChangeIdentityAnnotation("BioReplicate")
                .ChangePerProtein(true)
                .ChangeNormalizationMethod(NormalizationMethod.NONE);
            VerifyFoldChanges(testDocument, groupComparisonDef, expectedResults);
        }

        [TestMethod]
        public void TestAbundancesEqualizeMediansAsSmallMolecules()
        {
            RunTestAbundancesEqualizeMedians(true);
        }

        [TestMethod]
        public void TestAbundancesEqualizeMedians()
        {
            RunTestAbundancesEqualizeMedians(false);
        }

        private void RunTestAbundancesEqualizeMedians(bool asSmallMolecules)
        {
            SrmDocument testDocument = OpenTestDocument(asSmallMolecules);
            if (testDocument == null)
            {
                Assume.IsTrue(asSmallMolecules);
                return;
            }
            var chromatograms = testDocument.Settings.MeasuredResults.Chromatograms;
            var expected = ReadDataProcessedRows(new StreamReader(OpenTestFile("BrudererSubsetEqualizeMedians_dataProcessedData.csv")));
            NormalizationData normalizationData = NormalizationData.GetNormalizationData(testDocument, false, null);
            var mediansByReplicateFileIndex = chromatograms.ToDictionary(chrom => chrom.MSDataFileInfos.First().FileIndex,
                chrom => normalizationData.GetMedian(chrom.MSDataFileInfos.First().FileId, IsotopeLabelType.light).Value);
            double medianMedian = new Statistics(mediansByReplicateFileIndex.Values).Median();
            VerifyAbundances(testDocument, asSmallMolecules, expected, transitionChromInfo =>
            {
                if (transitionChromInfo.IsEmpty || transitionChromInfo.IsTruncated.GetValueOrDefault())
                {
                    return null;
                }

                if (transitionChromInfo.Area < 1)
                {
                    return 0;
                }
                else
                {
                    double abundance = Math.Log(transitionChromInfo.Area, 2.0);
                    abundance -= mediansByReplicateFileIndex[transitionChromInfo.FileIndex];
                    abundance += medianMedian;
                    return abundance;
                }
            });
        }

        [TestMethod]
        public void TestFoldChangeEqualizeMediansAsSmallMolecules()
        {
            RunTestFoldChangeEqualizeMedians(true);
        }

        [TestMethod]
        public void TestFoldChangeEqualizeMedians()
        {
            RunTestFoldChangeEqualizeMedians(true);
        }

        public void RunTestFoldChangeEqualizeMedians(bool asSmallMolecules)
        {
            SrmDocument testDocument = OpenTestDocument();
            if (testDocument == null)
            {
                Assume.IsTrue(asSmallMolecules);
                return;
            }
            var expectedResults = MsStatsTestUtil.ReadExpectedResults(typeof(MsStatsNormalizationTest),
                "BrudererSubsetEqualizeMedians_TestingResult.csv");
            GroupComparisonDef groupComparisonDef = new GroupComparisonDef("test")
                .ChangeControlValue("S2")
                .ChangeCaseValue("S1")
                .ChangeControlAnnotation("Condition")
                .ChangeIdentityAnnotation("BioReplicate")
                .ChangePerProtein(true)
                .ChangeNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS);
            VerifyFoldChanges(testDocument, groupComparisonDef, expectedResults);
        }

        private void VerifyAbundances(SrmDocument testDocument, bool asSmallMolecules, Dictionary<DataProcessedRowKey, double?> expected, Func<TransitionChromInfo, double?> calcAbundance)
        {
            var chromatograms = testDocument.Settings.MeasuredResults.Chromatograms;
            foreach (var protein in testDocument.MoleculeGroups)
            {
                foreach (var peptide in protein.Molecules)
                {
                    if (peptide.GlobalStandardType != null)
                    {
                        continue;
                    }
                    foreach (var precursor in peptide.TransitionGroups)
                    {
                        string seqCharge = testDocument.Settings.GetPrecursorCalc(
                                precursor.TransitionGroup.LabelType, peptide.ExplicitMods)
                                  .GetModifiedSequence(peptide.Peptide.Target, true) + "_" + precursor.PrecursorAdduct.AsFormulaOrInt();
                        // If we're running as small molecules, transform the result to match the known-good peptide results
                        var expectedSeqCharge = asSmallMolecules ?
                            seqCharge.Replace(RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator, string.Empty).
                            Replace(".0]", "]").Replace("_[M+H]", "_1").Replace("_[M+", "_").Replace("H]", string.Empty) : // pep_PEPT[+57.0]IDER_[M+3H] -> PEPT[+57]IDER_3
                            seqCharge;
                        foreach (var transition in precursor.Transitions)
                        {
                            string transitionCharge = transition.FragmentIonName + "_" + transition.Transition.Charge;
                            var expectedTransitionCharge = asSmallMolecules
                                ? transitionCharge.Replace("[-", " -").Replace("]_", "_") // "y3[-18]_1" -? "y3 -18_1"
                                : transitionCharge;
                            for (int iReplicate = 0; iReplicate < chromatograms.Count; iReplicate++)
                            {
                                var transitionChromInfo = transition.Results[iReplicate].First();
                                double? abundance = calcAbundance(transitionChromInfo);
                                var expectedAbundance = expected[new DataProcessedRowKey()
                                {
                                    Protein = protein.Name,
                                    Peptide = expectedSeqCharge,
                                    Transition = expectedTransitionCharge,
                                    Run = iReplicate + 1
                                }];
                                const double epsilon = 1E-8;
                                string msg = protein.Name + '_' + seqCharge + '_' + transitionCharge + '_' + (iReplicate + 1);
                                if (abundance.HasValue)
                                {
                                    if (expectedAbundance == null)
                                        Assert.IsNotNull(expectedAbundance, msg);
                                    Assert.AreEqual(expectedAbundance.Value, abundance.Value, epsilon, msg);
                                }
                                else
                                {
                                    Assert.IsNull(expectedAbundance, msg);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void VerifyFoldChanges(SrmDocument testDocument, GroupComparisonDef groupComparisonDef,
            IDictionary<string, LinearFitResult> expectedResults)
        {
            var groupComparer = new GroupComparer(groupComparisonDef, testDocument, new QrFactorizationCache());
            foreach (var protein in testDocument.MoleculeGroups)
            {
                var groupComparisonResult = groupComparer.CalculateFoldChanges(protein, null).FirstOrDefault();
                LinearFitResult expectedResult;
                if (expectedResults.TryGetValue(protein.Name, out expectedResult))
                {
                    Assert.IsNotNull(groupComparisonResult);
                    var foldChange = groupComparisonResult.LinearFitResult;
                    Assert.AreEqual(expectedResult.EstimatedValue, foldChange.EstimatedValue, 1E-5);
                    Assert.AreEqual(expectedResult.DegreesOfFreedom, foldChange.DegreesOfFreedom);
                    Assert.AreEqual(expectedResult.StandardError, foldChange.StandardError, 1E-5);
                    Assert.AreEqual(expectedResult.TValue, foldChange.TValue, 1E-5);
                    Assert.AreEqual(expectedResult.PValue, foldChange.PValue, 1E-5);
                }
                else
                {
                    Assert.IsNull(groupComparisonResult);
                    var standardPeptides = protein.Molecules.Where(mol => null != mol.GlobalStandardType).ToArray();
                    Assert.AreNotEqual(0, standardPeptides.Length);
                }
            }
        }

        private SrmDocument OpenTestDocument(bool asSmallMolecules = false)
        {
            if (asSmallMolecules)
            {
                if (SkipSmallMoleculeTestVersions())
                {
                    return null;
                }
            }
            using (var stream = OpenTestFile(asSmallMolecules ? "BrudererSubsetAsSmallMolecules.sky" : "BrudererSubset.sky"))
            {
                return (SrmDocument) new XmlSerializer(typeof (SrmDocument)).Deserialize(stream);
            }
        }

        private Stream OpenTestFile(string name)
        {
            return typeof (MsStatsNormalizationTest).Assembly
                .GetManifestResourceStream(typeof (MsStatsNormalizationTest), name);
        }

        Dictionary<DataProcessedRowKey, double?> ReadDataProcessedRows(TextReader reader)
        {
            DsvFileReader csvReader = new DsvFileReader(reader, TextUtil.SEPARATOR_CSV);
            var rows = new Dictionary<DataProcessedRowKey, double?>();
            while (null != csvReader.ReadLine())
            {

                DataProcessedRowKey dataProcessedRow = new DataProcessedRowKey
                {
                    Protein = csvReader.GetFieldByName("PROTEIN"),
                    Peptide = csvReader.GetFieldByName("PEPTIDE"),
                    Transition = csvReader.GetFieldByName("TRANSITION"),
                    Run = int.Parse(csvReader.GetFieldByName("RUN"), CultureInfo.InvariantCulture),
                };

                String strAbundance = csvReader.GetFieldByName("ABUNDANCE");
                double? abundance = "NA" == strAbundance
                    ? default(double?)
                    : double.Parse(strAbundance, CultureInfo.InvariantCulture);
                rows.Add(dataProcessedRow, abundance);
            }
            return rows;
        }

        struct DataProcessedRowKey
        {
            public String Protein { get; set; }
            public String Peptide { get; set; }
            public String Transition { get; set; }
            public int Run { get; set; }
        }
    }
}
