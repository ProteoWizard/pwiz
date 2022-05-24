/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2021 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Bruker PASEF from command-line (which we can test with Wine/Docker)
    /// </summary>
    [TestClass]
    public class CommandLinePerfImportDiaPasefTest : AbstractUnitTestEx
    {
        private bool IsRecordMode { get { return false; } }

        [TestMethod]
        public void ConsoleBrukerDiaPasefImportTest()
        {
            if (SkipForResharperAnalysis())
                return;

            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();
            TestFilesZipPaths = new[]
            {
                GetPerfTestDataURL(@"PerfImportBrukerDiaPasef_v2.zip"),
                "http://skyline.ms/tutorials/DIA-PASEF-small.zip"
            };
            TestFilesPersistent = new[] { ".d", "DIA-PASEF-small\\DDA_search", "DIA-PASEF-small\\DIA" }; // List of file basenames that we'd like to unzip alongside parent zipFile, and (re)use in place

            // Unzip test files.
            if (TestFilesZipPaths != null)
            {
                TestFilesDirs = new TestFilesDir[TestFilesZipPaths.Length];
                for (int i = 0; i < TestFilesZipPaths.Length; i++)
                {
                    TestFilesDirs[i] = new TestFilesDir(TestContext, TestFilesZipPaths[i], TestDirectoryName,
                        TestFilesPersistent, IsExtractHere(i));
                }
            }

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // Turn on performance measurement

            // Load a .sky with missing .skyd, forcing re-import with existing parameters
            // This simplifies the test code since we have six different PASEF modes to deal with here
            string skyfile = TestFilesDirs[0].GetTestPath("snipped.sky");

            // Update the paths to the .d files mentioned in the skyline doc
            string text = File.ReadAllText(skyfile);
            text = text.Replace(@"PerfImportBrukerDiaPasef", TestFilesDirs[0].PersistentFilesDir);
            File.WriteAllText(skyfile, text);

            var resultFilePaths = new[]
            {
                TestFilesDirs[0].GetTestPath("190314_TEN_175mingr_7-35_500nL_HeLa_diaPASEFsingle_py1_MSMS_Slot1-10_1_3425.d"),
                TestFilesDirs[0].GetTestPath("190314_TEN_175mingr_7-35_500nL_HeLa_diaPASEFsingle_py2_MSMS_Slot1-10_1_3427.d"),
                TestFilesDirs[0].GetTestPath("190314_TEN_175mingr_7-35_500nL_HeLa_diaPASEFdouble_py3_MSMS_Slot1-10_1_3426.d"),
                TestFilesDirs[0].GetTestPath("190314_TEN_175mingr_7-35_500nL_HeLa_AIF_MSMS_Slot1-10_1_3423.d"),
                TestFilesDirs[1].GetTestPath("DIA-PASEF-small\\DIA\\A210331_bcc_1180_lfqbA_17min_dia_200ng.d"),
                TestFilesDirs[1].GetTestPath("DIA-PASEF-small\\DIA\\A210331_bcc_1181_lfqbB_17min_dia_200ng.d"),
            };
            var outPath = TestFilesDirs[0].GetTestPath("import-results.sky");

            var args = new List<string>
            {
                "--in=" + skyfile,
                "--out=" + outPath,
                "--remove-all"
            };
            foreach(var resultFilePath in resultFilePaths)
                args.Add("--import-file=" + resultFilePath);

            var output = RunCommand(args.ToArray());
            AssertEx.Contains(output, TextUtil.LineSeparate(Resources.CommandLine_ImportResultsFile_Adding_results___));

            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(6, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("190314_TEN_175mingr_7-35_500nL_HeLa_diaPASEFsingle_py1_MSMS_Slot1-10_1_3425"));
            Assert.AreEqual(1, doc.PeptideGroupCount);
            Assert.AreEqual(34, doc.PeptideCount);
            Assert.AreEqual(34, doc.PeptideTransitionGroupCount);
            Assert.AreEqual(204, doc.PeptideTransitionCount);
        }
    }
}
