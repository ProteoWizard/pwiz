using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportSpectrumFilterTransitionListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportSpectrumFilterTransitionList()
        {
            TestFilesZip = @"TestFunctional\ImportSpectrumFilterTransitionListTest.data";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("BlankDocument.sky")));
            RunDlg<ImportTransitionListColumnSelectDlg>(
                () => SkylineWindow.ImportMassList(TestFilesDir.GetTestPath("TransitionList.csv")),
                dlg =>
                {
                    dlg.OkDialog();
                });
            var rawFileNames = Enumerable.Range(0, 5)
                .Select(i => new MsDataFilePath(TestFilesDir.GetTestPath("crv_qf_hsp_ms2_opt" + i + ".raw"))).ToList();
            ImportResultsFiles(rawFileNames);
            var blankDocumentLoaded = SkylineWindow.Document;
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("WithTransitions.sky")));
            ImportResultsFiles(rawFileNames);
            var otherDocument = SkylineWindow.Document;
            VerifyHasAllChromatograms(blankDocumentLoaded, otherDocument);
        }

        private void VerifyHasAllChromatograms(SrmDocument expected, SrmDocument actual)
        {
            CollectionAssert.AreEqual(expected.MeasuredResults.CachedFilePaths.ToList(),
                actual.MeasuredResults.CachedFilePaths.ToList());
            float tolerance = (float) expected.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var actualMolecules = actual.Molecules.ToLookup(molecule => molecule.ModifiedSequence);
            foreach (var expectedMolecule in expected.Molecules)
            {
                foreach (var expectedTransitionGroup in expectedMolecule.TransitionGroups)
                {
                    Assert.IsTrue(FindMatchingTransitionGroup(expectedTransitionGroup, actualMolecules[expectedMolecule.ModifiedSequence], out var actualMolecule, out var actualTransitionGroup));
                    for (int replicateIndex = 0;
                         replicateIndex < expected.MeasuredResults.Chromatograms.Count;
                         replicateIndex++)
                    {
                        var message =
                            $"{expectedMolecule.ModifiedSequence} {expectedTransitionGroup.SpectrumClassFilter} {expected.MeasuredResults.Chromatograms[replicateIndex].Name}";
                        if (!expected.MeasuredResults.TryLoadChromatogram(
                                expected.MeasuredResults.Chromatograms[replicateIndex], expectedMolecule,
                                expectedTransitionGroup, tolerance, out var expectedChromatograms) ||
                            expectedChromatograms.Length == 0)
                        {
                            continue;
                        }
                        Assert.IsTrue(actual.MeasuredResults.TryLoadChromatogram(actual.MeasuredResults.Chromatograms[replicateIndex], actualMolecule, actualTransitionGroup, tolerance, out var actualChromatograms), message);
                        Assert.AreEqual(expectedChromatograms.Length, actualChromatograms.Length, message);
                    }
                }
            }
        }

        private bool FindMatchingTransitionGroup(TransitionGroupDocNode transitionGroupDocNode,
            IEnumerable<PeptideDocNode> candidateMolecules, out PeptideDocNode matchingMolecule,
            out TransitionGroupDocNode matchingTransitionGroup)
        {
            foreach (var candidateMolecule in candidateMolecules)
            {
                foreach (var candidateTransitionGroup in candidateMolecule.TransitionGroups)
                {
                    if (Equals(candidateTransitionGroup.SpectrumClassFilter,
                            transitionGroupDocNode.SpectrumClassFilter))
                    {
                        matchingMolecule = candidateMolecule;
                        matchingTransitionGroup = candidateTransitionGroup;
                        return true;
                    }
                }
            }
            matchingMolecule = null;
            matchingTransitionGroup = null;
            return false;
        }
    }
}
