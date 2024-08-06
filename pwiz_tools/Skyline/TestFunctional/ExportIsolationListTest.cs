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
        public void TestExportIsolationListAsSmallMoleculesNegative()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.formulas, negativeCharges: RefinementSettings.ConvertToSmallMoleculesChargesMode.invert);
        }
        [TestMethod]
        public void TestExportIsolationListAsSmallMoleculesMixedPolarity()
        {
            DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode.formulas, negativeCharges: RefinementSettings.ConvertToSmallMoleculesChargesMode.invert_some);
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
        private RefinementSettings.ConvertToSmallMoleculesChargesMode AsSmallMoleculesNegativeMode { get; set; }
        private bool AsExplicitRetentionTimes { get; set; }

        public void DoTestExportIsolationList(RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules,
            bool asExplicitRetentionTimes = false, RefinementSettings.ConvertToSmallMoleculesChargesMode negativeCharges = RefinementSettings.ConvertToSmallMoleculesChargesMode.none)
        {
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none && SkipSmallMoleculeTestVersions())
            {
                return;
            }

            SmallMoleculeTestMode = asSmallMolecules;
            AsSmallMoleculesNegativeMode = negativeCharges;
            AsSmallMoleculesNegative = negativeCharges != RefinementSettings.ConvertToSmallMoleculesChargesMode.none;
            AsExplicitRetentionTimes = asExplicitRetentionTimes;

            TestFilesZip = @"TestFunctional\ExportIsolationListTest.zip";
            // Avoid trying to reuse the .skyd file while another test is still extant
            if (AsExplicitRetentionTimes)
                TestDirectoryName = "AsExplicitRetentionTimes"; 
            else if (SmallMoleculeTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none)
                TestDirectoryName = "AsSmallMolecules_" + SmallMoleculeTestMode;
            if (AsSmallMoleculesNegative)
                TestDirectoryName += "_Negative_" + AsSmallMoleculesNegativeMode;
            RunFunctionalTest();
        }

        private CultureInfo _cultureInfo;
        private char _fieldSeparator;

        protected override void DoTest()
        {
            // For now, CSV files are produced with invariant culture because some manufacturers do not handle internationalized CSVs.
            _cultureInfo = CultureInfo.InvariantCulture;
            _fieldSeparator = TextUtil.GetCsvSeparator(_cultureInfo);
            string isolationWidth = string.Format(_cultureInfo, "Narrow (~{0:0.0} m/z)", 1.3);

            // Load document which is already configured for DDA, and contains data for scheduling
            string standardDocumentFile = TestFilesDir.GetTestPath("BSA_Protea_label_free_meth3.sky");
            RunUI(() => { SkylineWindow.OpenFile(standardDocumentFile); });
            WaitForDocumentLoaded();
            if (SmallMoleculeTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                ConvertDocumentToSmallMolecules(SmallMoleculeTestMode, AsSmallMoleculesNegativeMode);
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
            var mzLast = AsSmallMoleculesNegative ? 442.535468 : 444.55002;
            var zFirst = AsSmallMoleculesNegative ? -2 : 2;
            var zLast = AsSmallMoleculesNegative ? -3 : 3;
            var ceFirst = AsSmallMoleculesNegative ? 20.3 : 20.4;
            var ceLast = AsSmallMoleculesNegative ? 19.1 : 19.2;

            // Export Agilent unscheduled DDA list.
            ExportIsolationList(
                "AgilentUnscheduledDda.csv",
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.None, ExportMethodType.Standard,
                AgilentIsolationListExporter.GetDdaHeader(_fieldSeparator),
                FieldSeparate("True", mzFirst, 20, zFirst, "Preferred", 0, string.Empty, isolationWidth, ceFirst),
                FieldSeparate("True", mzLast, 20, zLast, "Preferred", 0, string.Empty, isolationWidth, ceLast));

            // Export Agilent scheduled DDA list.
            if (!AsSmallMoleculesNegative) // .skyd file chromatograms are not useful in this conversion due to mass shift
              ExportIsolationList(
                "AgilentScheduledDda.csv", 
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.None, ExportMethodType.Scheduled,
                AgilentIsolationListExporter.GetDdaHeader(_fieldSeparator),
                FieldSeparate("True", mzFirst, 20, zFirst, "Preferred", t46, 2*halfWin, isolationWidth, ceFirst),
                FieldSeparate("True", mzLast, 20, zLast, "Preferred", t39, 2*halfWin, isolationWidth, ceLast));

            // Export Thermo unscheduled DDA list.
            const double nce = ThermoQExactiveIsolationListExporter.NARROW_NCE;
            var conversionDecorator = SmallMoleculeTestMode == RefinementSettings.ConvertToSmallMoleculesMode.none ?
                string.Empty :
                RefinementSettings.TestingConvertedFromProteomicPeptideNameDecorator;
            bool AsSmallMoleculeMasses = (SmallMoleculeTestMode == RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
            var peptideA = AsSmallMoleculeMasses ?
                CustomMolecule.INVARIANT_NAME_DETAIL + " [1162.623390/1163.328090] (light)" :
                conversionDecorator+"LVNELTEFAK (light)";
            var peptideB = AsSmallMoleculeMasses ? 
                CustomMolecule.INVARIANT_NAME_DETAIL + " [1330.628231/1331.376801] (light)" :
                SmallMoleculeTestMode == RefinementSettings.ConvertToSmallMoleculesMode.none ?
                conversionDecorator+"IKNLQS[+79.966331]LDPSH (light)" : 
                conversionDecorator+"IKNLQS[+80.0]LDPSH (light)";
            var polarity = AsSmallMoleculesNegative ? "Negative" : "Positive";
            var thermoQExactiveIsolationListExporter = new ThermoQExactiveIsolationListExporter(SkylineWindow.Document);
            ExportIsolationList(
                "ThermoUnscheduledDda.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.None, ExportMethodType.Standard,
                thermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, string.Empty, string.Empty, Math.Abs(zFirst), polarity, string.Empty, string.Empty, nce, peptideA),
                FieldSeparate(mzLast, string.Empty, string.Empty, Math.Abs(zLast), polarity, string.Empty, string.Empty, nce, peptideB));

            // Export Thermo scheduled DDA list.
            if (!AsSmallMoleculesNegative) // .skyd file chromatograms are not useful in this conversion due to mass shift
              ExportIsolationList(
                "ThermoScheduledDda.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.None, ExportMethodType.Scheduled,
                thermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, string.Empty, string.Empty, Math.Abs(zFirst), polarity, t46 - halfWin, t46 + halfWin, nce, peptideA),
                FieldSeparate(mzLast, string.Empty, string.Empty, Math.Abs(zLast), polarity, t39 - halfWin, t39 + halfWin, nce, peptideB));

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
                FieldSeparate(mzFirst, string.Empty, string.Empty, Math.Abs(zFirst), polarity, string.Empty, string.Empty, nce, peptideA),
                FieldSeparate(mzLast, string.Empty, string.Empty, Math.Abs(zLast), polarity, string.Empty, string.Empty, nce, peptideB));

            // Export Thermo scheduled Targeted list.
            if (!AsSmallMoleculesNegative) // .skyd file chromatograms are not useful in this conversion due to mass shift
                ExportIsolationList(
                "ThermoScheduledTargeted.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                thermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(mzFirst, string.Empty, string.Empty, Math.Abs(zFirst), polarity, t46 - halfWin, t46 + halfWin, nce, peptideA),
                FieldSeparate(mzLast, string.Empty, string.Empty, Math.Abs(zLast), polarity, t39 - halfWin, t39 + halfWin, nce, peptideB));

            // Export Thermo Fusion unscheduled Targeted list.
            var thermoFusionMassListExporter = new ThermoFusionMassListExporter(SkylineWindow.Document);
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
                FieldSeparate(mzFirst, zFirst, t46 - halfWin, t46 + halfWin, nce),
                FieldSeparate(mzLast, zLast, t39 - halfWin, t39 + halfWin, nce));

            // Export Thermo Stellar unscheduled isolation list
            var runStart = "0";
            var runEnd = "30";
            var thermoStellarMassListExporter = new ThermoStellarMassListExporter(SkylineWindow.Document);
            ExportIsolationList(
                "StellarIsolationList.csv",
                ExportInstrumentType.THERMO_STELLAR, FullScanAcquisitionMethod.PRM, ExportMethodType.Standard,
                thermoStellarMassListExporter.GetHeader(),
                FieldSeparate(mzFirst, zFirst, runStart, runEnd, ThermoStellarMassListExporter.WIDE_NCE),
                FieldSeparate(mzLast, zLast, runStart, runEnd, ThermoStellarMassListExporter.WIDE_NCE)
                );
            if (!AsSmallMoleculesNegative || AsExplicitRetentionTimes)
                ExportIsolationList(
                "StellarIsolationList.csv",
                ExportInstrumentType.THERMO_STELLAR, FullScanAcquisitionMethod.PRM, ExportMethodType.Scheduled,
                thermoStellarMassListExporter.GetHeader(),
                FieldSeparate(mzFirst, zFirst, t46 - halfWin, t46 + halfWin, ThermoStellarMassListExporter.WIDE_NCE),
                FieldSeparate(mzLast, zLast, t39 - halfWin, t39 + halfWin, ThermoStellarMassListExporter.WIDE_NCE)
            );
            string fragmentsFirst;
            if (!AsSmallMoleculesNegative)
                fragmentsFirst = FieldSeparate("582.3190", "951.4782", "595.3086", "708.3927", "837.4353", "1017.5251");
            else
                fragmentsFirst = FieldSeparate("580.3044", "595.3097", "708.3938", "837.4364", "951.4793", "1017.5262");
            string fragmentsLast;
            if (!AsSmallMoleculesNegative)
                fragmentsLast = FieldSeparate("444.5500", "496.7443", "340.1615", "406.8553", "609.7794", "545.7319");
            else
                fragmentsLast = FieldSeparate("340.1626", "406.8564", "442.5355", "455.1896", "488.7115", "496.7454");
            var rtFirst = !AsExplicitRetentionTimes
                ? FieldSeparate("45.8", "47.8")
                : FieldSeparate("55.6", "58.0");
            var rtLast = !AsExplicitRetentionTimes
                ? FieldSeparate("38.9", "40.9")
                : FieldSeparate("48.7", "51.1");
            var trapCeFirstTrap = !AsSmallMoleculesNegative
                ? FieldSeparate("21.2", "27.9")
                : FieldSeparate("21.2", "27.8");
            var trapCeLastTrap = !AsSmallMoleculesNegative
                ? FieldSeparate("17.3", "23.0")
                : FieldSeparate("17.2", "23.0");
            var trapCeFirstTransfer = !AsSmallMoleculesNegative
                ? FieldSeparate("26.2", "32.9")
                : FieldSeparate("26.2", "32.8");
            var trapCeLastTransfer = !AsSmallMoleculesNegative
                ? FieldSeparate("22.3", "28.0")
                : FieldSeparate("22.2", "28.0");

            // Export Waters Synapt (trap region) unscheduled Targeted list
            ExportIsolationList(
                "WatersTrapUnscheduledTargeted.mrm",
                ExportInstrumentType.WATERS_SYNAPT_TRAP, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, "0.0", "30.0", mzFirst.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsFirst, trapCeFirstTrap, "2.0", "2.0", 30, "0.0000", 0, 199),
                FieldSeparate(0, "0.0", "30.0", mzLast.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsLast, trapCeLastTrap, "2.0", "2.0", 30, "0.0000", 0, 199));

            // Export Waters Synapt (trap region) scheduled Targeted list
            if (!AsSmallMoleculesNegative || AsExplicitRetentionTimes)  // Negative ions do not match results, so need explicit RTs for scheduling
                ExportIsolationList(
                    "WatersTrapScheduledTargeted.mrm",
                    ExportInstrumentType.WATERS_SYNAPT_TRAP, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                    WatersIsolationListExporter.GetHeader(_fieldSeparator),
                    FieldSeparate(0, rtFirst, mzFirst.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsFirst, trapCeFirstTrap, "2.0", "2.0", 30, "0.0000", 0, 199),
                    FieldSeparate(0, rtLast, mzLast.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsLast, trapCeLastTrap, "2.0", "2.0", 30, "0.0000", 0, 199));

            // Export Waters Synapt (transfer region) unscheduled Targeted list
            ExportIsolationList(
                "WatersTransferUnscheduledTargeted.mrm",
                ExportInstrumentType.WATERS_SYNAPT_TRANSFER, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, "0.0", "30.0", mzFirst.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsFirst, "4.0", "4.0", trapCeFirstTransfer, 30, "0.0000", 0, 199),
                FieldSeparate(0, "0.0", "30.0", mzLast.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsLast, "4.0", "4.0", trapCeLastTransfer, 30, "0.0000", 0, 199));

            // Export Waters Synapt (transfer region) scheduled Targeted list
            if (!AsSmallMoleculesNegative || AsExplicitRetentionTimes)  // Negative ions do not match results, so need explicit RTs for scheduling
                ExportIsolationList(
                    "WatersTransferScheduledTargeted.mrm",
                    ExportInstrumentType.WATERS_SYNAPT_TRANSFER, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                    WatersIsolationListExporter.GetHeader(_fieldSeparator),
                    FieldSeparate(0, rtFirst, mzFirst.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsFirst, "4.0", "4.0", trapCeFirstTransfer, 30, "0.0000", 0, 199),
                    FieldSeparate(0, rtLast, mzLast.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsLast, "4.0", "4.0", trapCeLastTransfer, 30, "0.0000", 0, 199));

            // Export Waters Xevo unscheduled Targeted list
            ExportIsolationList(
                "WatersXevoUnscheduledTargeted.mrm",
                ExportInstrumentType.WATERS_XEVO_QTOF, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                WatersIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(0, "0.0", "30.0", mzFirst.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsFirst, trapCeFirstTrap, 30, "0.0000", 0, 199),
                FieldSeparate(0, "0.0", "30.0", mzLast.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsLast, trapCeLastTrap, 30, "0.0000", 0, 199));

            // Export Waters Xevo scheduled Targeted list
            if (!AsSmallMoleculesNegative || AsExplicitRetentionTimes)  // Negative ions do not match results, so need explicit RTs for scheduling
                ExportIsolationList(
                    "WatersXevoScheduledTargeted.mrm",
                    ExportInstrumentType.WATERS_XEVO_QTOF, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                    WatersIsolationListExporter.GetHeader(_fieldSeparator),
                    FieldSeparate(0, rtFirst, mzFirst.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsFirst, trapCeFirstTrap, 30, "0.0000", 0, 199),
                    FieldSeparate(0, rtLast, mzLast.ToString("0.0000", CultureInfo.InvariantCulture), fragmentsLast, trapCeLastTrap, 30, "0.0000", 0, 199));

            // Check error if analyzer is not set correctly.
            CheckMassAnalyzer(ExportInstrumentType.AGILENT_TOF, FullScanMassAnalyzerType.tof);
            CheckMassAnalyzer(ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanMassAnalyzerType.orbitrap);

            if (!AsExplicitRetentionTimes)
            {
                TestAgilentNegativeRt();
            }
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
            string csvPath = TestFilesDirs[0].GetTestPath(csvFilename);
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            var exportPolarityFilter = 
                AsSmallMoleculesNegativeMode != RefinementSettings.ConvertToSmallMoleculesChargesMode.none
                 && SkylineWindow.Document.IsMixedPolarity() ?
                ExportPolarity.separate :
                ExportPolarity.all;
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = instrumentType;
                exportMethodDlg.MethodType = methodType;
                exportMethodDlg.PolarityFilter = exportPolarityFilter;
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
            var csvResult = csvPath;
            if (exportPolarityFilter == ExportPolarity.separate)
            {
                // We will have emitted two docs, one for pos and one for neg
                var ext = csvPath.Substring(csvPath.LastIndexOf('.'));
                csvResult = csvPath.Replace(ext, string.Format("_{0}_0001{1}", ExportPolarity.negative, ext));
                var csvPos = File.ReadAllText(csvPath.Replace(ext, string.Format("_{0}_0001{1}", ExportPolarity.positive, ext)));
                var csvNeg = File.ReadAllText(csvResult);
                Assert.IsFalse(csvNeg.Equals(csvPos));
            }
            string csvOut = File.ReadAllText(csvResult);
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

        private void TestAgilentNegativeRt()
        {
            // remove the results to force the use of predicted RTs
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults,
                dlg =>
                {
                    dlg.RemoveAllReplicates();
                    dlg.OkDialog();
                });
            // Set RT predictor to produce negative times
            const float regressionWindow = 1.4f;
            var rtRegression = new RetentionTimeRegression("NegativeRt",
                new RetentionScoreCalculator(RetentionTimeRegression.SSRCALC_100_A), 
                .45, -7.8, regressionWindow, new MeasuredRetentionTime[0]);
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Set RT prediction with negative RTs", doc =>
                {
                    return doc.ChangeSettings(
                        doc.Settings.ChangePeptidePrediction(p => p.ChangeRetentionTime(rtRegression))
                            .ChangePeptidePrediction(p => p.ChangeUseMeasuredRTs(false)));
                });
            });
            // Export the transitions list for a scheduled method
            string csvPath = TestFilesDirs[0].GetTestPath("negativeRtTest.csv");
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = ExportInstrumentType.AGILENT_MASSHUNTER_12;
                exportMethodDlg.MethodType = ExportMethodType.Scheduled;
            });
            RunDlg<MultiButtonMsgDlg>(
                () => exportMethodDlg.OkDialog(csvPath),
                messageDlg =>
                {
                    //AssertEx.AreComparableStrings(Resources.ExportMethodDlg_OkDialog_The_precursor_mass_analyzer_type_is_not_set_to__0__in_Transition_Settings_under_the_Full_Scan_tab, messageDlg.Message, 1);
                    messageDlg.ClickYes();
                });

            string[] paths = { csvPath };
            if (SkylineWindow.Document.IsMixedPolarity())
            {
                // Mixed polarity documents result in two files 
                paths = new[]
                {
                    TestFilesDirs[0].GetTestPath("negativeRtTest_negative_0001.csv"),
                    TestFilesDirs[0].GetTestPath("negativeRtTest_positive_0001.csv")
                };
            }

            // Verify that all peptides have had their predicted retention time moved so that the retention time window subtracted from
            // the predicted retention time is at least AgilentMassListExporter.AGILENT_MIN_START_ACQUISITION_TIME
            foreach (var path in paths)
            {
                var resReader = new DsvFileReader(path, TextUtil.SEPARATOR_TSV);
                while (resReader.ReadLine() != null)
                {
                    Assert.IsTrue(float.TryParse(resReader.GetFieldByName("RT Window (min)"),
                        NumberStyles.Float | NumberStyles.AllowThousands, _cultureInfo, out var rtWindow));
                    Assert.AreEqual(regressionWindow, rtWindow);
                    Assert.IsTrue(float.TryParse(resReader.GetFieldByName("RT (min)"),
                        NumberStyles.Float | NumberStyles.AllowThousands, _cultureInfo, out var rtPredicted));
                    var pepSequence = resReader.GetFieldByName("Compound Name");
                    float halfWindow = rtWindow / 2;
                    float minPredictedTime = halfWindow + AgilentMassListExporter.AGILENT_MIN_START_ACQUISITION_TIME;
                    if (pepSequence == "YIC[+57.021464]DNQDTISSK.light" || pepSequence == "IKNLQSLDPSH.light")
                    {
                        // These two peptides are known to have predicted retention times less than rtWindow
                        Assert.AreEqual(rtPredicted, minPredictedTime,
                            "Peptide {0} retention time {1} should be equal to half-RT Window {2} plus minimum {3}",
                            pepSequence, rtPredicted, halfWindow, AgilentMassListExporter.AGILENT_MIN_START_ACQUISITION_TIME);
                    }
                    else
                    {
                        // All other peptides should have a predicted retention time that is greater than rtWindow
                        Assert.IsTrue(rtPredicted >= minPredictedTime,
                            "Peptide {0} retention time {1} should be greater than or equal to half-RT Window {2} plus minimum {3}",
                            pepSequence, rtPredicted, halfWindow, AgilentMassListExporter.AGILENT_MIN_START_ACQUISITION_TIME);
                    }
                }
            }
        }
    }

    [TestClass]
    public class ExportIsolationListTestMultiple : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestExportIsolationListMultiple()
        {
            TestFilesZip = @"TestFunctional\ExportIsolationListTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Test that exporting multiple methods produces the same as a single method
            var file = TestFilesDir.GetTestPath("example.sky");
            RunUI(() => { SkylineWindow.OpenFile(file); });
            WaitForDocumentLoaded();
            
            var exportMethodDlgSingle = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() =>
            {
                exportMethodDlgSingle.InstrumentType = ExportInstrumentType.THERMO_Q_EXACTIVE;
                exportMethodDlgSingle.ExportStrategy = ExportStrategy.Single;
                exportMethodDlgSingle.MethodType = ExportMethodType.Scheduled;
            });
            var outSingle = TestFilesDir.GetTestPath("outSingle.csv");
            OkDialog(exportMethodDlgSingle, () => exportMethodDlgSingle.OkDialog(outSingle));
            
            var exportMethodDlgMultiple = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() =>
            {
                exportMethodDlgMultiple.InstrumentType = ExportInstrumentType.THERMO_Q_EXACTIVE;
                exportMethodDlgMultiple.ExportStrategy = ExportStrategy.Buckets;
                exportMethodDlgMultiple.MaxTransitions = 100;
                exportMethodDlgMultiple.MethodType = ExportMethodType.Scheduled;
            });
            var outMultiple = TestFilesDir.GetTestPath("outMultiple");
            OkDialog(exportMethodDlgMultiple, () => exportMethodDlgMultiple.OkDialog(outMultiple + ".csv"));

            Assert.AreEqual(File.ReadAllText(outSingle), File.ReadAllText(outMultiple + "_0001.csv"));
        }
    }
}
