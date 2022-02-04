using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CandidatePeakTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCandidatePeaks()
        {
            TestFilesZip = @"TestFunctional\CandidatePeakTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CandidatePeakTest.sky"));
                SkylineWindow.ShowCandidatePeaks();
            });
            SelectPeptide("SLDLIESLLR");
            PauseTest();
        }

        private void SelectPeptide(string sequence)
        {
            foreach (var moleculeGroup in SkylineWindow.Document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    if (sequence == molecule.Target.ToString())
                    {
                        RunUI(()=>SkylineWindow.SelectedPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide));
                        return;
                    }
                }
            }
            Assert.Fail("Unable to find peptide sequence {0}", sequence);
        }
    }
}
