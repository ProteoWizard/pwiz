/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// A file that has finished importing must not be dragged backwards by a progress status that
    /// arrives late.
    /// <para>Progress statuses are immutable snapshots handed to the UI through the message queue,
    /// so an older one can be delivered after a newer one. When that happened to the last update of
    /// a file, the file sat at its final running percent - 99% - for the rest of the run, and
    /// <see cref="AllChromatogramsGraph.Finished"/> never became true because it requires every file
    /// to be complete, cancelled or in error. The import itself had finished correctly; only the
    /// display disagreed, which made it look like a hung import for 12 minutes.</para>
    /// </summary>
    [TestClass]
    public class FileProgressStaleStatusTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStaleProgressDoesNotRegressFinishedFile()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                var filePath = new MsDataFilePath(@"C:\test\stale.raw");
                using (var control = new FileProgressControl(new StubStateProvider()))
                {
                    control.FilePath = filePath;

                    var running = (ChromatogramLoadingStatus) new ChromatogramLoadingStatus(filePath, new[] { @"rep" })
                        .ChangePercentComplete(99);
                    control.SetStatus(running);
                    AssertEx.AreEqual(99, control.Progress);

                    // The file finishes.
                    var complete = (ChromatogramLoadingStatus) running.Complete();
                    control.SetStatus(complete);
                    AssertEx.AreEqual(100, control.Progress);

                    // A snapshot from the same progress chain arrives late. Applying it would leave
                    // this file below 100 with nothing left to move it forward.
                    control.SetStatus(running);
                    AssertEx.AreEqual(100, control.Progress,
                        "A stale status moved a finished file backwards.");

                    // A retry is a NEW progress chain, so it must still be able to restart the file.
                    var retried = (ChromatogramLoadingStatus) new ChromatogramLoadingStatus(filePath, new[] { @"rep" })
                        .ChangePercentComplete(10);
                    control.SetStatus(retried);
                    AssertEx.AreEqual(10, control.Progress,
                        "A retry could not restart a file that had already finished.");
                }
            });
        }

        private class StubStateProvider : FileProgressControl.IStateProvider
        {
            public DateTime Time { get { return DateTime.Now; } }
            public string PrepareErrorText(string errorText) { return errorText; }
            public int? GetFrozenProgress(MsDataFileUri filePath) { return null; }
            public bool IsProgressFrozen { get { return false; } }
        }
    }
}
