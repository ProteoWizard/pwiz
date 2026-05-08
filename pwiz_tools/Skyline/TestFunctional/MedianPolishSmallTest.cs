using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MedianPolishSmallTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMedianPolishSmall()
        {
            TestFilesZip = @"TestFunctional\MedianPolishSmallTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MedianPolishSmallTest.sky")));
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
                exportLiveReportDlg.OkDialog(TestFilesDir.GetTestPath("PRISM-MedianPolishSmallTest.parquet"));
            });

            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            {
                exportLiveReportDlg.ReportName = "MedianPolishedAreas";
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.OkDialog(
                    TestFilesDir.GetTestPath("MedianPolishedAreas-MedianPolishSmallTest.parquet"));
            });

            var document = SkylineWindow.Document;
            var expectedMedianPolishedAreas = ReadExpectedMedianPolishedAreas(document, TestFilesDir.GetTestPath("peptides_rollup.parquet"));
            Assert.IsNotNull(expectedMedianPolishedAreas);
            var normalizedValueCalculator = new NormalizedValueCalculator(document);
            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    var identityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                    var peptideQuantifier = new PeptideQuantifier(normalizedValueCalculator, moleculeGroup.PeptideGroup,
                        molecule, document.Settings.PeptideSettings.Quantification);
                    var medianPolishedValue = peptideQuantifier.GetMedianPolishQuantities(document.Settings, null);
                    var expected = expectedMedianPolishedAreas[identityPath];
                    Assert.AreEqual(expected.Length, medianPolishedValue.Length);
                    for (int i = 0; i < expected.Length; i++)
                    {
                        var expectedValue = expected[i];
                        var actualValue = medianPolishedValue[i];
                        if (expectedValue.HasValue)
                        {
                            Assert.IsNotNull(actualValue);
                            Assert.AreEqual(expectedValue.Value, actualValue.Value, .1, "Mismatch on {0} replicate {1}", molecule.ModifiedSequence, i);
                        }
                        else
                        {
                            Assert.IsNull(actualValue);
                        }
                    }
                }
            }
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
                foreach (var identityPath in peptidesByModifiedSequence[peptideSequence])
                {
                    result.Add(identityPath, values);
                }
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
