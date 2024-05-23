using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RescoreInPlaceTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRescoreInPlace()
        {
            TestFilesZip = @"TestFunctional\RescoreInPlaceTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ThreeReplicates.sky")));
            WaitForDocumentLoaded();
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg =>
            {
                dlg.Rescore(false);
            });
            var delay = 0;
            while (!SkylineWindow.Document.IsLoaded)
            {
                bool transitionSettingsUiClosed = false;
                var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(() =>
                {
                    SkylineWindow.ShowTransitionSettingsUI();
                    transitionSettingsUiClosed = true;
                });
                RunUI(() =>
                {
                    transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                    var newTolerance =
                        SkylineWindow.Document.Settings.TransitionSettings.Instrument.MzMatchTolerance == 0.055
                            ? 0.056
                            : 0.055;

                    transitionSettingsUi.MZMatchTolerance = newTolerance;
                });
                while (!transitionSettingsUiClosed)
                {
                    RunUI(()=>transitionSettingsUi.OkDialog());
                    WaitForConditionUI(() => transitionSettingsUiClosed || FindOpenForm<AlertDlg>() != null);
                    var alertDlg = FindOpenForm<AlertDlg>();
                    if (alertDlg != null)
                    {
                        Assert.IsFalse(transitionSettingsUiClosed,
                            "Unexpected alert found after TransitionSettingsUi closed: {0}",
                            TextUtil.LineSeparate(alertDlg.Message, alertDlg.DetailMessage));
                        OkDialog(alertDlg, alertDlg.OkDialog);
                    }
                }
                Thread.Sleep(delay);
                delay += 1000;
            }
        }
    }
}
