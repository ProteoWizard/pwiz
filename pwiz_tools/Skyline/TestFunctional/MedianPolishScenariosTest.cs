using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MedianPolishScenariosTest : AbstractFunctionalTest
    {
        private double _epsilon = 1e-6;

        // RT-loess fits a smoothing curve across peptides; with too few peptides the curve is
        // unstable and skyline-prism's lowess and Skyline's LoessInterpolator do not agree
        // closely. Only verify RT-loess on datasets with at least this many peptides.
        private const int MIN_PEPTIDES_FOR_RT_LOESS = 100;
        [TestMethod]
        public void TestMedianPolishSmall()
        {
            TestFilesZip = @"TestFunctional\MedianPolishSmallTest.zip";
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestMedianPolishMedium()
        {
            TestFilesZip = @"TestFunctional\MedianPolishMediumTest.zip";
            RunFunctionalTest();
        }


        protected override void DoTest()
        {
            var skyFileName = Directory.GetFiles(TestFilesDir.FullPath, "*.sky").FirstOrDefault();
            Assert.IsNotNull(skyFileName);
            RunUI(() => SkylineWindow.OpenFile(skyFileName));
            WaitForDocumentLoaded();
            // RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            // {
            //     exportLiveReportDlg.ReportName = "Protein Abundances";
            //     exportLiveReportDlg.SetUseInvariantLanguage(true);
            //     exportLiveReportDlg.OkDialog(
            //         TestFilesDir.GetTestPath("ProteinAbundances-Plasma-MagNet-Elute-Small.parquet"));
            // });
            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            {
                exportLiveReportDlg.ReportName = "PRISM";
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.OkDialog(TestFilesDir.GetTestPath("PRISM-current.parquet"));
            });
            CompareParquetFiles(TestFilesDir.GetTestPath("PRISM.parquet"),
                TestFilesDir.GetTestPath("PRISM-current.parquet"));

            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            {
                exportLiveReportDlg.ReportName = "MedianPolishedAreas";
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.OkDialog(
                    TestFilesDir.GetTestPath("MedianPolishedAreas.parquet"));
            });

            var document = SkylineWindow.Document;
            var normalizedValueCalculator = new NormalizedValueCalculator(document);

            var expectedMedianPolishedAreas = ReadExpectedPeptideAreas(document, TestFilesDir.GetTestPath("unnormalized_polished_peptides.parquet"));
            var maxDifference = VerifyPeptideAreas(document, expectedMedianPolishedAreas,
                identityPath => PolishUnnormalizedTransitions(normalizedValueCalculator, identityPath), _epsilon);
            Console.Out.WriteLine("Maximum difference median polished peptides: {0}", maxDifference);

            var expectedSummedAreas = ReadExpectedPeptideAreas(document, TestFilesDir.GetTestPath("unnormalized_summed_peptides.parquet"));
            var maxDifferenceSummed = VerifyPeptideAreas(document, expectedSummedAreas,
                identityPath => SumUnnormalizedTransitions(normalizedValueCalculator, identityPath), 1e-2);
            Console.Out.WriteLine("Maximum difference summed peptides: {0}", maxDifferenceSummed);

            var expectedMedianNormalized = ReadExpectedPeptideAreas(document, TestFilesDir.GetTestPath("median_normalized_peptides.parquet"));
            var maxDifferenceMedianNormalized = VerifyPeptideAreas(document, expectedMedianNormalized,
                identityPath => MedianPolishNormalized(normalizedValueCalculator, identityPath, NormalizationMethod.EQUALIZE_MEDIANS), 1e-1);
            Console.Out.WriteLine("Maximum difference median normalized peptides: {0}", maxDifferenceMedianNormalized);

            var expectedRtLoessNormalized = ReadExpectedPeptideAreas(document, TestFilesDir.GetTestPath("rtloess_normalized_peptides.parquet"));
            if (expectedRtLoessNormalized.Count >= MIN_PEPTIDES_FOR_RT_LOESS)
            {
                var maxDifferenceRtLoessNormalized = VerifyPeptideAreas(document, expectedRtLoessNormalized,
                    identityPath => MedianPolishNormalized(normalizedValueCalculator, identityPath, NormalizationMethod.RT_LOESS), 1);
                Console.Out.WriteLine("Maximum difference RT loess normalized peptides: {0}", maxDifferenceRtLoessNormalized);
            }

            // Switch the document to NormalizationMethod=None so the protein-level
            // median polish runs on un-normalized peptide values, then export the
            // "Protein Abundances" report to compare against an unnormalized PRISM run.
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                peptideSettingsUi.QuantNormalizationMethod = NormalizationMethod.NONE;
                peptideSettingsUi.OkDialog();
            });
            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            {
                exportLiveReportDlg.ReportName = "Protein Abundances";
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.OkDialog(TestFilesDir.GetTestPath("ProteinAbundances.parquet"));
            });
        }

        private double VerifyPeptideAreas(SrmDocument document, Dictionary<IdentityPath, double?[]> expectedAreas,
            Func<IdentityPath, double?[]> calculatorFunc, double delta)
        {
            double maxDifference = 0;
            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    var identityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                    var actual = calculatorFunc(identityPath);
                    if (actual == null)
                    {
                        continue;
                    }
                    if (!expectedAreas.TryGetValue(identityPath, out var expected))
                    {
                        expected = new double?[actual.Length];
                    }
                    Assert.AreEqual(expected.Length, actual.Length);
                    for (int i = 0; i < expected.Length; i++)
                    {
                        var expectedValue = expected[i];
                        var actualValue = actual[i];
                        if (expectedValue.HasValue)
                        {
                            Assert.IsNotNull(actualValue, "Mismatch on {0} replicate {1}", molecule.ModifiedSequence, i);
                            Assert.AreEqual(expectedValue.Value, actualValue.Value, delta, "Mismatch on {0} replicate {1}", molecule.ModifiedSequence, i);
                            maxDifference = Math.Max(maxDifference, Math.Abs(expectedValue.Value - actualValue.Value));
                        }
                        else
                        {
                            Assert.IsNull(actualValue, "Mismatch on {0} replicate {1}", molecule.ModifiedSequence, i);
                        }
                    }
                }
            }

            return maxDifference;
        }

        private double?[] PolishUnnormalizedTransitions(NormalizedValueCalculator normalizedValueCalculator, IdentityPath moleculeIdentityPath)
        {
            var document = normalizedValueCalculator.Document;
            var moleculeGroup = (PeptideGroupDocNode) document.FindNode(moleculeIdentityPath.GetIdentity(0));
            var molecule = (PeptideDocNode) moleculeGroup.FindNode(moleculeIdentityPath.GetIdentity(1));
            var peptideQuantifier = new PeptideQuantifier(normalizedValueCalculator, moleculeGroup.PeptideGroup,
                molecule, document.Settings.PeptideSettings.Quantification)
            {
                ImputeMissingValues = true
            };
            return peptideQuantifier.PolishUnnormalizedTransitions(document.Settings, null);
        }

        private double?[] SumUnnormalizedTransitions(NormalizedValueCalculator normalizedValueCalculator, IdentityPath moleculeIdentityPath)
        {
            var document = normalizedValueCalculator.Document;
            var moleculeGroup = (PeptideGroupDocNode) document.FindNode(moleculeIdentityPath.GetIdentity(0));
            var molecule = (PeptideDocNode) moleculeGroup.FindNode(moleculeIdentityPath.GetIdentity(1));
            // Sum the un-normalized transition areas (NormalizationMethod.NONE) so the result
            // matches skyline-prism's "sum" rollup in "unnormalized_summed_peptides.parquet".
            var quantificationSettings = document.Settings.PeptideSettings.Quantification
                .ChangeNormalizationMethod(NormalizationMethod.NONE);
            var peptideQuantifier = new PeptideQuantifier(normalizedValueCalculator, moleculeGroup.PeptideGroup,
                molecule, quantificationSettings)
            {
                ImputeMissingValues = true
            };
            return peptideQuantifier.GetPeptideLog2Abundances(document.Settings, null, SummarizationMethod.AVERAGING);
        }

        /// <summary>
        /// Returns the median-polished peptide log2 abundances with the given peptide-level
        /// normalization applied (EQUALIZE_MEDIANS or RT_LOESS), matching skyline-prism's
        /// "median_polish + normalization" output. The normalization factor is derived from
        /// the document's polished peptide abundances, exactly as production quantification does.
        /// </summary>
        private double?[] MedianPolishNormalized(NormalizedValueCalculator normalizedValueCalculator,
            IdentityPath moleculeIdentityPath, NormalizationMethod normalizationMethod)
        {
            var document = normalizedValueCalculator.Document;
            var moleculeGroup = (PeptideGroupDocNode) document.FindNode(moleculeIdentityPath.GetIdentity(0));
            var molecule = (PeptideDocNode) moleculeGroup.FindNode(moleculeIdentityPath.GetIdentity(1));
            var quantificationSettings = document.Settings.PeptideSettings.Quantification
                .ChangeNormalizationMethod(normalizationMethod);
            var peptideQuantifier = new PeptideQuantifier(normalizedValueCalculator, moleculeGroup.PeptideGroup,
                molecule, quantificationSettings)
            {
                ImputeMissingValues = true
            };
            return peptideQuantifier.GetMedianPolishQuantities(document.Settings,
                PeptideQuantifier.GetMedianPolishReplicates(document.Settings));
        }

        /// <summary>
        /// Asserts that the two Parquet files have the same schema and exactly the same
        /// values in every cell.
        /// </summary>
        private void CompareParquetFiles(string expectedPath, string actualPath)
        {
            using var expectedReader = ParquetReader.CreateAsync(expectedPath).ConfigureAwait(false).GetAwaiter().GetResult();
            using var actualReader = ParquetReader.CreateAsync(actualPath).ConfigureAwait(false).GetAwaiter().GetResult();
            var expectedFields = expectedReader.Schema.Fields.Select(field => field.Name).ToList();
            var actualFields = actualReader.Schema.Fields.Select(field => field.Name).ToList();
            CollectionAssert.AreEqual(expectedFields, actualFields,
                "Schema of {0} does not match {1}", expectedPath, actualPath);
            var expectedTable = expectedReader.ReadAsTableAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            var actualTable = actualReader.ReadAsTableAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.AreEqual(expectedTable.Count, actualTable.Count,
                "Row count of {0} does not match {1}", expectedPath, actualPath);
            for (int iRow = 0; iRow < expectedTable.Count; iRow++)
            {
                var expectedRow = expectedTable[iRow];
                var actualRow = actualTable[iRow];
                for (int iCol = 0; iCol < expectedFields.Count; iCol++)
                {
                    Assert.AreEqual(expectedRow[iCol], actualRow[iCol],
                        "Mismatch in column {0} row {1}", expectedFields[iCol], iRow);
                }
            }
        }

        private static bool HasAnyTruncatedTransition(PeptideDocNode molecule)
        {
            foreach (var transitionGroup in molecule.TransitionGroups)
            {
                foreach (var transition in transitionGroup.Transitions)
                {
                    if (transition.Results == null)
                    {
                        continue;
                    }
                    foreach (var chromInfoList in transition.Results)
                    {
                        foreach (var chromInfo in chromInfoList)
                        {
                            if (chromInfo.IsTruncated.GetValueOrDefault())
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private Dictionary<IdentityPath, double?[]> ReadExpectedPeptideAreas(SrmDocument document,
            string filePath)
        {
            using var reader = ParquetReader.CreateAsync(filePath).ConfigureAwait(false).GetAwaiter().GetResult();
            var peptidesByModifiedSequence = GetPeptidesByModifiedSequence(document);
            var fieldIndexes = reader.Schema.Fields.Select((field, index) => new { field.Name, Index = index })
                .ToDictionary(x => x.Name, x => x.Index);
            var icolPeptideSequence = fieldIndexes["PeptideModifiedSequenceUnimodIds"];
            var replicateColumns = new List<int>();
            foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
            {
                var columnName = fieldIndexes.Keys.FirstOrDefault(k => k.StartsWith(chromatogramSet.Name));
                Assert.IsNotNull(columnName, "Unable to find column for replicate {0}", chromatogramSet.Name);
                replicateColumns.Add(fieldIndexes[columnName]);
            }
            var result = new Dictionary<IdentityPath, double?[]>();
            foreach (var row in reader.ReadAsTableAsync().ConfigureAwait(false).GetAwaiter().GetResult())
            {
                var peptideSequence = row[icolPeptideSequence]!.ToString();
                var values = replicateColumns.Select(i => i >= 0 ? (double?)row[i] : null).ToArray();
                int count = 0;
                foreach (var identityPath in peptidesByModifiedSequence[peptideSequence])
                {
                    result.Add(identityPath, values);
                    count++;
                }
                Assert.AreNotEqual(0, "Peptide {0} not found", peptideSequence);
            }

            return result;
        }

        private ILookup<string, IdentityPath> GetPeptidesByModifiedSequence(SrmDocument document)
        {
            return document.MoleculeGroups.SelectMany(moleculeGroup => moleculeGroup.Molecules, Tuple.Create).ToLookup(
                tuple => ModifiedSequence.GetModifiedSequence(document.Settings, tuple.Item2, IsotopeLabelType.light).UnimodIds,
                tuple => new IdentityPath(tuple.Item1.PeptideGroup, tuple.Item2.Peptide));
        }
    }
}
