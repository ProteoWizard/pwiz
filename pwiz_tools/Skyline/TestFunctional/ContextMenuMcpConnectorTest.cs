/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises driving a graph's right-click context menu through a <see cref="UiElementPath"/> whose Type
    /// is "ContextMenu" (a menu that, unlike the main menu, is built on demand by the graph's
    /// ContextMenuBuilder). It toggles the Peak Areas graph's "Log Scale" item and verifies the backing
    /// setting changed. The item is matched by its visible text, and the graph is located the way the
    /// connector would -- via <see cref="JsonToolServer.GetOpenForms"/>.
    /// </summary>
    [TestClass]
    public class ContextMenuMcpConnectorTest : McpConnectorTest
    {
        [TestMethod]
        public void TestContextMenuMcpConnector()
        {
            TestFilesZip = @"TestFunctional\PeakAreaDotpGraphTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Every verb below is driven through the running JSON tool server (torn down with the window).
            StartToolService();

            RunUI(() => SkylineWindow.OpenFile(
                TestFilesDir.GetTestPath("ABSciex4000_Study9-1_Site19_CalCurves only.sky")));
            WaitForDocumentLoaded();

            // Show the Peak Areas - Replicate Comparison graph (its context menu has the per-peptide
            // "Log Scale" item), select a peptide so the graph has content, and start from log scale
            // off so the toggle has a definite direction.
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 0);
                Settings.Default.AreaLogScale = false;
                SkylineWindow.UpdatePeakAreaGraph();
            });
            WaitForGraphs();

            // Find the graph the way the AI McpConnector would -- by enumerating open forms -- then drive its
            // context menu through a path: the graph form's context menu (Type "ContextMenu"), then its
            // item by its visible text.
            string graphId = McpConnector.GetOpenForms()
                .First(form => form.Type == @"GraphSummary" && form.HasGraph).Id;
            var contextMenu = new UiElementPath(
                new UiElementPath(null, graphId, null, @"Form"), null, null, @"ContextMenu");
            var logScaleItem = new UiElementPath(contextMenu, @"Log Scale", null, null);
            McpConnector.PerformAction(logScaleItem, @"click", null);

            // The menu item has CheckOnClick=true, so the click flipped it from unchecked to checked,
            // turning the log scale on.
            WaitForConditionUI(() => Settings.Default.AreaLogScale);
            RunUI(() => Assert.IsTrue(Settings.Default.AreaLogScale,
                @"Clicking the context-menu item did not toggle the Peak Areas Log Scale setting."));
        }
    }
}
