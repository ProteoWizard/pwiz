/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

//
// Test for proper handling of Thermo FAIMS ion mobility data in SureQuant
// 

namespace TestPerf // Tests in this namespace are skipped unless the RunPerfTests attribute is set true
{
    [TestClass]
    public class PerfThermoSureQuantFAIMSTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestThermoSureQuantFAIMS()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfThermoSureQuantFAIMSTest.zip");
            TestFilesPersistent = new[] {@"220809_Pool1_SureQuant_Survey_500fmol.raw" }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        protected override void DoTest()
        {
            // Open the prepoulated .sky file (has no chromatograms)
            OpenDocument(GetTestPath(@"ThermoSureQuantFAIMSTest.sky"));

            // While developing this test I discovered that Share Minimized was leaving an empty .imsdb file
            // when it should have had 2 entries
            var zipPath = GetTestPath(@"ThermoSureQuantFAIMSTestMinimized.sky.zip");
            RunDlg<ShareTypeDlg>(SkylineWindow.ShareDocument);
            RunUI(() => SkylineWindow.ShareDocument(zipPath, ShareType.MINIMAL));
            File.Delete(GetTestPath(@"FAIMS_SureQuant.imsdb")); // Make sure we don't use original
            LoadNewDocument(true); // Reset
            OpenDocument(zipPath); // Load minimized doc
            // Inspect the minimized ion mobility library
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => { transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.IonMobility; });
            var editIonMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsUI.IonMobilityControl.EditIonMobilityLibrary);
            AssertEx.IsTrue(editIonMobilityLibraryDlg.LibraryMobilitiesFlatCount == 2);
            OkDialog(editIonMobilityLibraryDlg, editIonMobilityLibraryDlg.OkDialog);
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

            // Now import chromatograms, check that FAIMS filtering is working
            // Second transition will include points from wrong CV if we aren't treating MS2 CV data properly
            ImportResultsFile(GetTestPath(TestFilesPersistent[0]));
            var results = SkylineWindow.Document.MoleculeTransitions.ToArray()[1].Results;
            var transitionChromInfo = results[0].AsList()[0];
            var doc = SkylineWindow.Document;
            var chromatogramSet = doc.Settings.MeasuredResults.Chromatograms.First();
            var pair = doc.PeptidePrecursorPairs.ToArray()[1];
            doc.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, pair.NodePep, pair.NodeGroup, new pwiz.Common.Chemistry.MzTolerance(.001f), out var chromGroups);
            foreach (var chromatogramGroupInfo in chromGroups)
            {
                var chromatogramInfo = chromatogramGroupInfo.TransitionPointSets.First();
                var intensities = chromatogramInfo.Intensities;
                var npoints = intensities.Count;
                Assume.AreEqual(1621, npoints);
                var expected = new []
                {
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 9794.808, 50738.7852, 84061.625, 114143.828, 99994.9844, 82405.5859, 70431.125, 35012.9766,
                    0, 0, 0, 0, 0, 0, 131.6874, 943.962158, 1756.21155, 2568.48633, 3380.73584, 4192.98535, 5005.26,
                    5817.50928, 6629.784, 7442.03369, 8254.309, 9066.558, 9878.808, 10691.082, 11503.3311, 12315.6064,
                    13127.8555, 13940.1309, 14752.38, 15564.6289, 16376.9043, 17189.1543, 18001.4277, 18813.6777,
                    19625.9531, 20438.2012, 21250.4512, 22062.7266, 22874.9746, 23687.25, 24499.5, 25311.7734,
                    26124.0234, 26936.2734, 27748.5488, 28560.7969, 29373.0723, 30185.3223, 30997.57, 31809.8457,
                    32622.0957, 33434.37, 34246.62, 35058.8945, 35871.1445, 36683.3945, 37495.668, 38307.918, 39120.19,
                    39932.44, 40744.7148, 41556.9648, 42369.2148, 43181.49, 43993.74, 44806.0156, 45618.26, 46430.54,
                    47242.79, 48055.04, 48867.3125, 49679.5625, 50491.8359, 51304.0859, 52116.36, 52928.61, 53740.86,
                    54553.1328, 55365.3828, 56177.6563, 56989.9063, 57802.1836, 58614.4336, 59426.68, 60238.957,
                    61051.207, 61863.48, 62675.73, 63488.0039, 64300.2539, 65112.5039, 65924.78, 66737.03, 67549.3047,
                    68361.5547, 69173.83, 69986.08, 70798.33, 71610.6, 72422.85, 73235.125, 74047.375, 74859.625,
                    75671.9, 76484.15, 77296.42, 78108.67, 78920.9453, 79733.1953, 80545.4453, 81357.72, 82169.97,
                    82982.24, 83794.49, 84606.7656, 85419.0156, 86231.2656, 87043.54, 87855.79, 246267.813, 557429.75,
                    974534.8, 1353749.75, 1374841.88, 1395934.5, 1239506.88, 1019679.56, 772074.2, 503956.7, 235847.547,
                    157727.984, 91732.29, 50355.1953, 19711.5566, 11481.6826, 43668.7148, 61649.7734, 62899.76,
                    64149.79, 65399.7773, 66649.8047, 67899.8, 69149.82, 70399.81, 71649.8047, 72899.83, 74149.82,
                    75399.84, 76649.8359, 70782.12, 58940.39, 47384.7656, 37761.9453, 28139.42, 18516.5957, 8894.072, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0
                };
                for (var i = 0; i < npoints; i++)
                {
                    Assume.AreEqual(expected[i], intensities[i], .1, $@"intensity difference at point #{i}, {expected[i]} vs {intensities[i]}");
                }
                Assume.AreEqual(14.5112, transitionChromInfo.StartRetentionTime, .01, "start time differs");
                Assume.AreEqual(14.9879065, transitionChromInfo.EndRetentionTime, .01, "end time differs");
            }
        }
    }
}