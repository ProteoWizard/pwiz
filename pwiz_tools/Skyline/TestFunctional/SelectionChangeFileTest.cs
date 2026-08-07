/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies the file Skyline rewrites when the selection changes - the way a listening process is told to ask
    /// again, without Skyline knowing that process exists. It is watched here the way a listener would watch it,
    /// with a <see cref="FileSystemWatcher"/>.
    /// </summary>
    [TestClass]
    public class SelectionChangeFileTest : McpConnectorTest
    {
        // Generous: the write is coalesced onto a timer, so it trails the change it reports.
        private const int WATCH_TIMEOUT_MILLIS = 30 * 1000;

        [TestMethod]
        public void TestSelectionChangeFile()
        {
            TestFilesZip = @"TestFunctional\FilesTreeFormTest.data";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(@"Rat_plasma.sky");
            StartToolService();
            string filePath = JsonToolConstants.GetSelectionChangeFilePath(Program.MainJsonToolServer.PipeName);

            using (var watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath) ?? string.Empty,
                       Path.GetFileName(filePath)))
            using (var changed = new ManualResetEventSlim(false))
            {
                watcher.Changed += (sender, args) => changed.Set();
                watcher.Created += (sender, args) => changed.Set();
                watcher.EnableRaisingEvents = true;

                Select((int) SrmDocument.Level.Molecules, 0);
                AssertEx.IsTrue(changed.Wait(WATCH_TIMEOUT_MILLIS));
                AssertEx.FileExists(filePath);

                // A second change is a second notification: the file is rewritten every time, not just created.
                changed.Reset();
                Select((int) SrmDocument.Level.Molecules, 1);
                AssertEx.IsTrue(changed.Wait(WATCH_TIMEOUT_MILLIS));
            }
        }

        /// <summary>
        /// Selects a node, having first made sure it is not the one already selected - setting the selection to
        /// where it already is changes nothing, and so quite rightly tells nobody anything.
        /// </summary>
        private void Select(int level, int index)
        {
            RunUI(() =>
            {
                var path = SkylineWindow.Document.GetPathTo(level, index);
                AssertEx.IsFalse(Equals(path, SkylineWindow.SelectedPath));
                SkylineWindow.SelectedPath = path;
                AssertEx.AreEqual(path, SkylineWindow.SelectedPath);
            });
        }
    }
}
