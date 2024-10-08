/*
 * Original author: Simon MacLean <simon .at. teammaclean.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests importing results with common prefixes and suffixes and the
    /// <see cref="ImportResultsNameDlg"/> form.
    /// </summary>
    [TestClass]
    public class RemovePrefixSuffixTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestRemovePrefixSuffix()
        {
            TestFilesZip = @"TestFunctional\RemovePrefixSuffixTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument("160109_Mix1_calcurve.sky");

            // Test simple prefix removal (0 and 1)
            string importFilePath = TestFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML");
            string copy1FilePath = TestFilesDir.GetTestPath("160109_Mix1_calcurve_071.mzML");
            File.Copy(importFilePath, copy1FilePath);
            TestRemoval(importFilePath, copy1FilePath, importFilePath, copy1FilePath, "0", "1");

            // Test simple suffix removal (0 and 1)
            string importFilePath2 = TestFilesDir.GetTestPath("060109_Mix1_calcurve_070.mzML");
            string copyFilePath2 = TestFilesDir.GetTestPath("160109_Mix1_calcurve_070.mzML");
            TestRemoval(importFilePath, copy1FilePath, importFilePath2, copyFilePath2, "0", "1");

            // Test suffix and prefix removal (0 and 1)
            string importFilePath3 = TestFilesDir.GetTestPath("060109_Mix0_calcurve_070.mzML");
            string copyFilePath3 = TestFilesDir.GetTestPath("060109_Mix1_calcurve_070.mzML");
            TestRemoval(importFilePath, copy1FilePath, importFilePath3, copyFilePath3, "0", "1");

            // Test partial suffix and prefix removal
            string importFilePath4 = TestFilesDir.GetTestPath("060109_Mix0_calcurve_070.mzML");
            string copyFilePath4 = TestFilesDir.GetTestPath("060109_Mix1_calcurve_070.mzML");
            TestRemoval(importFilePath, copy1FilePath, importFilePath4, copyFilePath4, "Mix0_calcurve", "Mix1_calcurve", 3, 9);
        }

        public void TestRemoval(string file1Src, string file2Src,
            string file1Dest, string file2Dest,
            string expectedResult1, string expectedResult2,
            int? prefixLength = null, int? suffixLength = null)
        {
            if (!File.Exists(file1Dest))
                File.Move(file1Src, file1Dest);
            if (!File.Exists(file2Dest))
                File.Move(file2Src, file2Dest);

            using (new WaitDocumentChange())
            {
                SkylineWindow.BeginInvoke(new Action(()=>SkylineWindow.ImportResults()));
                WaitForConditionUI(() => null != FindOpenForm<ImportResultsDlg>() || null != FindOpenForm<AlertDlg>());
                var importResultsDlg = FindOpenForm<ImportResultsDlg>();
                if (importResultsDlg == null)
                {
                    var alertDlg = FindOpenForm<AlertDlg>();
                    Assert.IsNotNull(alertDlg, "Expected to find either ImportResultsDlg or AlertDlg");
                    importResultsDlg = ShowDialog<ImportResultsDlg>(alertDlg.ClickNo);
                }
                RunUI(() =>
                {
                    importResultsDlg.NamedPathSets = DataSourceUtil.GetDataSources(TestFilesDir.FullPath).ToArray();
                });
                var removeSuffix = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
                if (prefixLength != null)
                {
                    string foundPrefix = string.Empty;
                    foundPrefix = removeSuffix.Prefix;
                    RunUI(() => removeSuffix.Prefix = foundPrefix.Substring(0, foundPrefix.Length - prefixLength.Value));
                }
                if (suffixLength != null)
                {
                    string foundSuffix = string.Empty;
                    foundSuffix = removeSuffix.Suffix;
                    RunUI(() => removeSuffix.Suffix = foundSuffix.Substring(suffixLength.Value));
                }
                OkDialog(removeSuffix, () => removeSuffix.YesDialog());
            }
            WaitForDocumentLoaded();
            Assert.AreEqual(expectedResult1, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[0].Name);
            Assert.AreEqual(expectedResult2, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[1].Name);
            RunUI(SkylineWindow.Undo);
        }
    }
}