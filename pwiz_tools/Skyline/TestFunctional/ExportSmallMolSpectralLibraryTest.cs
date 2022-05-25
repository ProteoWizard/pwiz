using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ExportSmallMolSpectralLibraryTest : AbstractFunctionalTestEx
    {
        private bool _convertedFromPeptides;
        
        [TestMethod]
        public void TestExportSmallMolSpectralLibrary()
        {
            TestFilesZip = @"TestFunctional\ExportSpectralLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            CheckHighEnergyOffsetOutput(); // Verify the fix for Kaylie's issue of HE IM offsets not being output
            CheckConvertedSmallMolDocumentOutput();
        }

        private SrmDocument ExportTestLib(string docName, string libName, bool convertFromPeptides, out IList<DbRefSpectra> refSpectra)
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(docName)));
            var docOrig = WaitForDocumentLoaded();
            var doc = docOrig;
            _convertedFromPeptides = convertFromPeptides;
            if (_convertedFromPeptides)
            {
                var refine = new RefinementSettings();
                doc = refine.ConvertToSmallMolecules(docOrig, TestFilesDirs[0].FullPath, addAnnotations: false);
                SkylineWindow.SetDocument(doc, docOrig);
            }
            var exported = TestFilesDir.GetTestPath(libName);
            var libraryExporter = new SpectralLibraryExporter(SkylineWindow.Document, SkylineWindow.DocumentFilePath);
            libraryExporter.ExportSpectralLibrary(exported, null);
            Assert.IsTrue(File.Exists(exported));
            refSpectra = null;
            using (var connection = new SQLiteConnection(string.Format("Data Source='{0}';Version=3", exported)))
            {
                connection.Open();
                refSpectra = GetRefSpectra(connection);
            }

            return doc;
        }

        private void CheckConvertedSmallMolDocumentOutput()
        {
            // Export and check spectral library
            var doc = ExportTestLib("msstatstest.sky", "exportSM.blib", true, out var refSpectra);

            CheckRefSpectra(refSpectra, "APVPTGEVYFADSFDR", "C81H115N19O26", "[M+2H]", 885.9203087025, 4, new[]{"y7", "y6", "y5", "y13"});
            CheckRefSpectra(refSpectra, "APVPTGEVYFADSFDR", "C81H115N19O26", "[M6C134N15+2H]", 890.9244430127, 4);
            CheckRefSpectra(refSpectra, "AVTELNEPLSNEDR", "C65H107N19O27", "[M+2H]", 793.8864658775, 4);
            CheckRefSpectra(refSpectra, "AVTELNEPLSNEDR", "C65H107N19O27", "[M6C134N15+2H]", 798.8906001877, 4);
            CheckRefSpectra(refSpectra, "DQGGELLSLR", "C45H78N14O17", "[M+2H]", 544.29074472, 4);
            CheckRefSpectra(refSpectra, "DQGGELLSLR", "C45H78N14O17", "[M6C134N15+2H]", 549.2948790302, 4, new[] { "y8", "y5", "y4", "y3" });
            CheckRefSpectra(refSpectra, "ELLTTMGDR", "C42H74N12O16S", "[M+2H]", 518.260598685, 4);
            CheckRefSpectra(refSpectra, "ELLTTMGDR", "C42H74N12O16S", "[M6C134N15+2H]", 523.2647329952, 4);
            CheckRefSpectra(refSpectra, "FEELNADLFR", "C57H84N14O18", "[M+2H]", 627.31167714, 3);
            CheckRefSpectra(refSpectra, "FEELNADLFR", "C57H84N14O18", "[M6C134N15+2H]", 632.3158114502, 4);
            CheckRefSpectra(refSpectra, "FHQLDIDDLQSIR", "C70H110N20O23", "[M+2H]", 800.40991117, 3);
            CheckRefSpectra(refSpectra, "FHQLDIDDLQSIR", "C70H110N20O23", "[M6C134N15+2H]", 805.4140454802, 4);
            CheckRefSpectra(refSpectra, "FLIPNASQAESK", "C58H93N15O19", "[M+2H]", 652.8458841125, 4);
            CheckRefSpectra(refSpectra, "FLIPNASQAESK", "C58H93N15O19", "[M6C132N15+2H]", 656.8529835243, 4);
            CheckRefSpectra(refSpectra, "FTPGTFTNQIQAAFR", "C78H115N21O22", "[M+2H]", 849.9335534425, 4);
            CheckRefSpectra(refSpectra, "FTPGTFTNQIQAAFR", "C78H115N21O22", "[M6C134N15+2H]", 854.9376877527, 4);
            CheckRefSpectra(refSpectra, "ILTFDQLALDSPK", "C67H109N15O21", "[M+2H]", 730.9033990225, 4);
            CheckRefSpectra(refSpectra, "ILTFDQLALDSPK", "C67H109N15O21", "[M6C132N15+2H]", 734.9104984343, 4);
            CheckRefSpectra(refSpectra, "LSSEMNTSTVNSAR", "C58H101N19O25S", "[M+2H]", 748.8541114925, 4);
            CheckRefSpectra(refSpectra, "LSSEMNTSTVNSAR", "C58H101N19O25S", "[M6C134N15+2H]", 753.8582458027, 4);
            CheckRefSpectra(refSpectra, "NIVEAAAVR", "C40H71N13O13", "[M+2H]", 471.7719908375, 4);
            CheckRefSpectra(refSpectra, "NIVEAAAVR", "C40H71N13O13", "[M6C134N15+2H]", 476.7761251477, 4);
            CheckRefSpectra(refSpectra, "NLQYYDISAK", "C55H83N13O18", "[M+2H]", 607.8062276225, 4);
            CheckRefSpectra(refSpectra, "NLQYYDISAK", "C55H83N13O18", "[M6C132N15+2H]", 611.8133270343, 4);
            CheckRefSpectra(refSpectra, "TSAALSTVGSAISR", "C54H97N17O21", "[M+2H]", 660.8595228125, 4);
            CheckRefSpectra(refSpectra, "TSAALSTVGSAISR", "C54H97N17O21", "[M6C134N15+2H]", 665.8636571227, 4);
            CheckRefSpectra(refSpectra, "VHIEIGPDGR", "C47H77N15O15", "[M+2H]", 546.7934545725, 4);
            CheckRefSpectra(refSpectra, "VHIEIGPDGR", "C47H77N15O15", "[M6C134N15+2H]", 551.7975888827, 4);
            CheckRefSpectra(refSpectra, "VLTPELYAELR", "C60H98N14O18", "[M+2H]", 652.366452385, 4);
            CheckRefSpectra(refSpectra, "VLTPELYAELR", "C60H98N14O18", "[M6C134N15+2H]", 657.3705866952, 4);
            CheckRefSpectra(refSpectra, "VNLAELFK", "C44H72N10O12", "[M+2H]", 467.27383504, 4);
            CheckRefSpectra(refSpectra, "VNLAELFK", "C44H72N10O12", "[M6C132N15+2H]", 471.2809344518, 4);
            CheckRefSpectra(refSpectra, "VPDFSEYR", "C46H65N11O15", "[M+2H]", 506.7403563625, 4);
            CheckRefSpectra(refSpectra, "VPDFSEYR", "C46H65N11O15", "[M6C134N15+2H]", 511.7444906727, 4);
            CheckRefSpectra(refSpectra, "VPDGMVGFIIGR", "C57H93N15O15S", "[M+2H]", 630.8420902025, 4);
            CheckRefSpectra(refSpectra, "VPDGMVGFIIGR", "C57H93N15O15S", "[M6C134N15+2H]", 635.8462245127, 4);
            CheckRefSpectra(refSpectra, "ADVTPADFSEWSK", "C65H93N15O23", "[M+2H]", 726.8357133725, 3);
            CheckRefSpectra(refSpectra, "DGLDAASYYAPVR", "C62H92N16O21", "[M+2H]", 699.338423225, 3);
            CheckRefSpectra(refSpectra, "GAGSSEPVTGLDAK", "C53H89N15O22", "[M+2H]", 644.8226059875, 3);
            CheckRefSpectra(refSpectra, "GTFIIDPAAVIR", "C59H97N15O16", "[M+2H]", 636.8691622375, 3);
            CheckRefSpectra(refSpectra, "GTFIIDPGGVIR", "C57H93N15O16", "[M+2H]", 622.8535121675, 3);
            CheckRefSpectra(refSpectra, "LFLQFGAQGSPFLK", "C76H113N17O18", "[M+2H]", 776.9297511475, 3);
            CheckRefSpectra(refSpectra, "LGGNEQVTR", "C39H68N14O15", "[M+2H]", 487.256704915, 3);
            CheckRefSpectra(refSpectra, "TPVISGGPYEYR", "C61H91N15O19", "[M+2H]", 669.8380590775, 3);
            CheckRefSpectra(refSpectra, "TPVITGAPYEYR", "C63H95N15O19", "[M+2H]", 683.8537091475, 3);
            CheckRefSpectra(refSpectra, "VEATFGVDESNAK", "C58H91N15O23", "[M+2H]", 683.8278883375, 3);
            CheckRefSpectra(refSpectra, "YILAGVENSK", "C49H80N12O16", "[M+2H]", 547.29803844, 3);
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

        // Verify the fix for Kaylie's issue of HE IM offsets not being output
        // Also verify fix for writing spectral libraries for molecules defined by mass only (first item in document is mass-only)
        private void CheckHighEnergyOffsetOutput()
        {
            ExportTestLib("Original.sky", "exportIM.blib", false, out var refSpectra);
            var mzValues = new Dictionary<string, double>(){ {"PE(18:0_18:1)", 744.55487984}, {"PE(12:0_14:0)", 606.41402921}, {"PE(16:1_18:3)", 710.47662949 }};

            var fragmentNamesFA = new[] { "FA 18:0(+O)", "FA 18:1(+O)", "HG(PE,196)" };
            CheckRefSpectra(refSpectra, "PE(18:0_18:1)", string.Empty, "[M-H]", mzValues["PE(18:0_18:1)"], 3,
                fragmentNamesFA, 35.3074607849121, -0.5);
            CheckRefSpectra(refSpectra, "PE(12:0_14:0)", "C31H62NO8P", "[M-H]", mzValues["PE(12:0_14:0)"], 3,
                new[] { "FA 12:0(+O)", "FA 14:0(+O)", "HG(PE,196)" }, 31.3844776153564, -0.5);
            CheckRefSpectra(refSpectra, "PE(16:1_18:3)", "C39H70NO8P", "[M-H]", mzValues["PE(16:1_18:3)"], 3,
                new[] { "FA 16:1(+O)", "FA 18:3(+O)", "HG(PE,196)" }, 33.8052673339844, -0.5);

            // Now create a document based on the library contents
            var docAfter = NewDocumentFromSpectralLibrary("exportIM", TestFilesDir.GetTestPath("exportIM.blib"));
            foreach (var pair in mzValues)
            {
                AssertEx.IsTrue(docAfter.MoleculeTransitionGroups.Contains(m =>
                    m.CustomMolecule.Name.Equals(pair.Key) && Math.Abs(pair.Value - m.PrecursorMz) < .0001));
            }

            foreach (var fragmentName in fragmentNamesFA)
            {
                AssertEx.IsTrue(docAfter.MoleculeTransitions.Contains(m =>
                    m.Transition.Group.Peptide.CustomMolecule.Name.Equals("PE(18:0_18:1)") &&
                    string.IsNullOrEmpty(m.Transition.Group.Peptide.CustomMolecule.Formula) &&
                    Equals(m.Transition.FragmentIonName, fragmentName)));
            }
            AssertEx.IsTrue(docAfter.MoleculeTransitions.Contains(m =>
                m.Transition.Group.Peptide.CustomMolecule.Name.Equals("PE(12:0_14:0)") &&
                Equals(m.Transition.Group.Peptide.CustomMolecule.Formula, "C31H62NO8P")));
        }

        private void CheckRefSpectra(IList<DbRefSpectra> spectra, string name, string formula, string precursorAdduct, 
            double precursorMz, ushort numPeaks, string[] fragmentNames = null, 
            double? ionMobility = null, double? ionMobilityHighEnergyOffset = null)
        {
            if (_convertedFromPeptides)
            {
                name = RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator + name;
            }
            for (var i = 0; i < spectra.Count; i++)
            {
                var spectrum = spectra[i];
                if (spectrum.MoleculeName.Equals(name) &&
                    spectrum.ChemicalFormula.Equals(formula) &&
                    spectrum.PrecursorCharge.Equals(Adduct.FromStringAssumeProtonated(precursorAdduct).AdductCharge) &&
                    spectrum.PrecursorAdduct.Equals(precursorAdduct) &&
                    Math.Abs(spectrum.PrecursorMZ - precursorMz) < 0.001 &&
                    spectrum.NumPeaks.Equals(numPeaks))
                {
                    // Found the item - check its fragment info if provided
                    // This demonstrates fix for "Issue 689: File > Export > Spectral Library losing information from small molecule documents",
                    // point 3 "Ion annotations do not preserve the ion names from the document but instead always use 'ion[mass]' for the annotations
                    // when the document gave them names."
                    if (fragmentNames != null)
                    {
                        Assume.AreEqual(fragmentNames.Length, spectrum.PeakAnnotations.Count);
                        foreach (var annotation in spectrum.PeakAnnotations)
                        {
                            Assume.IsTrue(fragmentNames.Contains(annotation.Name));
                        }
                    }

                    if (ionMobility.HasValue)
                    {
                        AssertEx.AreEqualNullable(spectrum.IonMobility, ionMobility, .00001);
                        if (ionMobilityHighEnergyOffset.HasValue)
                        {
                            AssertEx.AreEqualNullable(spectrum.IonMobilityHighEnergyOffset, ionMobilityHighEnergyOffset, .00001);
                        }
                    }

                    // Item is OK, remove from further searches
                    spectra.RemoveAt(i);
                    return;
                }
            }
            Assume.Fail(string.Format("{0}, {1}, precursor charge {2}, precursor m/z {3}, with {4} peaks not found", name, formula, precursorAdduct, precursorMz, numPeaks));
        }
    }
}
