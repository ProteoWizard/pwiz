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
                exportLiveReportDlg.OkDialog(TestFilesDir.GetTestPath("PRISM.parquet"));
            });

            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            {
                exportLiveReportDlg.ReportName = "MedianPolishedAreas";
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.OkDialog(
                    TestFilesDir.GetTestPath("MedianPolishedAreas.parquet"));
            });

            var document = SkylineWindow.Document;
            var expectedMedianPolishedAreas = ReadExpectedMedianPolishedAreas(document, TestFilesDir.GetTestPath("peptides_rollup.parquet"));
            Assert.IsNotNull(expectedMedianPolishedAreas);
            var normalizedValueCalculator = new NormalizedValueCalculator(document);
            var maxDifference = 0.0;
            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    var identityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                    var peptideQuantifier = new PeptideQuantifier(normalizedValueCalculator, moleculeGroup.PeptideGroup,
                        molecule, document.Settings.PeptideSettings.Quantification);
                    var medianPolishedValue = peptideQuantifier.PolishUnnormalizedTransitions(document.Settings, null);
                    // Skyline's polish skips truncated transitions; PRISM does not. For
                    // peptides where any transition has a truncated peak in any replicate,
                    // the two implementations operate on different inputs and the comparison
                    // is not meaningful.
                    if (HasAnyTruncatedTransition(molecule))
                    {
                        continue;
                    }
                    if (!expectedMedianPolishedAreas.TryGetValue(identityPath, out var expected))
                    {
                        expected = new double?[medianPolishedValue.Length];
                    }
                    Assert.AreEqual(expected.Length, medianPolishedValue.Length);
                    for (int i = 0; i < expected.Length; i++)
                    {
                        var expectedValue = expected[i];
                        var actualValue = medianPolishedValue[i];
                        if (expectedValue.HasValue)
                        {
                            Assert.IsNotNull(actualValue);
                            Assert.AreEqual(expectedValue.Value, actualValue.Value, _epsilon, "Mismatch on {0} replicate {1}", molecule.ModifiedSequence, i);
                            maxDifference = Math.Max(maxDifference, Math.Abs(expectedValue.Value - actualValue.Value));
                        }
                        else
                        {
                            Assert.IsNull(actualValue);
                        }
                    }
                }
            }

            Console.Out.WriteLine("Maximum difference: {0}", maxDifference);

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

        private Dictionary<IdentityPath, double?[]> ReadExpectedMedianPolishedAreas(SrmDocument document,
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
