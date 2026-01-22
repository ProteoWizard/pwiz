using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportSpectrumFilterTransitionListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportSpectrumFilterTransitionList()
        {
            TestFilesZipPaths = new []
            {
                @"TestFunctional\ImportSpectrumFilterTransitionListTest.data",
                @"TestFunctional\crv_qf_hsp_ms2_opt0.zip"

            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("BlankDocument.sky")));
            RunDlg<ImportTransitionListColumnSelectDlg>(
                () => SkylineWindow.ImportMassList(TestFilesDirs[0].GetTestPath("TransitionList.csv")),
                dlg =>
                {
                    dlg.OkDialog();
                });
            var rawFileName = TestFilesDirs[1].GetTestPath("crv_qf_hsp_ms2_opt0.raw");
            ImportResultsFile(rawFileName);
            var blankDocumentLoaded = SkylineWindow.Document;
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("WithTransitions.sky")));
            ImportResultsFile(rawFileName);
            var otherDocument = SkylineWindow.Document;
            var missingChromatograms =
                FindMissingChromatograms(blankDocumentLoaded, otherDocument, new MsDataFilePath(rawFileName)).ToList();
            var message = TextUtil.LineSeparate(missingChromatograms.Select(tuple =>
                TextUtil.SpaceSeparate(tuple.Item1.ModifiedSequence + tuple.Item2.SpectrumClassFilter)));
            Assert.AreEqual(0, missingChromatograms.Count, message);
        }

        private IEnumerable<(PeptideDocNode, TransitionGroupDocNode)> FindMissingChromatograms(SrmDocument expected, SrmDocument actual, MsDataFileUri filePath)
        {
            var chromSetFileMatchExpected = expected.MeasuredResults.FindMatchingMSDataFile(filePath);
            var chromSetFileMatchActual = actual.MeasuredResults.FindMatchingMSDataFile(filePath);
            float tolerance = (float) expected.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var actualMolecules = actual.Molecules.ToLookup(molecule => molecule.ModifiedSequence);
            foreach (var expectedMolecule in expected.Molecules)
            {
                foreach (var expectedTransitionGroup in expectedMolecule.TransitionGroups)
                {
                    Assert.IsTrue(FindMatchingTransitionGroup(expectedTransitionGroup, actualMolecules[expectedMolecule.ModifiedSequence], out var actualMolecule, out var actualTransitionGroup));
                    if (!expected.MeasuredResults.TryLoadChromatogram(
                            chromSetFileMatchExpected.Chromatograms, expectedMolecule,
                            expectedTransitionGroup, tolerance, out var expectedChromatograms) ||
                        expectedChromatograms.Length == 0)
                    {
                        continue;
                    }

                    if (!actual.MeasuredResults.TryLoadChromatogram(chromSetFileMatchActual.Chromatograms,
                            actualMolecule, actualTransitionGroup, tolerance, out var actualChromatograms) || actualChromatograms.Length == 0)
                    {
                        yield return (actualMolecule, actualTransitionGroup);
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
