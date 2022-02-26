using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
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
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CandidatePeakTestTargetsOnly.sky"));
                SkylineWindow.ShowCandidatePeaks();
            });
            SelectPeptide("SLDLIESLLR");
            WaitForGraphs();
            var candidatePeakForm = FindOpenForm<CandidatePeakForm>();
            Assert.IsNotNull(candidatePeakForm);
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            RunUI(() =>
            {

            });
        }

        // private void VerifyFeatureScores()
        // {
        //     var candidatePeakForm = FindOpenForm<CandidatePeakForm>();
        //     WaitForConditionUI(() => candidatePeakForm.IsComplete);
        //     PropertyPath ppWeightedFeature = PropertyPath.Root
        //         .Property(nameof(CandidatePeakGroup.PeakScores))
        //         .Property(nameof(PeakGroupScore.WeightedFeatures))
        //         .DictionaryValues();
        //         
        //     var colIntensity = candidatePeakForm.DataboundGridControl.FindColumn()
        //         
        // }

        // private DataGridViewColumn FindColumn(PropertyPath propertyPath, )

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
