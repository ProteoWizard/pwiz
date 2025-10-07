using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MultiInjectCandidatePeakTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMultiInjectCandidatePeak()
        {
            TestFilesZip = @"TestFunctional\MultiInjectCandidatePeakTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiInjectCandidatePeakTest.sky"));
                SkylineWindow.ShowCandidatePeaks();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });

            var candidatePeakForm = FindOpenForm<CandidatePeakForm>();
            Assert.IsNotNull(candidatePeakForm);
            WaitForGraphs();
            var graphChromatogram = SkylineWindow.GetGraphChrom(SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name);
            RunUI(()=>graphChromatogram.SelectedFileIndex = 1);
            WaitForConditionUI(() => candidatePeakForm.IsComplete);
            
            PauseTest();
        }

        private IEnumerable<CandidatePeakGroup> GetCandidatePeaks(CandidatePeakForm candidatePeakForm)
        {
            return candidatePeakForm.DataboundGridControl.BindingListSource.OfType<RowItem>()
                .Select(rowItem => (CandidatePeakGroup)rowItem.Value);
        }
    }
}
