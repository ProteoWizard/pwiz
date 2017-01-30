using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ExportSpectralLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExportSpectralLibrary()
        {
            TestFilesZip = @"TestFunctional\ExportSpectralLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Export and check spectral library
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("msstatstest.sky")));
            var doc = WaitForDocumentLoaded();
            var exported = TestFilesDir.GetTestPath("export.blib");
            Skyline.SkylineWindow.ExportSpectralLibrary(SkylineWindow.DocumentFilePath, SkylineWindow.Document, exported, null);
            Assert.IsTrue(File.Exists(exported));

            var refSpectra = new List<DbRefSpectra>();
            using (var connection = new SQLiteConnection(string.Format("Data Source='{0}';Version=3", exported)))
            {
                connection.Open();
                using (var select = new SQLiteCommand(connection)
                {
                    CommandText = "SELECT * FROM RefSpectra"
                })
                using (var reader = select.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        refSpectra.Add(new DbRefSpectra
                        {
                            PeptideSeq = reader["peptideSeq"].ToString(),
                            PeptideModSeq = reader["peptideModSeq"].ToString(),
                            PrecursorCharge = int.Parse(reader["precursorCharge"].ToString()),
                            PrecursorMZ = double.Parse(reader["precursorMZ"].ToString()),
                            NumPeaks = ushort.Parse(reader["numPeaks"].ToString())
                        });
                    }
                }
            }
            CheckRefSpectra(refSpectra, "APVPTGEVYFADSFDR", "APVPTGEVYFADSFDR", 2, 885.920, 4);
            CheckRefSpectra(refSpectra, "APVPTGEVYFADSFDR", "APVPTGEVYFADSFDR[+10.0]", 2, 890.924, 4);
            CheckRefSpectra(refSpectra, "AVTELNEPLSNEDR", "AVTELNEPLSNEDR", 2, 793.886, 4);
            CheckRefSpectra(refSpectra, "AVTELNEPLSNEDR", "AVTELNEPLSNEDR[+10.0]", 2, 798.891, 4);
            CheckRefSpectra(refSpectra, "DQGGELLSLR", "DQGGELLSLR", 2, 544.291, 4);
            CheckRefSpectra(refSpectra, "DQGGELLSLR", "DQGGELLSLR[+10.0]", 2, 549.295, 4);
            CheckRefSpectra(refSpectra, "ELLTTMGDR", "ELLTTMGDR", 2, 518.261, 4);
            CheckRefSpectra(refSpectra, "ELLTTMGDR", "ELLTTMGDR[+10.0]", 2, 523.265, 4);
            CheckRefSpectra(refSpectra, "FEELNADLFR", "FEELNADLFR", 2, 627.312, 3);
            CheckRefSpectra(refSpectra, "FEELNADLFR", "FEELNADLFR[+10.0]", 2, 632.316, 4);
            CheckRefSpectra(refSpectra, "FHQLDIDDLQSIR", "FHQLDIDDLQSIR", 2, 800.410, 3);
            CheckRefSpectra(refSpectra, "FHQLDIDDLQSIR", "FHQLDIDDLQSIR[+10.0]", 2, 805.414, 4);
            CheckRefSpectra(refSpectra, "FLIPNASQAESK", "FLIPNASQAESK", 2, 652.846, 4);
            CheckRefSpectra(refSpectra, "FLIPNASQAESK", "FLIPNASQAESK[+8.0]", 2, 656.853, 4);
            CheckRefSpectra(refSpectra, "FTPGTFTNQIQAAFR", "FTPGTFTNQIQAAFR", 2, 849.934, 4);
            CheckRefSpectra(refSpectra, "FTPGTFTNQIQAAFR", "FTPGTFTNQIQAAFR[+10.0]", 2, 854.938, 4);
            CheckRefSpectra(refSpectra, "ILTFDQLALDSPK", "ILTFDQLALDSPK", 2, 730.903, 4);
            CheckRefSpectra(refSpectra, "ILTFDQLALDSPK", "ILTFDQLALDSPK[+8.0]", 2, 734.910, 4);
            CheckRefSpectra(refSpectra, "LSSEMNTSTVNSAR", "LSSEMNTSTVNSAR", 2, 748.854, 4);
            CheckRefSpectra(refSpectra, "LSSEMNTSTVNSAR", "LSSEMNTSTVNSAR[+10.0]", 2, 753.858, 4);
            CheckRefSpectra(refSpectra, "NIVEAAAVR", "NIVEAAAVR", 2, 471.772, 4);
            CheckRefSpectra(refSpectra, "NIVEAAAVR", "NIVEAAAVR[+10.0]", 2, 476.776, 4);
            CheckRefSpectra(refSpectra, "NLQYYDISAK", "NLQYYDISAK", 2, 607.806, 4);
            CheckRefSpectra(refSpectra, "NLQYYDISAK", "NLQYYDISAK[+8.0]", 2, 611.813, 4);
            CheckRefSpectra(refSpectra, "TSAALSTVGSAISR", "TSAALSTVGSAISR", 2, 660.860, 4);
            CheckRefSpectra(refSpectra, "TSAALSTVGSAISR", "TSAALSTVGSAISR[+10.0]", 2, 665.864, 4);
            CheckRefSpectra(refSpectra, "VHIEIGPDGR", "VHIEIGPDGR", 2, 546.793, 4);
            CheckRefSpectra(refSpectra, "VHIEIGPDGR", "VHIEIGPDGR[+10.0]", 2, 551.798, 4);
            CheckRefSpectra(refSpectra, "VLTPELYAELR", "VLTPELYAELR", 2, 652.366, 4);
            CheckRefSpectra(refSpectra, "VLTPELYAELR", "VLTPELYAELR[+10.0]", 2, 657.371, 4);
            CheckRefSpectra(refSpectra, "VNLAELFK", "VNLAELFK", 2, 467.274, 4);
            CheckRefSpectra(refSpectra, "VNLAELFK", "VNLAELFK[+8.0]", 2, 471.281, 4);
            CheckRefSpectra(refSpectra, "VPDFSEYR", "VPDFSEYR", 2, 506.740, 4);
            CheckRefSpectra(refSpectra, "VPDFSEYR", "VPDFSEYR[+10.0]", 2, 511.744, 4);
            CheckRefSpectra(refSpectra, "VPDGMVGFIIGR", "VPDGMVGFIIGR", 2, 630.842, 4);
            CheckRefSpectra(refSpectra, "VPDGMVGFIIGR", "VPDGMVGFIIGR[+10.0]", 2, 635.846, 4);
            CheckRefSpectra(refSpectra, "ADVTPADFSEWSK", "ADVTPADFSEWSK", 2, 726.836, 3);
            CheckRefSpectra(refSpectra, "DGLDAASYYAPVR", "DGLDAASYYAPVR", 2, 699.338, 3);
            CheckRefSpectra(refSpectra, "GAGSSEPVTGLDAK", "GAGSSEPVTGLDAK", 2, 644.823, 3);
            CheckRefSpectra(refSpectra, "GTFIIDPAAVIR", "GTFIIDPAAVIR", 2, 636.869, 3);
            CheckRefSpectra(refSpectra, "GTFIIDPGGVIR", "GTFIIDPGGVIR", 2, 622.854, 3);
            CheckRefSpectra(refSpectra, "LFLQFGAQGSPFLK", "LFLQFGAQGSPFLK", 2, 776.930, 3);
            CheckRefSpectra(refSpectra, "LGGNEQVTR", "LGGNEQVTR", 2, 487.257, 3);
            CheckRefSpectra(refSpectra, "TPVISGGPYEYR", "TPVISGGPYEYR", 2, 669.838, 3);
            CheckRefSpectra(refSpectra, "TPVITGAPYEYR", "TPVITGAPYEYR", 2, 683.854, 3);
            CheckRefSpectra(refSpectra, "VEATFGVDESNAK", "VEATFGVDESNAK", 2, 683.828, 3);
            CheckRefSpectra(refSpectra, "YILAGVENSK", "YILAGVENSK", 2, 547.298, 3);
            Assert.IsTrue(!refSpectra.Any());

            // Try to export spectral library with no results
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(() => manageResultsDlg.RemoveAllReplicates());
            OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
            WaitForDocumentChangeLoaded(doc);
            var errDlg1 = ShowDialog<MessageDlg>(SkylineWindow.ShowExportSpectralLibraryDialog);
            Assert.AreEqual(Resources.SkylineWindow_ShowExportSpectralLibraryDialog_The_document_must_contain_results_to_export_a_spectral_library_, errDlg1.Message);
            OkDialog(errDlg1, errDlg1.OkDialog);
            RunUI(() => SkylineWindow.Undo());

            // Try to export spectral library with no precursors
            RunUI(() => SkylineWindow.NewDocument());
            var errDlg2 = ShowDialog<MessageDlg>(SkylineWindow.ShowExportSpectralLibraryDialog);
            Assert.AreEqual(Resources.SkylineWindow_ShowExportSpectralLibraryDialog_The_document_must_contain_at_least_one_peptide_precursor_to_export_a_spectral_library_, errDlg2.Message);
            OkDialog(errDlg2, errDlg2.OkDialog);
        }

        private static void CheckRefSpectra(IList<DbRefSpectra> spectra, string peptideSeq, string peptideModSeq, int precursorCharge, double precursorMz, ushort numPeaks)
        {
            for (var i = 0; i < spectra.Count; i++)
            {
                var spectrum = spectra[i];
                if (spectrum.PeptideSeq.Equals(peptideSeq) &&
                    spectrum.PeptideModSeq.Equals(peptideModSeq) &&
                    spectrum.PrecursorCharge.Equals(precursorCharge) &&
                    Math.Abs(spectrum.PrecursorMZ - precursorMz) < 0.001 &&
                    spectrum.NumPeaks.Equals(numPeaks))
                {
                    spectra.RemoveAt(i);
                    return;
                }
            }
            Assert.Fail("{0} [{1}], precursor charge {2}, precursor m/z {3}, with {4} peaks not found", peptideSeq, peptideModSeq, precursorCharge, precursorMz, numPeaks);
        }
    }
}
