/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SmallMoleculeIrtTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSmallMoleculeIrt()
        {
            TestFilesZip = @"TestFunctional\SmallMoleculeIrtTest.zip";
            RunFunctionalTest();
        }
        protected override void DoTest()
        {
            TestPasteIRTs();
            TestResultsIRTs();
        }

        void TestPasteIRTs()
        {
            // Check irt paste of small molecule columns (target name, iRT, target formula, CAS)
            var txt =
                "bar\t2\t2\tC5H9S\t12-34-56\n" + // N.B. These are nonsense values
                "foo\t1\t1\tC12H19\t54-12-11\n" +
                "foo2\t21\t21\tC12H29\t54-12-12\n" +
                "foo3\t31\t31\tC12H39\t54-12-13\n" +
                "foo4\t41\t41\tC12H49\t54-12-14\n" +
                "foo5\t51\t51\tC12H59\t54-12-15\n" +
                "foo6\t61\t61\tC12H69\t54-12-16\n" +
                "baz\t14\t14\tC12H6\n" +
                "PEPTIDER\t77\t77\n" +
                "binng\t3\t3\tC23H5S2";
            RunUI(() => SkylineWindow.NewDocument(true));
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(() => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
            var irtCalc = ShowDialog<EditIrtCalcDlg>(peptideSettingsUi.AddCalculator);
            var calibrateDlg = ShowDialog<CalibrateIrtDlg>(irtCalc.Calibrate);
            SetClipboardTextUI(txt);
            RunUI(() => calibrateDlg.PasteCalibration());
            AssertEx.AreEqual(10, calibrateDlg.StandardPeptideCount);
            string minFixedPointName = null, maxFixedPointName = null;
            RunUI(() =>
            {
                minFixedPointName = calibrateDlg.MinFixedPointName;
                maxFixedPointName = calibrateDlg.MaxFixedPointName;
                calibrateDlg.StandardName = "test_mix";
            });
            AssertEx.AreEqual("foo", minFixedPointName);
            AssertEx.AreEqual("PEPTIDER", maxFixedPointName);
            OkDialog(calibrateDlg, calibrateDlg.OkDialog);
            RunUI(() =>
            {
                irtCalc.CalcName = "Nonsense";
                irtCalc.CreateDatabase(TestFilesDir.GetTestPath("Nonsense.irtdb"));
            });
// PauseTest(); // Uncomment for a convenient informal demo stopping point
            OkDialog(irtCalc, irtCalc.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            // Did everything we pasted in wind up in the irtDB?
            RunUI(() =>
            {
                var doc = SkylineWindow.DocumentUI;
                var calculator = doc.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
                Assert.IsNotNull(calculator);
                var peptideSeqs = calculator.PeptideScores.Select(item => item.Key).ToList();
                Assert.AreEqual(10, calculator.GetStandardPeptides(peptideSeqs).Count());
                var lines = txt.Split('\n').ToArray();
                foreach (var line in lines)
                {
                    var args = line.Split('\t').Select(col => col.Trim()).ToArray();
                    var molecule = args.Length == 3
                        ? new Target(args[0])
                        : new Target(SmallMoleculeLibraryAttributes.Create(args[0], args[3], null,
                            args.Length < 5 ? null : string.Format("CAS:{0}", args[4])));
                    AssertEx.IsTrue(peptideSeqs.Contains(molecule));
                }
            });

            // Clean up

            RunUI(() => SkylineWindow.NewDocument(true));
        }
        private void TestResultsIRTs()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DextranLadder.sky")));
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(()=>SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
            var irtCalc = ShowDialog<EditIrtCalcDlg>(peptideSettingsUi.AddCalculator);
            var calibrateDlg = ShowDialog<CalibrateIrtDlg>(irtCalc.Calibrate);

            // First, populate the iRT database with any set of standards from the results
            var addStandardsDlg = ShowDialog<AddIrtStandardsDlg>(calibrateDlg.UseResults);
            RunUI(() => addStandardsDlg.StandardCount = 10);
            OkDialog(addStandardsDlg, addStandardsDlg.OkDialog);
            RunUI(() => calibrateDlg.StandardName = "Document");
            OkDialog(calibrateDlg, calibrateDlg.OkDialog);
            var addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(irtCalc.AddResults);
            OkDialog(addIrtPeptidesDlg, addIrtPeptidesDlg.OkDialog);
            var alertDlg = WaitForOpenForm<AlertDlg>();
            OkDialog(alertDlg, alertDlg.ClickYes);

            // Now, change the set of standards to the ones that we want to use.
            var changeIrtPeptidesDlg = ShowDialog<ChangeIrtPeptidesDlg>(irtCalc.ChangeStandardPeptides);
            RunUI(() =>
                {
                    changeIrtPeptidesDlg.PeptidesText =
                        "(Glc)3\r\n(Glc)4\r\n(Glc)5\r\n(Glc)6\r\n(Glc)7\r\n(Glc)8\r\n(Glc)9\r\n(Glc)10\r\n";
                });
            OkDialog(changeIrtPeptidesDlg, changeIrtPeptidesDlg.OkDialog);
            RunUI(()=>
            {
                irtCalc.CalcName = "DextranLadderIrt";
                irtCalc.CreateDatabase(TestFilesDir.GetTestPath("DextranLadder.irtdb"));
            });
            var confirmStandardCountDlg = ShowDialog<MultiButtonMsgDlg>(irtCalc.OkDialog);
            OkDialog(confirmStandardCountDlg, confirmStandardCountDlg.ClickYes);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            RunUI(()=>SkylineWindow.SaveDocument());
        }
    }
}
