using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
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
            var progress = new SilentProgressMonitor();
            new SpectralLibraryExporter(SkylineWindow.Document, SkylineWindow.DocumentFilePath)
                .ExportSpectralLibrary(exported, progress);
            Assert.IsTrue(File.Exists(exported));

            var refSpectra = GetRefSpectra(exported);
            CheckRefSpectraAll(refSpectra);


            // Add an iRT calculator and re-export spectral library
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editIrtCalcDlg = ShowDialog<EditIrtCalcDlg>(peptideSettingsDlg.AddCalculator);
            IList<DbIrtPeptide> irtPeptides = null;
            RunUI(() =>
            {
                editIrtCalcDlg.CalcName = "Biognosys-11";
                editIrtCalcDlg.CreateDatabase(TestFilesDir.GetTestPath("test.irtdb"));
                editIrtCalcDlg.IrtStandards = IrtStandard.BIOGNOSYS_11;
            });
            var addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(editIrtCalcDlg.AddResults);
            var recalibrateDlg = ShowDialog<MultiButtonMsgDlg>(addIrtPeptidesDlg.OkDialog);
            OkDialog(recalibrateDlg, recalibrateDlg.BtnYesClick);
            RunUI(() => irtPeptides = editIrtCalcDlg.AllPeptides.ToList());
            OkDialog(editIrtCalcDlg, editIrtCalcDlg.OkDialog);
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
            doc = WaitForDocumentChange(doc);
            Assert.IsTrue(doc.Settings.HasRTPrediction);
            RunUI(() => SkylineWindow.SaveDocument());
            var exportedWithIrts = TestFilesDir.GetTestPath("export-irts.blib");
            var progress2 = new SilentProgressMonitor();
            new SpectralLibraryExporter(SkylineWindow.Document, SkylineWindow.DocumentFilePath)
                .ExportSpectralLibrary(exportedWithIrts, progress2);

            IList<DbIrtPeptide> irtLibrary;
            using (var connection = new SQLiteConnection(string.Format("Data Source='{0}';Version=3", exportedWithIrts)))
            {
                connection.Open();
                refSpectra = GetRefSpectra(connection);
                irtLibrary = GetIrtLibrary(connection);
            }
            CheckRefSpectraAll(refSpectra);
            foreach (var peptide in irtLibrary)
                CheckIrtPeptide(irtPeptides, peptide.PeptideModSeq, peptide.Irt, peptide.Standard);
            Assert.IsTrue(!irtPeptides.Any());

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


        private static void CheckRefSpectraAll(IList<DbRefSpectra> refSpectra)
        {
            CheckRefSpectra(refSpectra, "APVPTGEVYFADSFDR", "APVPTGEVYFADSFDR", 2, 885.920, 4, 24.366);
            CheckRefSpectra(refSpectra, "APVPTGEVYFADSFDR", "APVPTGEVYFADSFDR[+10.0]", 2, 890.924, 4, 24.532);
            CheckRefSpectra(refSpectra, "AVTELNEPLSNEDR", "AVTELNEPLSNEDR", 2, 793.886, 4, 17.095);
            CheckRefSpectra(refSpectra, "AVTELNEPLSNEDR", "AVTELNEPLSNEDR[+10.0]", 2, 798.891, 4, 17.095);
            CheckRefSpectra(refSpectra, "DQGGELLSLR", "DQGGELLSLR", 2, 544.291, 4, 20.355);
            CheckRefSpectra(refSpectra, "DQGGELLSLR", "DQGGELLSLR[+10.0]", 2, 549.295, 4, 20.311);
            CheckRefSpectra(refSpectra, "ELLTTMGDR", "ELLTTMGDR", 2, 518.261, 4, 16.904);
            CheckRefSpectra(refSpectra, "ELLTTMGDR", "ELLTTMGDR[+10.0]", 2, 523.265, 4, 16.904);
            CheckRefSpectra(refSpectra, "FEELNADLFR", "FEELNADLFR", 2, 627.312, 3, 24.324);
            CheckRefSpectra(refSpectra, "FEELNADLFR", "FEELNADLFR[+10.0]", 2, 632.316, 4, 24.283);
            CheckRefSpectra(refSpectra, "FHQLDIDDLQSIR", "FHQLDIDDLQSIR", 2, 800.410, 3, 22.960);
            CheckRefSpectra(refSpectra, "FHQLDIDDLQSIR", "FHQLDIDDLQSIR[+10.0]", 2, 805.414, 4, 22.919);
            CheckRefSpectra(refSpectra, "FLIPNASQAESK", "FLIPNASQAESK", 2, 652.846, 4, 18.956);
            CheckRefSpectra(refSpectra, "FLIPNASQAESK", "FLIPNASQAESK[+8.0]", 2, 656.853, 4, 19.001);
            CheckRefSpectra(refSpectra, "FTPGTFTNQIQAAFR", "FTPGTFTNQIQAAFR", 2, 849.934, 4, 26.833);
            CheckRefSpectra(refSpectra, "FTPGTFTNQIQAAFR", "FTPGTFTNQIQAAFR[+10.0]", 2, 854.938, 4, 26.833);
            CheckRefSpectra(refSpectra, "ILTFDQLALDSPK", "ILTFDQLALDSPK", 2, 730.903, 4, 25.962);
            CheckRefSpectra(refSpectra, "ILTFDQLALDSPK", "ILTFDQLALDSPK[+8.0]", 2, 734.910, 4, 25.962);
            CheckRefSpectra(refSpectra, "LSSEMNTSTVNSAR", "LSSEMNTSTVNSAR", 2, 748.854, 4, 14.068);
            CheckRefSpectra(refSpectra, "LSSEMNTSTVNSAR", "LSSEMNTSTVNSAR[+10.0]", 2, 753.858, 4, 14.068);
            CheckRefSpectra(refSpectra, "NIVEAAAVR", "NIVEAAAVR", 2, 471.772, 4, 15.444);
            CheckRefSpectra(refSpectra, "NIVEAAAVR", "NIVEAAAVR[+10.0]", 2, 476.776, 4, 15.444);
            CheckRefSpectra(refSpectra, "NLQYYDISAK", "NLQYYDISAK", 2, 607.806, 4, 18.204);
            CheckRefSpectra(refSpectra, "NLQYYDISAK", "NLQYYDISAK[+8.0]", 2, 611.813, 4, 18.204);
            CheckRefSpectra(refSpectra, "TSAALSTVGSAISR", "TSAALSTVGSAISR", 2, 660.860, 4, 18.403);
            CheckRefSpectra(refSpectra, "TSAALSTVGSAISR", "TSAALSTVGSAISR[+10.0]", 2, 665.864, 4, 18.403);
            CheckRefSpectra(refSpectra, "VHIEIGPDGR", "VHIEIGPDGR", 2, 546.793, 4, 15.672);
            CheckRefSpectra(refSpectra, "VHIEIGPDGR", "VHIEIGPDGR[+10.0]", 2, 551.798, 4, 15.672);
            CheckRefSpectra(refSpectra, "VLTPELYAELR", "VLTPELYAELR", 2, 652.366, 4, 23.642);
            CheckRefSpectra(refSpectra, "VLTPELYAELR", "VLTPELYAELR[+10.0]", 2, 657.371, 4, 23.683);
            CheckRefSpectra(refSpectra, "VNLAELFK", "VNLAELFK", 2, 467.274, 4, 25.787);
            CheckRefSpectra(refSpectra, "VNLAELFK", "VNLAELFK[+8.0]", 2, 471.281, 4, 25.829);
            CheckRefSpectra(refSpectra, "VPDFSEYR", "VPDFSEYR", 2, 506.740, 4, 17.826);
            CheckRefSpectra(refSpectra, "VPDFSEYR", "VPDFSEYR[+10.0]", 2, 511.744, 4, 17.826);
            CheckRefSpectra(refSpectra, "VPDGMVGFIIGR", "VPDGMVGFIIGR", 2, 630.842, 4, 26.775);
            CheckRefSpectra(refSpectra, "VPDGMVGFIIGR", "VPDGMVGFIIGR[+10.0]", 2, 635.846, 4, 26.734);
            CheckRefSpectra(refSpectra, "ADVTPADFSEWSK", "ADVTPADFSEWSK", 2, 726.836, 3, 22.100);
            CheckRefSpectra(refSpectra, "DGLDAASYYAPVR", "DGLDAASYYAPVR", 2, 699.338, 3, 20.500);
            CheckRefSpectra(refSpectra, "GAGSSEPVTGLDAK", "GAGSSEPVTGLDAK", 2, 644.823, 3, 14.563);
            CheckRefSpectra(refSpectra, "GTFIIDPAAVIR", "GTFIIDPAAVIR", 2, 636.869, 3, 27.025);
            CheckRefSpectra(refSpectra, "GTFIIDPGGVIR", "GTFIIDPGGVIR", 2, 622.854, 3, 24.701);
            CheckRefSpectra(refSpectra, "LFLQFGAQGSPFLK", "LFLQFGAQGSPFLK", 2, 776.930, 3, 28.778);
            CheckRefSpectra(refSpectra, "LGGNEQVTR", "LGGNEQVTR", 2, 487.257, 3, 10.326);
            CheckRefSpectra(refSpectra, "TPVISGGPYEYR", "TPVISGGPYEYR", 2, 669.838, 3, 18.156);
            CheckRefSpectra(refSpectra, "TPVITGAPYEYR", "TPVITGAPYEYR", 2, 683.854, 3, 18.854);
            CheckRefSpectra(refSpectra, "VEATFGVDESNAK", "VEATFGVDESNAK", 2, 683.828, 3, 16.021);
            CheckRefSpectra(refSpectra, "YILAGVENSK", "YILAGVENSK", 2, 547.298, 3, 17.118);
            Assert.IsTrue(!refSpectra.Any());
        }


        private static IList<DbIrtPeptide> GetIrtLibrary(SQLiteConnection connection)
        {
            var list = new List<DbIrtPeptide>();
            using (var select = new SQLiteCommand(connection) { CommandText = "SELECT * FROM IrtLibrary" })
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new DbIrtPeptide(
                        new Target(reader["PeptideModSeq"].ToString()),
                        double.Parse(reader["Irt"].ToString()),
                        bool.Parse(reader["Standard"].ToString()),
                        int.Parse(reader["TimeSource"].ToString())));
                }
            }
            return list;
        }

        private static void CheckIrtPeptide(IList<DbIrtPeptide> peptides, string peptideModSeq, double irt, bool standard)
        {
            for (var i = 0; i < peptides.Count; i++)
            {
                var peptide = peptides[i];
                if (peptide.PeptideModSeq.Equals(peptideModSeq) &&
                    Math.Abs(peptide.Irt - irt) < 0.001 &&
                    peptide.Standard == standard)
                {
                    peptides.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
