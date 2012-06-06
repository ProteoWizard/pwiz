/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests ExportMethodDlg 
    /// </summary>
    [TestClass]
    public class ExportMethodDlgTest : AbstractFunctionalTest
    {

        [TestMethod]
        public void TestExportMethodDlg()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {

            CreateDummyRTRegression();

            ThermoTsqTest();

            ThermoLtqTest();

            AbiQtrapTest();

            AbiTofTest();
        }

        private static void ThermoTsqTest()
        {

            DeselectDummyRTRegression();
            DisableMS1Filtering();
            DisableMS2Filtering();


            RunDlg<ExportMethodDlg>(
                (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method)),
                exportMethodDlg =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO_TSQ;
                        Assert.AreEqual(ExportMethodDlg.TRANS_PER_SAMPLE_INJ_TXT, exportMethodDlg.GetMaxLabelText);
                        Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                        Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                        //Assert.IsTrue(exportMethodDlg.IsRunLengthVisible);
                        Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);

                        exportMethodDlg.CancelButton.PerformClick();
                    }
            );


            // Select the dummy RT regression so that we can select the "Scheduled" method type
            SelectDummyRTRegression();

            RunDlg<ExportMethodDlg>(
                (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method)),
                exportMethodDlg =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO_TSQ;

                    Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                    Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                    Assert.AreEqual(ExportMethodDlg.RUN_DURATION_TXT, exportMethodDlg.GetDwellTimeLabel);
                    Assert.IsTrue(exportMethodDlg.IsRunLengthVisible);
                    Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);

                    exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                    Assert.AreEqual(ExportMethodDlg.CONCUR_TRANS_TXT, exportMethodDlg.GetMaxLabelText);

                    Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);
                    Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);

                    exportMethodDlg.CancelButton.PerformClick();
                }
            );

            DeselectDummyRTRegression();

        }

        private static void ThermoLtqTest()
        {

            DeselectDummyRTRegression();
            DisableMS1Filtering();
            DisableMS2Filtering();


            // The "Method type" combo box should be enabled.
            // However, LTQ instruments do not support scheduled targeted MS/MS so we should display 
            // a warning if the user selects "Scheduled" method type.
            var exportMethodDlg1 = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            RunUI(() =>
            {
                exportMethodDlg1.InstrumentType = ExportInstrumentType.THERMO_LTQ;
                Assert.AreEqual(ExportMethodDlg.PREC_PER_SAMPLE_INJ_TXT, exportMethodDlg1.GetMaxLabelText);
                Assert.IsFalse(exportMethodDlg1.IsOptimizeTypeEnabled);
                Assert.IsTrue(exportMethodDlg1.IsTargetTypeEnabled);
                Assert.AreEqual(ExportMethodType.Standard, exportMethodDlg1.MethodType);
                
                Assert.IsFalse(exportMethodDlg1.IsRunLengthVisible);
                Assert.IsFalse(exportMethodDlg1.IsDwellTimeVisible);

            });

            // Select the "Scheduled" method type. This should popup an error message
            RunDlg<MessageDlg>(
                () => exportMethodDlg1.MethodType = ExportMethodType.Scheduled,
                messageDlg =>
                {
                    Assert.AreEqual(ExportMethodDlg.SCHED_NOT_SUPPORTED_ERR_TXT, messageDlg.Message);
                    messageDlg.OkDialog();
                });

            RunUI(() => exportMethodDlg1.CancelButton.PerformClick());
            WaitForClosedForm(exportMethodDlg1);


            // Select the dummy RT regression AND enable MS1 filtering so that we can select the "Scheduled" method type
            SelectDummyRTRegression();
            EnableMS1Filtering();
            DisableMS2Filtering();

            RunDlg<ExportMethodDlg>(
               (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method)),
               exportMethodDlg =>
               {
                   exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO_LTQ;

                   Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                   Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);

                   // change Method type to "Scheduled"
                   exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                   Assert.AreEqual(ExportMethodDlg.CONCUR_PREC_TXT, exportMethodDlg.GetMaxLabelText);

                   // change Method type to "Standard". We should see the "Run Duration" text box
                   exportMethodDlg.MethodType = ExportMethodType.Standard;
                   Assert.AreEqual(ExportMethodDlg.PREC_PER_SAMPLE_INJ_TXT, exportMethodDlg.GetMaxLabelText);
                   Assert.AreEqual(ExportMethodDlg.RUN_DURATION_TXT, exportMethodDlg.GetDwellTimeLabel);
                   Assert.IsTrue(exportMethodDlg.IsRunLengthVisible);
                   Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);

                   exportMethodDlg.CancelButton.PerformClick();
               }
           );


            // Enable MS2 filtering. 
            EnableMS2Filtering();

            var exportMethodDlg2 = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            RunUI(() =>
            {
                exportMethodDlg2.InstrumentType = ExportInstrumentType.THERMO_LTQ;
                Assert.AreEqual(ExportMethodDlg.PREC_PER_SAMPLE_INJ_TXT, exportMethodDlg2.GetMaxLabelText);
                Assert.IsFalse(exportMethodDlg2.IsOptimizeTypeEnabled);
                Assert.IsTrue(exportMethodDlg2.IsTargetTypeEnabled);
                Assert.AreEqual(ExportMethodType.Standard, exportMethodDlg2.MethodType);
                
                Assert.IsFalse(exportMethodDlg2.IsRunLengthVisible);
                Assert.IsFalse(exportMethodDlg2.IsDwellTimeVisible);

            });

            // Select the "Scheduled" method type. This should popup an error message
            // since we are not exporting an inclusion list (reason: both MS1 and MS2 filtering are enabled)
            RunDlg<MessageDlg>(
                () => exportMethodDlg2.MethodType = ExportMethodType.Scheduled,
                messageDlg =>
                {
                    Assert.AreEqual(ExportMethodDlg.SCHED_NOT_SUPPORTED_ERR_TXT, messageDlg.Message);
                    messageDlg.OkDialog();
                });

            RunUI(() => exportMethodDlg2.CancelButton.PerformClick());
            WaitForClosedForm(exportMethodDlg2);

        }

        private static void AbiQtrapTest()
        {
            DeselectDummyRTRegression();
            DisableMS1Filtering();
            DisableMS2Filtering();


            RunDlg<ExportMethodDlg>(
                (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method)),
                exportMethodDlg =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.ABI_QTRAP;
                    Assert.AreEqual(ExportMethodDlg.TRANS_PER_SAMPLE_INJ_TXT, exportMethodDlg.GetMaxLabelText);
                    Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                    Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                    Assert.AreEqual(ExportMethodDlg.DWELL_TIME_TXT, exportMethodDlg.GetDwellTimeLabel);
                    Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);
                    Assert.IsTrue(exportMethodDlg.IsDwellTimeVisible);

                    exportMethodDlg.CancelButton.PerformClick();
                }
            );


            // Select the dummy RT regression so that we can select the "Scheduled" method type
            SelectDummyRTRegression();

            RunDlg<ExportMethodDlg>(
                (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method)),
                exportMethodDlg =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.ABI_QTRAP;

                    Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                    Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);

                    exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                    Assert.AreEqual(ExportMethodDlg.CONCUR_TRANS_TXT, exportMethodDlg.GetMaxLabelText);

                    Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);
                    Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);

                    exportMethodDlg.CancelButton.PerformClick();
                }
            );

            DeselectDummyRTRegression();
        }

        private static void AbiTofTest()
        {

            DeselectDummyRTRegression();
            DisableMS1Filtering();
            DisableMS2Filtering();
 
            // The "Method type" combo box should be enabled.
            // However, ABI TOF instruments do not support scheduled targeted MS/MS so we should display 
            // a warning if the user selects "Scheduled" method type.
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method), exportMethodDlg1 =>
            {
                exportMethodDlg1.InstrumentType = ExportInstrumentType.ABI_TOF;
                Assert.AreEqual(ExportMethodDlg.PREC_PER_SAMPLE_INJ_TXT, exportMethodDlg1.GetMaxLabelText);
                Assert.IsFalse(exportMethodDlg1.IsOptimizeTypeEnabled);
                Assert.IsTrue(exportMethodDlg1.IsTargetTypeEnabled);

                // The dwell time field should not be visible.
                // Targeted MS/MS method template for ABI TOF instruments must have a MS/MS scan.
                // Accumulation (dwell) time specified in the template is always used.
                Assert.IsFalse(exportMethodDlg1.IsRunLengthVisible);
                Assert.IsFalse(exportMethodDlg1.IsDwellTimeVisible);

                // Should no longer show a message box, now that scheduled MRM-HR is supported
                exportMethodDlg1.MethodType = ExportMethodType.Scheduled;
                exportMethodDlg1.CancelButton.PerformClick();
            });

            // Select the dummy RT regression AND enable MS1 filtering
            // Now we can export an inclusion list method.
            SelectDummyRTRegression();
            EnableMS1Filtering();
            DisableMS2Filtering();

      
            RunDlg<ExportMethodDlg>(
                (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method)),
                exportMethodDlg =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.ABI_TOF;

                    Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                    Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);

                    // change Method type to "Scheduled"
                    // We are exporting an IDA experiment type (inclusion list)
                    // There will be a RT associated with each entry in the inclusion list
                    // We should not see the dwell time text field
                    exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                    Assert.AreEqual(ExportMethodDlg.CONCUR_PREC_TXT, exportMethodDlg.GetMaxLabelText);
                    Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                    Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);

                    // change Method type to "Standard".
                    // We are exporting an IDA experiment (inclusion list)
                    // Each entry in the inclusion list will be assigned a RT of 0
                    // We should not see the dwell time text field
                    exportMethodDlg.MethodType = ExportMethodType.Standard;
                    Assert.AreEqual(ExportMethodDlg.PREC_PER_SAMPLE_INJ_TXT, exportMethodDlg.GetMaxLabelText);
                    Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                    Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);

                    exportMethodDlg.CancelButton.PerformClick();
                }
            );

            DeselectDummyRTRegression();

        }

        private static void CreateDummyRTRegression()
        {
            
            // Add dummy retention time regression so that we can select "Scheduled" method type
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editRTDlg = ShowDialog<EditRTDlg>(peptideSettingsDlg.AddRTRegression);

            RunUI(() =>
            {
                const string ssrCalc = "SSRCalc 3.0 (300A)";
                editRTDlg.ChooseCalculator(ssrCalc);
                editRTDlg.SetRegressionName("EditMethodDialogTest");
                editRTDlg.SetSlope("1.0");
                editRTDlg.SetIntercept("0");
                editRTDlg.SetTimeWindow(10);

                editRTDlg.OkDialog();
            });
            WaitForClosedForm(editRTDlg);

            RunUI(peptideSettingsDlg.OkDialog);
            WaitForClosedForm(peptideSettingsDlg);
        }

        private static void SetDocument(Func<SrmDocument, SrmDocument> changeDoc)
        {
            var doc = SkylineWindow.Document;
            var docNew = changeDoc(doc);
            Assert.IsFalse(ReferenceEquals(docNew, doc), "Document change was expected");
            Assert.IsTrue(SkylineWindow.SetDocument(docNew, doc));
            Assert.IsFalse(ReferenceEquals(SkylineWindow.Document, doc), "SetDocument failed to set the new document");
            Assert.IsTrue(ReferenceEquals(SkylineWindow.Document, docNew), "Unexpected document set after SetDocument");
        }

        private static void DeselectDummyRTRegression()
        {
            SetDocument(doc=> doc.ChangeSettings(doc.Settings.ChangeTransitionPrediction(predict =>
                predict.ChangeRetentionTime(RetentionTimeList.GetDefault()))));
        }

        private static void SelectDummyRTRegression()
        {
            SetDocument(doc => doc.ChangeSettings(doc.Settings.ChangeTransitionPrediction(predict =>
                predict.ChangeRetentionTime(Settings.Default.RetentionTimeList["EditMethodDialogTest"]))));
        }

        private static void EnableMS1Filtering()
        {
            SetDocument(doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, 1, IsotopeEnrichmentsList.GetDefault()))));
        }

        private static void DisableMS1Filtering()
        {
            SetDocument(doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.None, null, null))));
        }

        private static void EnableMS2Filtering()
        {
            SetDocument(doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangeAcquisitionMethod(FullScanAcquisitionMethod.Targeted, null))));
        }

        private static void DisableMS2Filtering()
        {
            SetDocument(doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangeAcquisitionMethod(FullScanAcquisitionMethod.None, null))));
        }
    }
}
