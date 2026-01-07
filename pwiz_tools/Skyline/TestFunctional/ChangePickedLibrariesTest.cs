using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ChangePickedLibrariesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChangePickedLibraries()
        {
            TestFilesZip = @"TestFunctional\ChangePickedLibrariesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            WaitForDocumentLoaded();
            Assert.IsNotNull(FindOpenForm<GraphSpectrum>());
            var libraryNames = new[] { "Rat (NIST) (Rat_plasma2)", "Rat (GPM) (Rat_plasma2)", "Rat_Prosit" };
            for (int i = 0; i < 5; i++)
            {
                RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
                {
                    peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
                    peptideSettingsUi.PickedLibraries = libraryNames.Take(2).ToArray();
                    peptideSettingsUi.OkDialog();
                });
                RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
                {
                    peptideSettingsUi.PickedLibraries = libraryNames;
                    peptideSettingsUi.OkDialog();
                });
            }
        }
    }
}
