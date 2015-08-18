/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test DDA and Targeted isolation list export.
    /// </summary>
    [TestClass]
    public class ExportIsolationListTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestExportIsolationList()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.none);
        }

        [TestMethod]
        public void TestExportIsolationListAsSmallMolecules()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void TestExportIsolationListWithSLens()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.none, withSLens: true);
        }

        [TestMethod]
        public void TestExportIsolationListAsSmallMoleculesWithSLens()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.formulas, withSLens: true);
        }

        [TestMethod]
        public void TestExportIsolationListAsSmallMoleculesNegative()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.formulas, negativeCharges:true);
        }

        [TestMethod]
        public void TestExportIsolationListAsSmallMoleculeMasses()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        [TestMethod]
        public void TestExportIsolationListAsExplicitRetentionTimes()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.none, true);
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.formulas, true);
        }

        private RefinementSettings.ConvertToSmallMoleculesMode SmallMoleculeTestMode { get; set; }
        private bool AsSmallMoleculesNegative { get; set; }
        private bool AsExplicitRetentionTimes { get; set; }
        private bool WithSLens { get; set; }

        public void DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules, 
            bool asExplicitRetentionTimes = false, bool negativeCharges = false, bool withSLens = false)
        {
            SmallMoleculeTestMode = asSmallMolecules;
            AsSmallMoleculesNegative = (SmallMoleculeTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none) && negativeCharges;
            AsExplicitRetentionTimes = asExplicitRetentionTimes;
            WithSLens = withSLens;

            TestFilesZip = @"TestFunctional\ExportIsolationListTest.zip";
            // Avoid trying to reuse the .skyd file while another test is still extant
            if (AsExplicitRetentionTimes)
                TestDirectoryName = "AsExplicitRetentionTimes"; 
            else if (AsSmallMoleculesNegative)
                TestDirectoryName = "AsSmallMoleculesNegative_" + SmallMoleculeTestMode;
            else if (SmallMoleculeTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none)
                TestDirectoryName = "AsSmallMolecules_" + SmallMoleculeTestMode;
            if (WithSLens)
                TestDirectoryName += "WithSLens";
            RunFunctionalTest();
        }

        private CultureInfo _cultureInfo;
        private char _fieldSeparator;

        protected override void DoTest()
        {
            // This test is quite specific to the input data set - needs results for all nodes to export the list.  Shut off the special non-proteomic test mode, otherwise you get:
            // To export a scheduled method, you must first choose a retention time predictor in Peptide Settings / Prediction, or import results for all peptides in the document.
            TestSmallMolecules = false;

            // For now, CSV files are produced with invariant culture because some manufacturers do not handle internationalized CSVs.
            _cultureInfo = CultureInfo.InvariantCulture;
            _fieldSeparator = TextUtil.GetCsvSeparator(_cultureInfo);
            string isolationWidth = string.Format(_cultureInfo, "Narrow (~{0:0.0} m/z)", 1.3);
            const double slens = 1.234;

            // Load document which is already configured for DDA, and contains data for scheduling
            string standardDocumentFile = TestFilesDir.GetTestPath("BSA_Protea_label_free_meth3.sky");
            RunUI(() => { SkylineWindow.OpenFile(standardDocumentFile); });
            WaitForDocumentLoaded();
            if (SmallMoleculeTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                ConvertDocumentToSmallMolecules(SmallMoleculeTestMode, AsSmallMoleculesNegative);
                WaitForDocumentLoaded();
            }

            var t46 = 46.790;
            var t39 = 39.900;
            var halfWin = 1.0;
            if (AsExplicitRetentionTimes)
            {
                const double timeOffset = 10;  // To verify that explicit retention times are in use
                const double winOffset = .4;

                RunUI(() => SkylineWindow.ModifyDocument("Convert to explicit retention times", document =>
                {
                    var refine = new RefinementSettings();
                    return refine.ConvertToExplicitRetentionTimes(document, timeOffset, winOffset);
                }));

                t46 += timeOffset;
                t39 += timeOffset;
                halfWin += winOffset*0.5;
            }

            // Conversion to negative charge states shifts the masses
            var mzFirst = AsSmallMoleculesNegative ? 580.304419 : 582.318971;
            var mzLast = AsSmallMoleculesNegative ? 442.535467 : 444.55002;
            var zFirst = AsSmallMoleculesNegative ? -2 : 2;
            var zLast = AsSmallMoleculesNegative ? -3 : 3;
            var ceFirst = AsSmallMoleculesNegative ? 20.3 : 20.4;
            var ceLast = AsSmallMoleculesNegative ? 16.2 : 19.2;
            double? slensA = null; 
            double? slensB = null;
            if (WithSLens)
            {
                slensA = slens;
                slensB = ThermoMassListExporter.DEFAULT_SLENS;
            }

            // Export Agilent unscheduled DDA list.
            ExportIsolationList(
                "AgilentUnscheduledDda.csv",
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.None, ExportMethodType.Standard,
                AgilentIsolationListExporter.GetDdaHeader(_fieldSeparator),
                FieldSeparate("True", mzFirst, 20, zFirst, "Preferred", 0, string.Empty, isolationWidth, ceFirst),
                FieldSeparate("True", mzLast, 20, zLast, "Preferred", 0, string.Empty, isolationWidth, ceLast));

            // Export Agilent scheduled DDA list.
            ExportIsolationList(
                "AgilentScheduledDda.csv", 
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.None, ExportMethodType.Scheduled,
                AgilentIsolationListExporter.GetDdaHeader(_fieldSeparator),
                FieldSeparate("True", mzFirst, 20, zFirst, "Preferred", t46, 2*halfWin, isolationWidth, ceFirst),
                FieldSeparate("True", mzLast, 20, zLast, "Preferred", t39, 2*halfWin, isolationWidth, ceLast));

            // Export Thermo unscheduled DDA list.
            const double nce = ThermoQExactiveIsolationListExporter.NARROW_NCE;
            bool AsSmallMolecules = (SmallMoleculeTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none);
            bool AsSmallMoleculeMasses = (SmallMoleculeTestMode == RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
            var peptideA = AsSmallMolecules
                ? (AsSmallMoleculeMasses ? "Ion [1164.639040/1165.343970] (light)" : (AsSmallMoleculesNegative ? "LVNELTEFAK(-H2) (light)" : "LVNELTEFAK(+H2) (light)"))
                : "LVNELTEFAK (light)";
            var peptideB = AsSmallMolecules
                ? (AsSmallMoleculeMasses ? "Ion [1333.651707/1334.400621] (light)" : (AsSmallMoleculesNegative ? "IKNLQSLDPSH(-H3) (light)" : "IKNLQSLDPSH(+H3) (light)"))
                : "IKNLQS[+80.0]LDPSH (light)";
            var polarity = AsSmallMoleculesNegative ? "Negative" : "Positive";
            var thermoQExactiveIsolationListExporter = new ThermoQExactiveIsolationListExporter(SkylineWindow.Document)
            {
                UseSlens = WithSLens
            };
            ExportIsolationList(
                "ThermoUnscheduledDda.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.None, ExportMethodType.Standard,
                thermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, string.Empty, string.Empty, Math.Abs(zFirst), polarity, string.Empty, string.Empty, nce, slensA, peptideA),
                FieldSeparate(mzLast, string.Empty, string.Empty, Math.Abs(zLast), polarity, string.Empty, string.Empty, nce, slensB, peptideB));

            // Export Thermo scheduled DDA list.
            if (!AsSmallMoleculesNegative) // .skyd file chromatograms are not useful in this conversion due to mass shift
              ExportIsolationList(
                "ThermoScheduledDda.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.None, ExportMethodType.Scheduled,
                thermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, string.Empty, string.Empty, Math.Abs(zFirst), polarity, t46 - halfWin, t46 + halfWin, nce, slensA, peptideA),
                FieldSeparate(mzLast, string.Empty, string.Empty, Math.Abs(zLast), polarity, t39 - halfWin, t39 + halfWin, nce, slensB, peptideB));

            // Export Agilent unscheduled Targeted list.
            ExportIsolationList(
                "AgilentUnscheduledTargeted.csv", 
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                AgilentIsolationListExporter.GetTargetedHeader(_fieldSeparator),
                FieldSeparate("True", mzFirst, zFirst, 0, string.Empty, isolationWidth, ceFirst, string.Empty),
                FieldSeparate("True", mzLast, zLast, 0, string.Empty, isolationWidth, ceLast, string.Empty));

            // Export Agilent scheduled Targeted list.
            if (!AsSmallMoleculesNegative) // .skyd file chromatograms are not useful in this conversion due to mass shift
                ExportIsolationList(
                "AgilentScheduledTargeted.csv", 
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                AgilentIsolationListExporter.GetTargetedHeader(_fieldSeparator),
                FieldSeparate("True", mzFirst, zFirst, t46, 2*halfWin, isolationWidth, ceFirst, string.Empty),
                FieldSeparate("True", mzLast, zLast, t39, 2*halfWin, isolationWidth, ceLast, string.Empty));

            // Export Thermo unscheduled Targeted list.
            ExportIsolationList(
                "ThermoUnscheduledTargeted.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                thermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, string.Empty, string.Empty, Math.Abs(zFirst), polarity, string.Empty, string.Empty, nce, slensA, peptideA),
                FieldSeparate(mzLast, string.Empty, string.Empty, Math.Abs(zLast), polarity, string.Empty, string.Empty, nce, slensB, peptideB));

            // Export Thermo scheduled Targeted list.
            if (!AsSmallMoleculesNegative) // .skyd file chromatograms are not useful in this conversion due to mass shift
                ExportIsolationList(
                "ThermoScheduledTargeted.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                thermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, string.Empty, string.Empty, Math.Abs(zFirst), polarity, t46 - halfWin, t46 + halfWin, nce, slensA, peptideA),
                FieldSeparate(mzLast, string.Empty, string.Empty, Math.Abs(zLast), polarity, t39 - halfWin, t39 + halfWin, nce, slensB, peptideB));

            // Export Thermo Fusion unscheduled Targeted list.
            var thermoFusionMassListExporter = new ThermoFusionMassListExporter(SkylineWindow.Document)
            {
                UseSlens = WithSLens
            };
            ExportIsolationList(
                "FusionUnscheduledTargeted.csv",
                ExportInstrumentType.THERMO_FUSION, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                thermoFusionMassListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, zFirst, string.Empty, string.Empty, nce),
                FieldSeparate(mzLast, zLast, string.Empty, string.Empty, nce));

            // Export Thermo Fusion scheduled Targeted list.
            if (!AsSmallMoleculesNegative) // .skyd file chromatograms are not useful in this conversion due to mass shift
                ExportIsolationList(
                "FusionScheduledTargeted.csv",
                ExportInstrumentType.THERMO_FUSION, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                thermoFusionMassListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, zFirst, t46 - halfWin, t46 + halfWin, nce, slensA),
                FieldSeparate(mzLast, zLast, t39 - halfWin, t39 + halfWin, nce, slensB));

            string fragmentsFirst;
            if (!AsSmallMolecules || AsExplicitRetentionTimes)
                fragmentsFirst = FieldSeparate(582.318971, 951.478188, 595.308603, 708.392667, 837.43526, 1017.525138);
            else if (!AsSmallMoleculesNegative)
                fragmentsFirst = FieldSeparate(582.318971, 595.308603, 708.392667, 837.43526, 951.478188, 1017.525138);
            else
                fragmentsFirst = FieldSeparate(580.304419, 595.3097, 708.393764, 837.436357, 951.479285, 1017.526235);
            string fragmentsLast;
            if (!AsSmallMolecules || AsExplicitRetentionTimes)
                fragmentsLast = FieldSeparate(444.55002, 496.744257, 340.161545, 406.855332, 609.77936, 545.731878);
            else if (!AsSmallMoleculesNegative)
                fragmentsLast = FieldSeparate(340.161545, 406.855332, 444.55002, 455.188488, 488.710414, 496.744257);
            else
                fragmentsLast = FieldSeparate(340.162642, 406.856429, 442.535467, 455.189585, 488.711512, 496.745354);
            var rtFirst = !AsExplicitRetentionTimes
                ? FieldSeparate(45.790649, 47.790649)
                : FieldSeparate(55.590649, 57.990649);
            var rtLast = !AsExplicitRetentionTimes
                ? FieldSeparate(38.902004, 40.902004)
                : FieldSeparate(48.702004, 51.102004);
            var trapCeFirstTrap = !AsSmallMoleculesNegative
                ? FieldSeparate(21.225723, 27.893228)
                : FieldSeparate(21.168106, 27.822316);
            var trapCeLastTrap = !AsSmallMoleculesNegative
                ? FieldSeparate(17.285531, 23.043761)
                : FieldSeparate(17.227914, 22.972848);
            var trapCeFirstTransfer = !AsSmallMoleculesNegative
                ? FieldSeparate(26.225723, 32.893228)
                : FieldSeparate(26.168106, 32.822316);
            var trapCeLastTransfer = !AsSmallMoleculesNegative
                ? FieldSeparate(22.285531, 28.043761)
                : FieldSeparate(22.227914, 27.972848);

            // Export Waters Synapt (trap region) unscheduled Targeted list
            ExportIsolationList(
                "WatersTrapUnscheduledTargeted.mrm",
                ExportInstrumentType.WATERS_SYNAPT_TRAP, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, 0, 30, mzFirst, fragmentsFirst, trapCeFirstTrap, 2, 2, 30, 0, 0, 199),
                FieldSeparate(0, 0, 30, mzLast, fragmentsLast, trapCeLastTrap, 2, 2, 30, 0, 0, 199));

            // Export Waters Synapt (trap region) scheduled Targeted list
            ExportIsolationList(
                "WatersTrapScheduledTargeted.mrm",
                ExportInstrumentType.WATERS_SYNAPT_TRAP, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, rtFirst, mzFirst, fragmentsFirst, trapCeFirstTrap, 2, 2, 30, 0, 0, 199),
                FieldSeparate(0, rtLast, mzLast, fragmentsLast, trapCeLastTrap, 2, 2, 30, 0, 0, 199));

            // Export Waters Synapt (transfer region) unscheduled Targeted list
            ExportIsolationList(
                "WatersTransferUnscheduledTargeted.mrm",
                ExportInstrumentType.WATERS_SYNAPT_TRANSFER, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, 0, 30, mzFirst, fragmentsFirst, 4, 4, trapCeFirstTransfer, 30, 0, 0, 199),
                FieldSeparate(0, 0, 30, mzLast, fragmentsLast, 4, 4, trapCeLastTransfer, 30, 0, 0, 199));

            // Export Waters Synapt (transfer region) scheduled Targeted list
            ExportIsolationList(
                "WatersTransferScheduledTargeted.mrm",
                ExportInstrumentType.WATERS_SYNAPT_TRANSFER, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, rtFirst, mzFirst, fragmentsFirst, 4, 4, trapCeFirstTransfer, 30, 0, 0, 199),
                FieldSeparate(0, rtLast, mzLast, fragmentsLast, 4, 4, trapCeLastTransfer, 30, 0, 0, 199));

            // Export Waters Xevo unscheduled Targeted list
            ExportIsolationList(
                "WatersXevoUnscheduledTargeted.mrm",
                ExportInstrumentType.WATERS_XEVO_QTOF, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, 0, 30, mzFirst, fragmentsFirst, trapCeFirstTrap, 30, 0, 0, 199),
                FieldSeparate(0, 0, 30, mzLast, fragmentsLast, trapCeLastTrap, 30, 0, 0, 199));

            // Export Waters Xevo scheduled Targeted list
            ExportIsolationList(
                "WatersXevoScheduledTargeted.mrm",
                ExportInstrumentType.WATERS_XEVO_QTOF, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, rtFirst, mzFirst, fragmentsFirst, trapCeFirstTrap, 30, 0, 0, 199),
                FieldSeparate(0, rtLast, mzLast, fragmentsLast, trapCeLastTrap, 30, 0, 0, 199));

            // Check error if analyzer is not set correctly.
            CheckMassAnalyzer(ExportInstrumentType.AGILENT_TOF, FullScanMassAnalyzerType.tof);
            CheckMassAnalyzer(ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanMassAnalyzerType.orbitrap);
        }

        private void ExportIsolationList(
            string csvFilename, string instrumentType, FullScanAcquisitionMethod acquisitionMethod, 
            ExportMethodType methodType, params string[] checkStrings)
        {
            // Set acquisition method, mass analyzer type, resolution, etc.
            if (Equals(instrumentType, ExportInstrumentType.AGILENT_TOF))
            {
                RunUI(() => SkylineWindow.ModifyDocument("Set TOF full-scan settings", doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.tof, 10000, null)
                    .ChangeProductResolution(FullScanMassAnalyzerType.tof, 10000, null)
                    .ChangeAcquisitionMethod(acquisitionMethod, null)))));
            }
            else
            {
                RunUI(() => SkylineWindow.ModifyDocument("Set Orbitrap full-scan settings", doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                    .ChangeProductResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                    .ChangeAcquisitionMethod(acquisitionMethod, null)))));
            }

            // Open Export Method dialog, and set method to scheduled or standard.
            string csvPath = TestContext.GetTestPath(csvFilename);
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = instrumentType;
                exportMethodDlg.MethodType = methodType;
                exportMethodDlg.UseSlens = WithSLens;
                Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                Assert.IsFalse(exportMethodDlg.IsMaxTransitionsEnabled);
            });

            if (methodType == ExportMethodType.Standard)
            {
                // Simply close the dialog.
                OkDialog(exportMethodDlg, ()=> exportMethodDlg.OkDialog(csvPath));
            }
            else
            {
                // Close the SchedulingOptionsDlg, then wait for export dialog to close.
                RunDlg<SchedulingOptionsDlg>(() => exportMethodDlg.OkDialog(csvPath), schedulingOptionsDlg => schedulingOptionsDlg.OkDialog());
                WaitForClosedForm(exportMethodDlg);
            }

            // Check for expected output.
            string csvOut = File.ReadAllText(csvPath);
            AssertEx.Contains(csvOut, checkStrings);
        }

        private static void CheckMassAnalyzer(string instrumentType, FullScanMassAnalyzerType massAnalyzer)
        {
            // NOTE: Change to a DIFFERENT mass analyzer than we expect.
            if (!Equals(instrumentType, ExportInstrumentType.AGILENT_TOF))
            {
                RunUI(() => SkylineWindow.ModifyDocument("Change full-scan settings", doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.tof, 10000, null)
                    .ChangeProductResolution(FullScanMassAnalyzerType.tof, 10000, null)
                    .ChangeAcquisitionMethod(FullScanAcquisitionMethod.Targeted, null)))));
            }
            else
            {
                RunUI(() => SkylineWindow.ModifyDocument("Change full-scan settings", doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                    .ChangeProductResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                    .ChangeAcquisitionMethod(FullScanAcquisitionMethod.Targeted, null)))));
            }

            var exportMethodDlg =
                ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() => { exportMethodDlg.InstrumentType = instrumentType; });
            RunDlg<MessageDlg>(
                exportMethodDlg.OkDialog,
                messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.ExportMethodDlg_OkDialog_The_precursor_mass_analyzer_type_is_not_set_to__0__in_Transition_Settings_under_the_Full_Scan_tab, messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });
            OkDialog(exportMethodDlg, exportMethodDlg.CancelDialog);
        }

        private string FieldSeparate(params object[] valuesIn)
        {
            var sb = new StringBuilder();
            var values = valuesIn.Where(v => v != null).ToArray();
            for (int i = 0; ; )
            {
                object value = values[i++];
                sb.Append(value is double ? string.Format(_cultureInfo, "{0}", value) : value.ToString());
                if (i == values.Length)
                    break;
                sb.Append(_fieldSeparator);
            }
            return sb.ToString();
        }

    }
}
