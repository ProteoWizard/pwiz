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
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises driving the main Skyline window's menus through the AI Connector. The View > Libraries >
    /// Ion Types submenu is not built from ordinary menu items: it hosts an <c>IonTypeSelectionPanel</c>
    /// (a ToolStripControlHost) whose ion types (A/B/C/X/Y/Z...) are checkbox-buttons. The test verifies that
    /// (1) the main window is now discoverable/resolvable as a form, and (2) the menu walk descends into the
    /// hosted control so a button like "B" can be clicked by its visible text -- toggling ShowBIons.
    /// </summary>
    [TestClass]
    public class IonTypeMenuConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestIonTypeMenuConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // The Ion Types submenu is only shown for proteomic documents; enable it the way showing a
            // proteomic spectrum does (a loaded peptide document has already done this in normal use).
            // The verbs are driven through the running JSON tool server (torn down with the window).
            RunUI(() =>
            {
                SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.proteomic);
                SkylineWindow.ViewMenu.EnableProteomicIons(true);
                Program.StartToolService();
            });

            // Change 1: the main window is now discoverable through GetOpenForms and resolvable as a form.
            var mainWindowForm = JsonUiService.GetOpenForms()
                .FirstOrDefault(form => form.Type == nameof(SkylineWindow));
            Assert.IsNotNull(mainWindowForm, @"The main Skyline window should be listed by GetOpenForms.");
            var mainWindowPath = new UiElementPath(null, mainWindowForm.Id, null, @"Form");
            // Resolving it and walking its controls should not throw and should find the main menu strip.
            var children = (ControlInfo[]) JsonUiService.PerformAction(mainWindowPath, @"get_children", null);
            Assert.IsTrue(children.Length > 0, @"get_children on the main window should return its controls.");

            // get_children can now inspect the Ion Types submenu: EnumerateChildren opens the dropdown so its
            // on-demand panel is built, and the hosted IonTypeSelectionPanel is flattened so the ion-type
            // buttons surface as direct children -- listed by their text (A/B/C for N-term, X/Y/Z for C-term).
            var menuStrip = new UiElementPath(mainWindowPath, null, null, @"MenuStrip");
            var ionTypes = new UiElementPath(
                new UiElementPath(new UiElementPath(menuStrip, @"View", null, null), @"Libraries", null, null),
                @"Ion Types", null, null);
            var ionButtons = (ControlInfo[]) JsonUiService.PerformAction(ionTypes, @"get_children", null);
            var ionButtonLabels = ionButtons.Select(button => button.Path?.Text).ToArray();
            foreach (var ionType in new[] { @"A", @"B", @"C", @"X", @"Y", @"Z" })
                CollectionAssert.Contains(ionButtonLabels, ionType,
                    $@"get_children of the Ion Types submenu should list the '{ionType}' ion button.");

            // Change 2: the menu walk reaches the ion-type checkbox hosted in the Ion Types submenu and
            // clicking it toggles the backing setting. Start from a known state so the toggle has direction.
            RunUI(() => Settings.Default.ShowBIons = false);
            Program.MainJsonToolServer.ClickMainMenuItem(@"View > Libraries > Ion Types > B");
            WaitForConditionUI(() => Settings.Default.ShowBIons);
            RunUI(() => Assert.IsTrue(Settings.Default.ShowBIons,
                @"Clicking the hosted 'B' ion-type button did not turn b-ions on."));

            // Clicking it again toggles it back off (the button is a checkbox).
            Program.MainJsonToolServer.ClickMainMenuItem(@"View > Libraries > Ion Types > B");
            WaitForConditionUI(() => !Settings.Default.ShowBIons);
            RunUI(() => Assert.IsFalse(Settings.Default.ShowBIons,
                @"Clicking the hosted 'B' ion-type button again did not turn b-ions back off."));
        }
    }
}
