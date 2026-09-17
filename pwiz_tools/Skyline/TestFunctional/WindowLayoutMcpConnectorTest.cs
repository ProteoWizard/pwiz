/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.SkylineTestUtil;
using SkylineTool;
using Rectangle = System.Drawing.Rectangle;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the connector's window layout verbs (<see cref="IJsonToolService.SetWindowState"/>,
    /// <see cref="IJsonToolService.SetWindowBounds"/> and <see cref="IJsonToolService.GetLayout"/>) on the two kinds
    /// of window they place. The main window: a state is set and read back, a read does not change it, bounds
    /// restore a maximized window to Normal, "maximize" fills the screen WITHOUT the Maximized state (which would
    /// refuse every later resize), and "center" centers. A dockable window (the Document Grid): bounds move its
    /// floating frame, a dock state docks it and bounds then size its side, a place relative to the Targets view
    /// splits beside it or joins its tab group and the layout reports it, and it floats again and hides.
    /// </summary>
    [TestClass]
    public class WindowLayoutMcpConnectorTest : McpConnectorTest
    {
        private const int WIDTH = 800;
        private const int HEIGHT = 500;
        private const int DOCK_WIDTH = 320;

        [TestMethod]
        public void TestWindowLayoutMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();
            TestMainWindow();
            TestDockableWindow();
        }

        private void TestMainWindow()
        {
            // A state is set and read back; a read of the bounds must not un-maximize the window to do so.
            var window = McpConnector.SetWindowState(null, nameof(FormWindowState.Maximized));
            AssertEx.AreEqual(nameof(FormWindowState.Maximized), window.State);
            window = McpConnector.SetWindowBounds();
            AssertEx.AreEqual(nameof(FormWindowState.Maximized), window.State);
            RunUI(() => AssertEx.AreEqual(FormWindowState.Maximized, SkylineWindow.WindowState,
                "Reading the bounds un-maximized the window."));
            AssertMatchesForm(window, SkylineWindow);

            // Bounds restore the window to Normal and land exactly where asked.
            var screen = GetScreenBounds(SkylineWindow);
            var requested = new Rectangle(screen.Left + 20, screen.Top + 30, WIDTH, HEIGHT);
            window = McpConnector.SetWindowBounds(null, ToRectangle(requested));
            AssertEx.AreEqual(nameof(FormWindowState.Normal), window.State);
            AssertEx.AreEqual(requested, ToRectangle(window.Bounds));
            AssertMatchesForm(window, SkylineWindow);

            // "maximize" fills the screen as a Normal window. The state first: a genuinely maximized window
            // overhangs its screen by the invisible border, so a regression reports the state, not an off-by-eight.
            window = McpConnector.SetWindowBounds(null, null, WindowLayout.PLACEMENT_MAXIMIZE);
            AssertEx.AreEqual(nameof(FormWindowState.Normal), window.State);
            AssertEx.AreEqual(screen, ToRectangle(window.Bounds));
            AssertMatchesForm(window, SkylineWindow);

            // "center" centers the requested size on the screen.
            window = McpConnector.SetWindowBounds(null, ToRectangle(new Rectangle(0, 0, WIDTH, HEIGHT)),
                WindowLayout.PLACEMENT_CENTER);
            var centered = new Rectangle(screen.Left + (screen.Width - WIDTH) / 2,
                screen.Top + (screen.Height - HEIGHT) / 2, WIDTH, HEIGHT);
            AssertEx.AreEqual(centered, ToRectangle(window.Bounds));
            AssertMatchesForm(window, SkylineWindow);

            // Requests the main window cannot honor are refused and leave it where it was.
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowBounds(null, null, @"sideways");
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowState(null, nameof(DockState.DockLeft));
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowState(null, null, @"SequenceTreeForm:Targets", WindowLayout.RELATION_TAB);
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowState(null, nameof(FormWindowState.Normal), @"SequenceTreeForm:Targets");
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowState();
            });
            AssertEx.AreEqual(centered, ToRectangle(McpConnector.SetWindowBounds().Bounds));
        }

        private void TestDockableWindow()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            WaitForMcpConnectorForm<DocumentGridForm>();
            string targetsId = GetOpenFormId<SequenceTreeForm>();
            var grid = FindOpenForm<DocumentGridForm>();
            var targets = FindOpenForm<SequenceTreeForm>();

            // Shown floating; a read reports the floating frame, and bounds move it.
            var window = McpConnector.SetWindowBounds(GridId());
            AssertEx.AreEqual(nameof(DockState.Floating), window.State);
            RunUI(() => AssertEx.AreEqual(grid.ParentForm.Bounds, ToRectangle(window.Bounds)));
            var screen = GetScreenBounds(grid);
            var requested = new Rectangle(screen.Left + 40, screen.Top + 50, WIDTH, HEIGHT);
            window = McpConnector.SetWindowBounds(GridId(), ToRectangle(requested));
            AssertEx.AreEqual(requested, ToRectangle(window.Bounds));
            RunUI(() => AssertEx.AreEqual(requested, grid.ParentForm.Bounds));

            // A dock state docks it as the first pane of that side; bounds then size the side.
            window = McpConnector.SetWindowState(GridId(), nameof(DockState.DockRight));
            AssertEx.AreEqual(nameof(DockState.DockRight), window.State);
            Assert.IsNull(window.SplitFrom, @"The only pane on a side should not report a split.");
            RunUI(() => AssertEx.AreEqual(DockState.DockRight, grid.DockState));
            window = McpConnector.SetWindowBounds(GridId(), ToRectangle(new Rectangle(0, 0, DOCK_WIDTH, 0)));
            RunUI(() =>
            {
                AssertEx.AreEqual(DOCK_WIDTH, (int) SkylineWindow.DockPanel.DockRightPortion);
                AssertEx.AreEqual(grid.Pane.RectangleToScreen(grid.Pane.ClientRectangle), ToRectangle(window.Bounds));
            });

            // A place relative to another window splits beside it, and the reply and the layout both say so ...
            window = McpConnector.SetWindowState(GridId(), null, targetsId, WindowLayout.RELATION_BOTTOM);
            AssertEx.AreEqual(targets.DockState.ToString(), window.State);
            AssertEx.AreEqual(targetsId, window.SplitFrom);
            AssertEx.AreEqual(nameof(DockPaneAlignment.Bottom), window.SplitSide);
            AssertEx.AreEqual(0.5, window.SplitShare);
            RunUI(() =>
            {
                AssertEx.IsFalse(ReferenceEquals(targets.Pane, grid.Pane),
                    "Splitting beside the Targets view should give the grid a pane of its own.");
                AssertEx.IsTrue(grid.Pane.Top > targets.Pane.Top, "The grid should sit below the Targets view.");
            });
            var layout = McpConnector.GetLayout();
            AssertEx.AreEqual(SkylineWindow.WindowState.ToString(), layout.MainWindow.State);
            var leftArea = layout.Areas.FirstOrDefault(area => area.State == targets.DockState.ToString());
            Assert.IsNotNull(leftArea, @"The layout should list the side the Targets view is docked to: " + DescribeLayout(layout));
            var gridPane = leftArea.Panes.FirstOrDefault(pane => pane.Tabs.Contains(GridId()));
            Assert.IsNotNull(gridPane, @"The layout should list the grid's pane on that side: " + DescribeLayout(layout));
            AssertEx.AreEqual(targetsId, gridPane.SplitFrom);
            AssertEx.AreEqual(nameof(DockPaneAlignment.Bottom), gridPane.SplitSide);
            Assert.IsNull(leftArea.Panes.First(pane => pane.Tabs.Contains(targetsId)).SplitFrom,
                @"The first pane on the side should not report a split.");

            // ... or joins its tab group.
            window = McpConnector.SetWindowState(GridId(), null, targetsId);
            CollectionAssert.Contains(window.Tabs, targetsId);
            CollectionAssert.Contains(window.Tabs, GridId());
            RunUI(() => AssertEx.IsTrue(ReferenceEquals(targets.Pane, grid.Pane),
                "Joining the Targets view's tab group should put the grid in its pane."));

            // Floating again (at the default place), then sized; then hidden.
            window = McpConnector.SetWindowState(GridId(), nameof(DockState.Floating));
            AssertEx.AreEqual(nameof(DockState.Floating), window.State);
            window = McpConnector.SetWindowBounds(GridId(), ToRectangle(requested));
            AssertEx.AreEqual(requested, ToRectangle(window.Bounds));
            RunUI(() => AssertEx.AreEqual(requested, grid.ParentForm.Bounds));
            window = McpConnector.SetWindowState(GridId(), nameof(DockState.Hidden));
            AssertEx.AreEqual(nameof(DockState.Hidden), window.State);
            RunUI(() => AssertEx.AreEqual(DockState.Hidden, grid.DockState));
            // A hidden window is not among the open forms, so it comes back the way a user brings it back: through
            // its View menu item, not by form id.
            Assert.IsFalse(McpConnector.GetOpenForms().Any(form => form.Type == nameof(DocumentGridForm)),
                @"A hidden window should not be listed among the open forms.");
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            WaitForMcpConnectorForm<DocumentGridForm>();

            // Requests a dockable window cannot honor.
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowState(GridId(), nameof(FormWindowState.Maximized));
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowState(GridId(), null, targetsId, @"sideways");
            });
            // The document area is sized by its splits: bounds and placements there are refused.
            McpConnector.SetWindowState(GridId(), nameof(DockState.Document));
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowBounds(GridId(), ToRectangle(requested));
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowBounds(GridId(), null, WindowLayout.PLACEMENT_CENTER);
            });
        }

        // The grid's form id right now: a form id carries the title, and the Document Grid's title changes once
        // it shows a report ("Document Grid" -> "Document Grid: Proteins"), so it is looked up at each step.
        private string GridId() => GetOpenFormId<DocumentGridForm>();

        // The layout as one line per pane, for a failure message.
        private static string DescribeLayout(LayoutInfo layout)
        {
            return string.Join(@" | ", layout.Areas.Select(area => area.State + @": " + string.Join(@"; ",
                area.Panes.Select(pane => @"[" + string.Join(@", ", pane.Tabs) + @"]" +
                                          (pane.SplitFrom == null ? string.Empty : @" from " + pane.SplitFrom + @" " + pane.SplitSide)))));
        }

        // The reported bounds and screen are what the form itself reports.
        private void AssertMatchesForm(WindowInfo window, Form form)
        {
            RunUI(() =>
            {
                AssertEx.AreEqual(form.Bounds, ToRectangle(window.Bounds));
                AssertEx.AreEqual(Screen.FromControl(form).Bounds, ToRectangle(window.Screen));
            });
        }

        private Rectangle GetScreenBounds(Control control)
        {
            var screen = Rectangle.Empty;
            RunUI(() => screen = Screen.FromControl(control).Bounds);
            return screen;
        }

        private static Rectangle ToRectangle(SkylineTool.Rectangle r)
        {
            return Rectangle.FromLTRB((int) r.Left, (int) r.Top, (int) r.Right, (int) r.Bottom);
        }

        private static SkylineTool.Rectangle ToRectangle(Rectangle r)
        {
            return new SkylineTool.Rectangle { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom };
        }
    }
}
