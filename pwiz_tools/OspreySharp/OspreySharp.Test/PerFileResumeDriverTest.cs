/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="PerFileResumeDriver"/>, the per-file resume
    /// sidecar mechanics shared by PerFileScoringTask and PerFileRescoreTask.
    /// </summary>
    [TestClass]
    public class PerFileResumeDriverTest
    {
        private const string TASK = "PerFileResumeDriverTest";
        private const string VERSION = "26.1.1.0";

        /// <summary>
        /// IsCurrent must require BOTH the output file on disk AND a matching
        /// validity sidecar; Stamp writes that sidecar and ClearStale removes it.
        /// </summary>
        [TestMethod]
        public void TestIsCurrentStampAndClearStale()
        {
            string outputPath = Path.GetTempFileName();
            string sidecarPath = TaskValiditySidecar.PathFor(outputPath, TASK);
            var warnings = new List<string>();
            try
            {
                // Output exists but no sidecar yet -> not current.
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(outputPath, TASK, "key1"));

                // After a successful stamp the matching key is current; a
                // different key is not (a foreign invocation's output).
                PerFileResumeDriver.Stamp(outputPath, TASK, VERSION, "key1",
                    new[] { "input.mzML" }, warnings.Add);
                Assert.AreEqual(0, warnings.Count);
                Assert.IsTrue(PerFileResumeDriver.IsCurrent(outputPath, TASK, "key1"));
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(outputPath, TASK, "key2"));

                // The file-existence gate: a valid sidecar without its output is
                // NOT current (the sidecar can outlive a deleted output).
                File.Delete(outputPath);
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(outputPath, TASK, "key1"));

                // ClearStale removes the sidecar so the next probe re-runs.
                File.WriteAllText(outputPath, "rebuilt");
                Assert.IsTrue(PerFileResumeDriver.IsCurrent(outputPath, TASK, "key1"));
                PerFileResumeDriver.ClearStale(outputPath, TASK);
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(outputPath, TASK, "key1"));
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                if (File.Exists(sidecarPath))
                    File.Delete(sidecarPath);
            }
        }

        /// <summary>
        /// A sidecar-write failure is non-fatal: Stamp must route it to the
        /// warning callback and not throw (the output is already on disk; only
        /// the resume-skip hint is lost). Writing under a non-existent directory
        /// forces the failure.
        /// </summary>
        [TestMethod]
        public void TestStampSwallowsWriteFailure()
        {
            string missingDir = Path.Combine(Path.GetTempPath(),
                "osprey-resume-driver-missing-dir", "out.parquet");
            var warnings = new List<string>();

            PerFileResumeDriver.Stamp(missingDir, TASK, VERSION, "key1",
                new[] { "input.mzML" }, warnings.Add);

            Assert.AreEqual(1, warnings.Count);
        }
    }
}
