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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
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
            TestFilesZip = @"TestFunctional\ExportMethodDialogTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {

            CreateDummyRTRegression();

            ThermoTsqTest();

            ThermoLtqTest();

            AbiQtrapTest();

            AbiTofTest();

            // Avoid "The current document contains peptides without enough information to rank transitions for triggered acquisition."
            var save = TestSmallMolecules;
            TestSmallMolecules = false;
            AgilentThermoABSciexTriggeredTest();

            BrukerTOFMethodTest();
            TestSmallMolecules = save;

            ABSciexShortNameTest();
        }

        private static void ThermoTsqTest()
        {

            DeselectDummyRTRegression();
            DisableMS1Filtering();
            DisableMS2Filtering();

            string tsqTriggeredFailureMessage = TextUtil.LineSeparate(Resources.ExportMethodDlg_VerifySchedulingAllowed_The__0__instrument_lacks_support_for_direct_method_export_for_triggered_acquisition_,
                                                                      Resources.ExportMethodDlg_VerifySchedulingAllowed_You_must_export_a__0__transition_list_and_manually_import_it_into_a_method_file_using_vendor_software_);
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
                RunUI(() =>
                          {
                              exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO_TSQ;
                              Assert.AreEqual(ExportMethodDlg.TRANS_PER_SAMPLE_INJ_TXT, exportMethodDlg.GetMaxLabelText);
                              Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                              Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                              //Assert.IsTrue(exportMethodDlg.IsRunLengthVisible);
                              Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                          });
                RunDlg<MessageDlg>(() => exportMethodDlg.MethodType = ExportMethodType.Scheduled,
                    dlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_To_export_a_scheduled_list_you_must_first_choose_a_retention_time_predictor_in_Peptide_Settings_Prediction_or_import_results_for_all_peptides_in_the_document,
                            dlg.Message, 0);
                        dlg.OkDialog();
                    });
                RunDlg<MessageDlg>(() => exportMethodDlg.MethodType = ExportMethodType.Triggered,
                    dlg =>
                    {
                        AssertEx.AreComparableStrings(tsqTriggeredFailureMessage,
                            dlg.Message, 2);
                        dlg.OkDialog();
                    });
                RunUI(exportMethodDlg.CancelButton.PerformClick);
                WaitForClosedForm(exportMethodDlg);
            }


            // Select the dummy RT regression so that we can select the "Scheduled" method type
            SelectDummyRTRegression();

            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));

                RunUI(() =>
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
                    });

                RunDlg<MessageDlg>(() => exportMethodDlg.MethodType = ExportMethodType.Triggered,
                    dlg =>
                    {
                        AssertEx.AreComparableStrings(tsqTriggeredFailureMessage,
                            dlg.Message, 2);
                        dlg.OkDialog();
                    });

                RunUI(exportMethodDlg.CancelButton.PerformClick);
                WaitForClosedForm(exportMethodDlg);
            }

            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));

                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO;
                    Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                    Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                    Assert.AreEqual(ExportMethodDlg.DWELL_TIME_TXT, exportMethodDlg.GetDwellTimeLabel);
                    Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);
                    Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);

                    exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                    Assert.IsNull(FindOpenForm<MessageDlg>());
                    Assert.AreEqual(ExportMethodDlg.CONCUR_TRANS_TXT, exportMethodDlg.GetMaxLabelText);

                    Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);
                    Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                });

                RunDlg<MessageDlg>(() => exportMethodDlg.MethodType = ExportMethodType.Triggered,
                    dlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.ExportMethodDlg_VerifySchedulingAllowed_Triggered_acquistion_requires_a_spectral_library_or_imported_results_in_order_to_rank_transitions_,
                            dlg.Message, 0);
                        dlg.OkDialog();
                    });

                RunUI(exportMethodDlg.CancelButton.PerformClick);
                WaitForClosedForm(exportMethodDlg);
            }

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

            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() =>
                    SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));

                RunUI(() =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.ABI_QTRAP;

                        Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                        Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);

                        exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                        Assert.AreEqual(ExportMethodDlg.CONCUR_TRANS_TXT, exportMethodDlg.GetMaxLabelText);

                        Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);
                        Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                    });
                RunDlg<MessageDlg>(() => exportMethodDlg.MethodType = ExportMethodType.Triggered,
                    dlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.ExportMethodDlg_VerifySchedulingAllowed_Triggered_acquistion_requires_a_spectral_library_or_imported_results_in_order_to_rank_transitions_,
                            dlg.Message);
                        dlg.OkDialog();
                    });
                RunUI(exportMethodDlg.CancelButton.PerformClick);
                WaitForClosedForm(exportMethodDlg);
            }

            DeselectDummyRTRegression();
        }

        private static void AbiTofTest()
        {

            SelectDummyRTRegression();
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

        private void AgilentThermoABSciexTriggeredTest()
        {
            // Failure trying to export to file with a peptide lacking results or library match
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Bovine_std_curated_seq_small2-missing.sky")));
            WaitForDocumentLoaded();
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.AGILENT;
                    exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                });

                RunDlg<MessageDlg>(() => exportMethodDlg.MethodType = ExportMethodType.Triggered,
                    dlg =>
                    {
                        AssertEx.AreComparableStrings(Resources.ExportMethodDlg_VerifySchedulingAllowed_The_current_document_contains_peptides_without_enough_information_to_rank_transitions_for_triggered_acquisition_,
                            dlg.Message, 0);
                        dlg.OkDialog();
                    });

                RunUI(exportMethodDlg.CancelButton.PerformClick);
                WaitForClosedForm(exportMethodDlg);
            }

            // Success cases exporting for document with both results on some peptides and a library match on
            // a peptide without results
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Bovine_std_curated_seq_small2-trigger.sky")));
            string agilentExpected = TestFilesDir.GetTestPath("TranListTriggered.csv");
            string agilentActual = TestFilesDir.GetTestPath("TranListTriggered-actual.csv");
            string thermoExpected = TestFilesDir.GetTestPath("TranListIsrm.csv");
            string thermoActual = TestFilesDir.GetTestPath("TranListIsrm-actual.csv");
            string abSciexExpected = TestFilesDir.GetTestPath("TranListAbSciexTriggered.csv");
            string abSciexActual = TestFilesDir.GetTestPath("TranListAbSciexTriggered-actual.csv");
            string agilentActualMeth = TestFilesDir.GetTestPath("TranListTriggered-actual.m");
            string agilentTemplateMeth = TestFilesDir.GetTestPath("cm-HSA-2_1mm-tMRM-TH100B.m");
            // Agilent transition list
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.AGILENT;
                        exportMethodDlg.ExportStrategy = ExportStrategy.Single;

                        // change Method type to "Triggered"
                        exportMethodDlg.MethodType = ExportMethodType.Triggered;
                        Assert.IsTrue(exportMethodDlg.IsPrimaryCountVisible);
                        Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                        exportMethodDlg.PrimaryCount = 2;
                    });

                RunDlg<MultiButtonMsgDlg>(() => exportMethodDlg.OkDialog(agilentActual),
                    dlg => dlg.Btn0Click());
                WaitForClosedForm(exportMethodDlg);
            }

            AssertEx.NoDiff(File.ReadAllText(agilentExpected), File.ReadAllText(agilentActual));

            // AB Sciex transition list no previous results
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.ABI;
                    exportMethodDlg.ExportStrategy = ExportStrategy.Single;

                    // change Method type to "Triggered"
                    exportMethodDlg.MethodType = ExportMethodType.Triggered;
                    Assert.IsTrue(exportMethodDlg.IsPrimaryCountVisible);
                    Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                    exportMethodDlg.PrimaryCount = 2;
                });
                RunDlg<MultiButtonMsgDlg>(() => exportMethodDlg.OkDialog(abSciexActual),
                                          dlg => dlg.Btn0Click());
                WaitForClosedForm(exportMethodDlg);
            }
            AssertEx.NoDiff(File.ReadAllText(abSciexExpected), File.ReadAllText(abSciexActual));

            // Thermo transition list
            {
                RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List),
                    exportMethodDlg =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO;
                        exportMethodDlg.ExportStrategy = ExportStrategy.Single;

                        // change Method type to "Triggered"
                        exportMethodDlg.MethodType = ExportMethodType.Triggered;
                        exportMethodDlg.IsThermoStartAndEndTime = true;
                        Assert.IsTrue(exportMethodDlg.IsPrimaryCountVisible);
                        Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                        exportMethodDlg.OkDialog(thermoActual);
                    });
            }

            AssertEx.NoDiff(File.ReadAllText(thermoExpected), File.ReadAllText(thermoActual));

            ExportWithExplicitCollisionEnergyValues(thermoActual);

            // Agilent method
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
                RunUI(() =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.AGILENT6400;
                        exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                        exportMethodDlg.SetTemplateFile(agilentTemplateMeth);

                        // change Method type to "Triggered"
                        exportMethodDlg.MethodType = ExportMethodType.Triggered;
                        Assert.IsTrue(exportMethodDlg.IsPrimaryCountVisible);
                        Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                    });

                RunDlg<MultiButtonMsgDlg>(() => exportMethodDlg.OkDialog(agilentActualMeth),
                                          dlg => dlg.Btn0Click());
                WaitForClosedForm(exportMethodDlg);
            }
            Assert.IsTrue(Directory.Exists(agilentActualMeth));


            // AB Sciex transition list with previous results
            string abSciexWithResultsExpected = TestFilesDir.GetTestPath("TranListAbSciexWithResultsTriggered.csv");
            string abSciexWithResultsActual = TestFilesDir.GetTestPath("TranListAbSciexWithResultsTriggered-actual.csv");
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MRM Triggered MRM data imported.sky")));
            WaitForDocumentLoaded();
            {
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));              
                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.ABI;
                    exportMethodDlg.ExportStrategy = ExportStrategy.Single;

                    // change Method type to "Triggered"
                    exportMethodDlg.MethodType = ExportMethodType.Triggered;
                    Assert.IsTrue(exportMethodDlg.IsPrimaryCountVisible);
                    Assert.IsTrue(exportMethodDlg.IsOptimizeTypeEnabled);
                    exportMethodDlg.PrimaryCount = 2;
                    exportMethodDlg.OkDialog(abSciexWithResultsActual);
                });
                WaitForClosedForm(exportMethodDlg);
            }
            AssertEx.NoDiff(File.ReadAllText(abSciexWithResultsExpected), File.ReadAllText(abSciexWithResultsActual));
        }

        private void BrukerTOFMethodTest()
        {
            const string brukerOutputMethodFilename = "RetTimeMassListFile.Method";
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Bovine_std_curated_seq_small2-trigger.sky")));
            string brukerActualMeth = TestFilesDir.GetTestPath("brukermethodexport.m");
            string brukerExpectedMeth = TestFilesDir.GetTestPath("BrukerExpected.Method");
            string brukerTemplateMeth = TestFilesDir.GetTestPath("Bruker Template Scheduled Precursor List.m");
            WaitForDocumentLoaded();

            // Export PRM method unscheduled
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method),
                    exportMethodDlg =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.BRUKER_TOF;
                        exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                        exportMethodDlg.SetTemplateFile(brukerTemplateMeth);
                        exportMethodDlg.MethodType = ExportMethodType.Standard;
                        Assert.IsTrue(exportMethodDlg.IsRunLengthVisible);
                        Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                        exportMethodDlg.RunLength = 20;
                        exportMethodDlg.OkDialog(brukerActualMeth);
                    });

            Assert.IsTrue(Directory.Exists(brukerActualMeth));
            AssertEx.NoDiff(File.ReadAllText(brukerExpectedMeth), File.ReadAllText(Path.Combine(brukerActualMeth, brukerOutputMethodFilename)));
            DirectoryEx.SafeDelete(brukerActualMeth);

            // Export PRM method scheduled
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method),
                    exportMethodDlg =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.BRUKER_TOF;
                        exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                        exportMethodDlg.SetTemplateFile(brukerTemplateMeth);
                        exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                        Assert.IsFalse(exportMethodDlg.IsRunLengthVisible);
                        Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                        exportMethodDlg.OkDialog(brukerActualMeth);
                    });

            Assert.IsTrue(Directory.Exists(brukerActualMeth));
            brukerExpectedMeth = TestFilesDir.GetTestPath("BrukerExpectedSched.Method");
            AssertEx.NoDiff(File.ReadAllText(brukerExpectedMeth), File.ReadAllText(Path.Combine(brukerActualMeth, brukerOutputMethodFilename)));
            DirectoryEx.SafeDelete(brukerActualMeth);

            // Export PRM method scheduled error
            {
                RunUI(() => SkylineWindow.ModifyDocument("Remove RT prediction",
                    doc => doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(predict => predict.ChangeRetentionTime(null)))));
                var exportMethodDlgError = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
                RunUI(() =>
                {
                    exportMethodDlgError.InstrumentType = ExportInstrumentType.BRUKER_TOF;
                    exportMethodDlgError.ExportStrategy = ExportStrategy.Single;
                    exportMethodDlgError.SetTemplateFile(brukerTemplateMeth);
                });
                RunDlg<MessageDlg>(() => exportMethodDlgError.MethodType = ExportMethodType.Scheduled,
                    dlg => dlg.CancelDialog());
                OkDialog(exportMethodDlgError, exportMethodDlgError.CancelDialog);
            }

            // Export DIA Method
            {
                var isoWindows = new IsolationScheme("Prespecified", new[]
                {
                    new IsolationWindow(500, 521, null, 0.5, 0.5),
                    new IsolationWindow(520, 541, null, 0.5, 0.5),
                    new IsolationWindow(540, 561, null, 0.5, 0.5),
                });

                RunUI(() => SkylineWindow.ModifyDocument("Add isolation window list",
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(full => full.ChangeAcquisitionMethod(FullScanAcquisitionMethod.DIA, isoWindows)))));
                var exportMethodDlgDia = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));

                RunUI(() =>
                {
                    exportMethodDlgDia.InstrumentType = ExportInstrumentType.BRUKER_TOF;
                    exportMethodDlgDia.ExportStrategy = ExportStrategy.Single;
                    exportMethodDlgDia.SetTemplateFile(brukerTemplateMeth);
                });

                RunDlg<MessageDlg>(() => exportMethodDlgDia.MethodType = ExportMethodType.Scheduled, dlg =>
                {
                    Assert.AreEqual(Resources.ExportMethodDlg_comboTargetType_SelectedIndexChanged_Scheduled_methods_are_not_yet_supported_for_DIA_acquisition, dlg.Message);
                    dlg.CancelDialog();
                });

                RunUI(() =>
                {
                    Assert.IsTrue(exportMethodDlgDia.IsRunLengthVisible);
                    exportMethodDlgDia.RunLength = 20;
                    exportMethodDlgDia.OkDialog(brukerActualMeth);
                });

                Assert.IsTrue(Directory.Exists(brukerActualMeth));
                brukerExpectedMeth = TestFilesDir.GetTestPath("BrukerExpectedDIA.Method");
                AssertEx.NoDiff(File.ReadAllText(brukerExpectedMeth), File.ReadAllText(Path.Combine(brukerActualMeth, brukerOutputMethodFilename)));            
            }

            // Export DIA method error not prespecified
            {
                var isoResults = new IsolationScheme("Results (20)", 20.0);
                RunUI(() => SkylineWindow.ModifyDocument("Add results isolation scheme",
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(full => full.ChangeAcquisitionMethod(FullScanAcquisitionMethod.DIA, isoResults)))));

                var exportMethodDlgDia = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));

                RunUI(() =>
                {
                    exportMethodDlgDia.InstrumentType = ExportInstrumentType.BRUKER_TOF;
                    exportMethodDlgDia.ExportStrategy = ExportStrategy.Single;
                    exportMethodDlgDia.SetTemplateFile(brukerTemplateMeth);
                });

                RunDlg<MessageDlg>(exportMethodDlgDia.OkDialog, dlg =>
                {
                    Assert.AreEqual(Resources.ExportMethodDlg_OkDialog_The_DIA_isolation_list_must_have_prespecified_windows_, dlg.Message);
                    dlg.CancelDialog();
                });
                
                OkDialog(exportMethodDlgDia, exportMethodDlgDia.CancelDialog);
            }
        }

        private void ABSciexShortNameTest()
        {
            // Failure trying to export to file with a peptide lacking results or library match 
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("mods_shortNameTest.sky")));
            string modsShortNameExpected = TestFilesDir.GetTestPath("ModShortName.csv");
            string modsShortNameActual = TestFilesDir.GetTestPath("ModShortName-Actual.csv");

            ExportAbTransitionList(modsShortNameActual);

            AssertEx.NoDiff(File.ReadAllText(modsShortNameExpected), ReadAllNonSmallMoleculeText(modsShortNameActual));

            // Test fix for explicit and variable modifications
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("shortnames_4peptide.sky")));
            string modsShortNameExplicit = TestFilesDir.GetTestPath("ModShortName-Explicit.csv");
            ExportAbTransitionList(modsShortNameExplicit);
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("shortnames_4peptide-var.sky")));
            string modsShortNameVariable = TestFilesDir.GetTestPath("ModShortName-Variable.csv");
            ExportAbTransitionList(modsShortNameVariable);
            AssertEx.NoDiff(File.ReadAllText(modsShortNameExplicit), ReadAllNonSmallMoleculeText(modsShortNameVariable));

            string[] expectedPeptides = {"L[1Ac]VNELTEFAK", "S[1Ac]LNC[CAM]TLR", "L[1Ac]TWASHEK", "PSCVPLMR"};
            foreach (var line in File.ReadAllLines(modsShortNameExplicit))
            {
                Assert.IsTrue(expectedPeptides.Any(line.Contains));
            }
        }

        private static void ExportAbTransitionList(string pathList)
        {
            WaitForDocumentLoaded();

            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = ExportInstrumentType.ABI_QTRAP;
                exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                exportMethodDlg.OkDialog(pathList);
            });

            WaitForClosedForm(exportMethodDlg);
        }

        private void ExportWithExplicitCollisionEnergyValues(string pathList)
        {
            var original = SkylineWindow.Document;
            var refine = new RefinementSettings();
            var document = refine.ConvertToSmallMolecules(original);
            for (var loop = 0; loop < 2; loop++)
            {
                SkylineWindow.SetDocument(document, SkylineWindow.Document);
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO_QUANTIVA;
                    exportMethodDlg.OptimizeType = ExportOptimize.CE;
                    exportMethodDlg.MethodType = ExportMethodType.Standard;
                });
                RunUI(() => exportMethodDlg.OkDialog(pathList));
                WaitForClosedForm(exportMethodDlg);
                var actual = File.ReadAllLines(pathList);
                if (loop == 1)
                {
                    // Explicit CE values
                    Assert.AreEqual(document.MoleculeTransitionCount+1, actual.Length); // Should be just one line per transition, and a header
                    break;
                }
                else
                {
                    Assert.AreEqual(document.MoleculeTransitionCount*11 + 1, actual.Length); // Multiple steps, and a header
                }
                // Change the current document to use explicit CE values, verify that this changes the output
                var ce = 1;
                for (bool changing = true; changing; )
                {
                    changing = false;
                    foreach (var peptideGroupDocNode in document.MoleculeGroups)
                    {
                        var pepGroupPath = new IdentityPath(IdentityPath.ROOT, peptideGroupDocNode.Id);
                        foreach (var nodePep in peptideGroupDocNode.Molecules)
                        {
                            var pepPath = new IdentityPath(pepGroupPath, nodePep.Id);
                            foreach (var nodeTransitionGroup in nodePep.TransitionGroups)
                            {
                                if (!nodeTransitionGroup.ExplicitValues.CollisionEnergy.HasValue)
                                {
                                    var tgPath = new IdentityPath(pepPath, nodeTransitionGroup.Id);
                                    document = (SrmDocument)document.ReplaceChild(tgPath.Parent,
                                        nodeTransitionGroup.ChangeExplicitValues(nodeTransitionGroup.ExplicitValues.ChangeCollisionEnergy(ce++)));
                                    changing = true;
                                    break;
                                }
                            }
                        }
                        if (changing)
                            break;
                    }
                }
            }
            SkylineWindow.SetDocument(original, SkylineWindow.Document);
        }

        private string ReadAllNonSmallMoleculeText(string pathList)
        {
            var actual = File.ReadAllText(pathList);
            if (TestSmallMolecules)
            {
                for (int i = 0; i++ < 4; )
                    actual = actual.Substring(0, actual.LastIndexOf('\n') - 1); // Trim test molecule related lines
            }
            return actual;
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
                const double slope = 1.0;
                editRTDlg.SetSlope(slope.ToString(LocalizationHelper.CurrentCulture));
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
            SetDocument(doc=> doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(predict =>
                predict.ChangeRetentionTime(null))));
        }

        private static void SelectDummyRTRegression()
        {
            SetDocument(doc => doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(predict =>
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
