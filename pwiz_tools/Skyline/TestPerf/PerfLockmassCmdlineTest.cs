/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify operation of Waters lockmass correction from the commandline.
    /// </summary>
    [TestClass]
    public class LockmassCmdLineTest : AbstractUnitTest
    {

        [TestMethod]
        public void WatersLockmassCmdlinePerfTest()
        {
            if (IsPerfTest && !RunPerfTests)
            {
                return; // Don't want to run this lengthy test right now
            }

            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfTestLockmass.zip";
            TestFilesPersistent = new[] { "ID19638_01_UCA195_2533_082715.raw" }; // List of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZip, "CmdlineTest", TestFilesPersistent);

            var skyfile = GetTestPath("2533_FattyAcids.sky");

            var rawPath = GetTestPath(TestFilesPersistent[0]);
            const double lockmassNegative = 554.2615;
                
            // Exercise the commandline
            var outPathUncorrected = TestFilesDir.GetTestPath("cmdlineTestUncorrected.sky");
            var outPathCorrected = TestFilesDir.GetTestPath("cmdlineTestCorrected.sky");
            RunCommand("--in=" + skyfile,
                "--import-file=" + rawPath,
                "--out=" + outPathUncorrected);
            FileEx.SafeDelete(Path.ChangeExtension(skyfile, ChromatogramCache.EXT));
            var cmdDocUncorrected = ResultsUtil.DeserializeDocument(outPathUncorrected);
            RunCommand("--in=" + skyfile,
                "--import-file=" + rawPath,
                "--import-lockmass-negative=" + lockmassNegative,
                "--out=" + outPathCorrected);
            var cmdDocCorrected = ResultsUtil.DeserializeDocument(outPathCorrected);
            ComparePeaks(cmdDocCorrected, cmdDocUncorrected);    
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(relativePath);
        }

        private static void RunCommand(params string[] inputArgs)
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new CommandStatusWriter(new StringWriter(consoleBuffer));
            CommandLineRunner.RunCommand(inputArgs, consoleOutput);
        }

        private static List<TransitionGroupChromInfo> Peaks(SrmDocument doc)
        {
            return (from tg in doc.MoleculeTransitionGroups
                       from r in tg.Results
                       from p in r
                       select p).ToList();
        }

        private static void ComparePeaks(SrmDocument corrected, SrmDocument uncorrected)
        {
            var correctedPeaks = Peaks(corrected);
            var uncorrectedPeaks = Peaks(uncorrected);
            Assert.AreEqual(uncorrectedPeaks.Count, correctedPeaks.Count);
            var nWorse = 0;
            for (var i = 0; i < correctedPeaks.Count; i++)
            {
                var correctedPeak = correctedPeaks[i];
                var uncorrectedPeak = uncorrectedPeaks[i];
                Assert.AreEqual(uncorrectedPeak.RetentionTime ?? -1, correctedPeak.RetentionTime ?? -1, 0.01,
                    "peak retention times should be similar"); // Expect similar RT
                if (Math.Abs(uncorrectedPeak.MassError ?? 0) < Math.Abs(correctedPeak.MassError ?? 0))
                    nWorse++;
            }
            Assert.IsTrue(nWorse < (3 * correctedPeaks.Count) / 10, "mass error should be reduced"); // Expect overall lower mass error
        }
    }
}
