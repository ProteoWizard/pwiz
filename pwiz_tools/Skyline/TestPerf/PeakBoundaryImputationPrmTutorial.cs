using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PeakBoundaryImputationPrmTutorial : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakBoundaryImputationPrmTutorial()
        {
            TestFilesZip = @"https://skyline.ms/tutorials/PeakBoundaryImputation-PRM.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("TRX-Pelt-Plate4.sky")));
            WaitForDocumentLoaded();
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(()=>peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction);
                PauseForScreenShot(peptideSettingsUi);
                RunUI(() =>
                {
                    peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.Library.ChangeName(SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Single().Name);
                    peptideSettingsUi.ImputeMissingPeaks = true;
                    peptideSettingsUi.MaxRtShift = 0.2;
                    peptideSettingsUi.MaxPeakWidthVariation = 50;
                });
                PauseForScreenShot(peptideSettingsUi);
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());
        }
    }
}
