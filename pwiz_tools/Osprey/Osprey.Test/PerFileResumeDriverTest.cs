/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *                Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="PerFileResumeDriver"/>, the per-file resume test every task
    /// that reuses outputs file by file asks.
    /// </summary>
    [TestClass]
    public class PerFileResumeDriverTest
    {
        private const string TASK = "PerFileResumeDriverTest";

        /// <summary>
        /// IsCurrent reads the stamp the output carries inside itself: the matching task and key
        /// by this build are current, anything else is not, an output without a stamp never is,
        /// and rewriting the output replaces its stamp in the same commit - so there is nothing
        /// to clear before a recompute and nothing to stamp after it.
        /// </summary>
        [TestMethod]
        public void TestIsCurrentReadsTheEmbeddedStamp()
        {
            string dir = Path.Combine(Path.GetTempPath(), "resume_driver_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "run" + FdrScoresSidecar.EXT_FIRST_PASS);
                var records = new List<FdrScoreRecord> { new FdrScoreRecord(1, 0.5, 0.01, 0.01, 10.0) };

                // Present but unstamped (a foreign or legacy file) -> not current.
                File.WriteAllText(output, "not a sidecar");
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(output, TASK, "key1"));
                File.Delete(output);

                FdrScoresSidecar.BeginRun();
                FdrScoresSidecar.Write(output, records, FdrScoresSidecar.Pass.FirstPass,
                    ArtifactStamp.ForCurrentBuild(TASK, "key1"));
                Assert.IsTrue(PerFileResumeDriver.IsCurrent(output, TASK, "key1"));
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(output, TASK, "key2"));
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(output, TASK + "Other", "key1"));

                // A later run's write replaces the stamp with its own.
                FdrScoresSidecar.BeginRun();
                FdrScoresSidecar.Write(output, records, FdrScoresSidecar.Pass.FirstPass,
                    ArtifactStamp.ForCurrentBuild(TASK, "key2"));
                Assert.IsTrue(PerFileResumeDriver.IsCurrent(output, TASK, "key2"));
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(output, TASK, "key1"));

                // Deleted -> not current, and asking does not throw.
                File.Delete(output);
                Assert.IsFalse(PerFileResumeDriver.IsCurrent(output, TASK, "key2"));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }
    }
}
