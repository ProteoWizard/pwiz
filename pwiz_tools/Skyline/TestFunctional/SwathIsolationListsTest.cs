/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SwathIsolationListsTest: AbstractFunctionalTestEx
    {
        private const string Fixed15 = "Fixed15Da-CES-5_10.txt";
        private const string Fixed25 = "Fixed25Da-CES-5_10.txt";
        private const string VW64 = "VW64_CES-5_10.txt";
        private const string VW100 = "VW100_CES-5_10.txt";

        [TestMethod]
        public void TestSwathIsolationLists()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestSwathIsolationList(Fixed15, Resources.IsolationSchemeList_GetDefaults_SWATH__15_m_z_);
            TestSwathIsolationList(Fixed25, Resources.IsolationSchemeList_GetDefaults_SWATH__25_m_z_);
            TestSwathIsolationList(VW64, Resources.IsolationSchemeList_GetDefaults_SWATH__VW_64_);
            TestSwathIsolationList(VW100, Resources.IsolationSchemeList_GetDefaults_SWATH__VW_100_);
        }

        private void TestSwathIsolationList(string listName, string isolationSchemeName)
        {             
            // Display full scan tab.
            var fullScanDlg = ShowTransitionSettings(TransitionSettingsUI.TABS.FullScan);

            // Switch isolation scheme.
            RunUI(() =>
            {
                fullScanDlg.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                fullScanDlg.ProductMassAnalyzer = FullScanMassAnalyzerType.tof;
                fullScanDlg.IsolationSchemeName = isolationSchemeName;
            });
            OkDialog(fullScanDlg, fullScanDlg.OkDialog);

            // Open Export Method dialog, and set method to standard.
            string exportPath = TestContext.GetTestPath(listName);
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = ExportInstrumentType.ABI_TOF;
                exportMethodDlg.MethodType = ExportMethodType.Standard;
            });
            OkDialog(exportMethodDlg, () => exportMethodDlg.OkDialog(exportPath));

            // Check for expected output.
            string actualExport = File.ReadAllText(exportPath);
            string expectedExport = GetExpectedExport(listName);
            Assert.AreEqual(expectedExport, actualExport);
        }

        private string GetExpectedExport(string name)
        {
            switch (name)
            {
                case Fixed15: return
@"395.5	410.5	5
409.5	424.5	5
423.5	438.5	5
437.5	452.5	5
451.5	466.5	5
465.5	480.5	5
479.5	494.5	5
493.5	508.5	5
507.5	522.5	5
521.5	536.5	5
535.5	550.5	5
549.5	564.5	5
563.5	578.5	5
577.5	592.5	5
591.5	606.5	5
605.5	620.5	5
619.5	634.5	5
633.5	648.5	5
647.5	662.5	5
661.5	676.5	5
675.5	690.5	5
689.5	704.5	5
703.5	718.5	5
717.5	732.5	5
731.5	746.5	5
745.5	760.5	5
759.5	774.5	5
773.5	788.5	5
787.5	802.5	5
801.5	816.5	5
815.5	830.5	5
829.5	844.5	5
843.5	858.5	5
857.5	872.5	5
871.5	886.5	5
885.5	900.5	5
899.5	914.5	5
913.5	928.5	5
927.5	942.5	5
941.5	956.5	5
955.5	970.5	5
969.5	984.5	5
983.5	998.5	5
997.5	1012.5	10
1011.5	1026.5	10
1025.5	1040.5	10
1039.5	1054.5	10
1053.5	1068.5	10
1067.5	1082.5	10
1081.5	1096.5	10
1095.5	1110.5	10
1109.5	1124.5	10
1123.5	1138.5	10
1137.5	1152.5	10
1151.5	1166.5	10
1165.5	1180.5	10
1179.5	1194.5	10
1193.5	1208.5	10
1207.5	1222.5	10
1221.5	1236.5	10
1235.5	1249.5	10
";

                case Fixed25: return
@"399.5	424.5	5
423.5	448.5	5
447.5	472.5	5
471.5	496.5	5
495.5	520.5	5
519.5	544.5	5
543.5	568.5	5
567.5	592.5	5
591.5	616.5	5
615.5	640.5	5
639.5	664.5	5
663.5	688.5	5
687.5	712.5	5
711.5	736.5	5
735.5	760.5	5
759.5	784.5	5
783.5	808.5	5
807.5	832.5	5
831.5	856.5	5
855.5	880.5	5
879.5	904.5	5
903.5	928.5	5
927.5	952.5	5
951.5	976.5	5
975.5	1000.5	10
999.5	1024.5	10
1023.5	1048.5	10
1047.5	1072.5	10
1071.5	1096.5	10
1095.5	1120.5	10
1119.5	1144.5	10
1143.5	1168.5	10
1167.5	1192.5	10
1191.5	1216.5	10
1215.5	1240.5	10
";

                case VW64: return
@"399.5	409.5	5
408.5	416.5	5
415.5	423.5	5
422.5	430.5	5
429.5	437.5	5
436.5	444.5	5
443.5	451.5	5
450.5	458.5	5
457.5	465.5	5
464.5	471.5	5
470.5	477.5	5
476.5	483.5	5
482.5	489.5	5
488.5	495.5	5
494.5	501.5	5
500.5	507.5	5
506.5	514.5	5
513.5	521.5	5
520.5	528.5	5
527.5	535.5	5
534.5	542.5	5
541.5	549.5	5
548.5	556.5	5
555.5	563.5	5
562.5	570.5	5
569.5	577.5	5
576.5	584.5	5
583.5	591.5	5
590.5	598.5	5
597.5	605.5	5
604.5	612.5	5
611.5	619.5	5
618.5	626.5	5
625.5	633.5	5
632.5	640.5	5
639.5	647.5	5
646.5	654.5	5
653.5	663.5	5
662.5	672.5	5
671.5	681.5	5
680.5	690.5	5
689.5	699.5	5
698.5	708.5	5
707.5	722.5	5
721.5	736.5	5
735.5	750.5	5
749.5	764.5	5
763.5	778.5	5
777.5	792.5	5
791.5	806.5	5
805.5	825.5	10
824.5	844.5	10
843.5	863.5	10
862.5	882.5	10
881.5	901.5	10
900.5	920.5	10
919.5	939.5	10
938.5	968.5	10
967.5	997.5	10
996.5	1026.5	10
1025.5	1075.5	10
1074.5	1124.5	10
1123.5	1173.5	10
1172.5	1249.5	10
";

                case VW100: return
@"399.5	406.5	5
405.5	412.5	5
411.5	418.5	5
417.5	424.5	5
423.5	430.5	5
429.5	436.5	5
435.5	442.5	5
441.5	448.5	5
447.5	454.5	5
453.5	459.5	5
458.5	464.5	5
463.5	469.5	5
468.5	474.5	5
473.5	479.5	5
478.5	484.5	5
483.5	489.5	5
488.5	494.5	5
493.5	499.5	5
498.5	504.5	5
503.5	509.5	5
508.5	514.5	5
513.5	519.5	5
518.5	524.5	5
523.5	529.5	5
528.5	534.5	5
533.5	539.5	5
538.5	544.5	5
543.5	549.5	5
548.5	554.5	5
553.5	559.5	5
558.5	564.5	5
563.5	569.5	5
568.5	574.5	5
573.5	579.5	5
578.5	584.5	5
583.5	589.5	5
588.5	594.5	5
593.5	599.5	5
598.5	604.5	5
603.5	609.5	5
608.5	614.5	5
613.5	619.5	5
618.5	624.5	5
623.5	629.5	5
628.5	634.5	5
633.5	639.5	5
638.5	644.5	5
643.5	649.5	5
648.5	654.5	5
653.5	660.5	5
659.5	666.5	5
665.5	672.5	5
671.5	678.5	5
677.5	684.5	5
683.5	690.5	5
689.5	696.5	5
695.5	702.5	5
701.5	708.5	5
707.5	714.5	5
713.5	720.5	5
719.5	726.5	5
725.5	732.5	5
731.5	738.5	5
737.5	744.5	5
743.5	750.5	5
749.5	756.5	5
755.5	763.5	5
762.5	770.5	5
769.5	777.5	5
776.5	784.5	5
783.5	791.5	5
790.5	798.5	5
797.5	805.5	5
804.5	812.5	5
811.5	819.5	5
818.5	826.5	5
825.5	834.5	5
833.5	842.5	5
841.5	850.5	5
849.5	858.5	5
857.5	867.5	5
866.5	876.5	5
875.5	885.5	5
884.5	894.5	5
893.5	903.5	5
902.5	914.5	5
913.5	925.5	5
924.5	936.5	5
935.5	950.5	5
949.5	964.5	5
963.5	978.5	5
977.5	992.5	5
991.5	1011.5	10
1010.5	1030.5	10
1029.5	1054.5	10
1053.5	1078.5	10
1077.5	1117.5	10
1116.5	1156.5	10
1155.5	1200.5	10
1199.5	1249.5	10
";
            }

            Assert.Fail();
            return null;
        }
    }
}
