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
    /// Exercises the connector's window placement verb (<see cref="IJsonToolService.SetWindowPlacement"/>) on the
    /// two kinds of window it places. The main window: an empty request reads without changing the window state,
    /// bounds restore a maximized window to Normal, "maximize" fills the screen WITHOUT the Maximized state (which
    /// would refuse every later resize), "center" centers, and WindowState sets the real state. A dockable window
    /// (the Document Grid): bounds move its floating frame, a dock state docks it, bounds then size its side,
    /// RelativeTo splits beside or joins the Targets view, and it floats again at a rectangle.
    /// </summary>
    [TestClass]
    public class WindowPlacementMcpConnectorTest : McpConnectorTest
    {
        private const int WIDTH = 800;
        private const int HEIGHT = 500;
        private const int DOCK_WIDTH = 320;

        [TestMethod]
        public void TestWindowPlacementMcpConnector()
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
            // An empty request reads, and must not un-maximize the window to do so.
            RunUI(() => SkylineWindow.WindowState = FormWindowState.Maximized);
            var placement = McpConnector.SetWindowPlacement();
            AssertEx.AreEqual(nameof(FormWindowState.Maximized), placement.WindowState);
            RunUI(() => AssertEx.AreEqual(FormWindowState.Maximized, SkylineWindow.WindowState,
                "Reading the placement un-maximized the window."));
            AssertMatchesForm(placement, SkylineWindow);

            // Bounds restore the window to Normal and land exactly where asked.
            var screen = GetScreenBounds(SkylineWindow);
            var requested = new Rectangle(screen.Left + 20, screen.Top + 30, WIDTH, HEIGHT);
            placement = McpConnector.SetWindowPlacement(null, new WindowPlacement { Bounds = ToRectangle(requested) });
            AssertEx.AreEqual(nameof(FormWindowState.Normal), placement.WindowState);
            AssertEx.AreEqual(requested, ToRectangle(placement.Bounds));
            AssertMatchesForm(placement, SkylineWindow);

            // "maximize" fills the screen as a Normal window. The state first: a genuinely maximized window
            // overhangs its screen by the invisible border, so a regression reports the state, not an off-by-eight.
            placement = McpConnector.SetWindowPlacement(null,
                new WindowPlacement { Placement = WindowPlacement.PLACEMENT_MAXIMIZE });
            AssertEx.AreEqual(nameof(FormWindowState.Normal), placement.WindowState);
            AssertEx.AreEqual(screen, ToRectangle(placement.Bounds));
            AssertMatchesForm(placement, SkylineWindow);

            // "center" centers the requested size on the screen.
            placement = McpConnector.SetWindowPlacement(null, new WindowPlacement
            {
                Bounds = ToRectangle(new Rectangle(0, 0, WIDTH, HEIGHT)),
                Placement = WindowPlacement.PLACEMENT_CENTER
            });
            var centered = new Rectangle(screen.Left + (screen.Width - WIDTH) / 2,
                screen.Top + (screen.Height - HEIGHT) / 2, WIDTH, HEIGHT);
            AssertEx.AreEqual(centered, ToRectangle(placement.Bounds));
            AssertMatchesForm(placement, SkylineWindow);

            // WindowState sets the real state.
            placement = McpConnector.SetWindowPlacement(null,
                new WindowPlacement { WindowState = nameof(FormWindowState.Maximized) });
            AssertEx.AreEqual(nameof(FormWindowState.Maximized), placement.WindowState);
            RunUI(() => AssertEx.AreEqual(FormWindowState.Maximized, SkylineWindow.WindowState));
            McpConnector.SetWindowPlacement(null, new WindowPlacement { Bounds = ToRectangle(requested) });

            // A request the main window cannot honor is refused and leaves it where it was.
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowPlacement(null, new WindowPlacement { Placement = @"sideways" });
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowPlacement(null, new WindowPlacement { DockState = nameof(DockState.DockLeft) });
            });
            AssertEx.AreEqual(requested, ToRectangle(McpConnector.SetWindowPlacement().Bounds));
        }

        private void TestDockableWindow()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            string gridId = WaitForMcpConnectorForm<DocumentGridForm>();
            string targetsId = GetOpenFormId<SequenceTreeForm>();
            var grid = FindOpenForm<DocumentGridForm>();
            var targets = FindOpenForm<SequenceTreeForm>();

            // Shown floating; a read reports the floating frame.
            var placement = McpConnector.SetWindowPlacement(gridId);
            AssertEx.AreEqual(nameof(DockState.Floating), placement.DockState);
            RunUI(() => AssertEx.AreEqual(grid.ParentForm.Bounds, ToRectangle(placement.Bounds)));

            // Bounds move the floating frame.
            var screen = GetScreenBounds(grid);
            var requested = new Rectangle(screen.Left + 40, screen.Top + 50, WIDTH, HEIGHT);
            placement = McpConnector.SetWindowPlacement(gridId, new WindowPlacement { Bounds = ToRectangle(requested) });
            AssertEx.AreEqual(requested, ToRectangle(placement.Bounds));
            RunUI(() => AssertEx.AreEqual(requested, grid.ParentForm.Bounds));

            // A dock state docks it; bounds then size its side.
            placement = McpConnector.SetWindowPlacement(gridId,
                new WindowPlacement { DockState = nameof(DockState.DockRight) });
            AssertEx.AreEqual(nameof(DockState.DockRight), placement.DockState);
            RunUI(() => AssertEx.AreEqual(DockState.DockRight, grid.DockState));
            placement = McpConnector.SetWindowPlacement(gridId,
                new WindowPlacement { Bounds = ToRectangle(new Rectangle(0, 0, DOCK_WIDTH, 0)) });
            RunUI(() =>
            {
                AssertEx.AreEqual(DOCK_WIDTH, (int) SkylineWindow.DockPanel.DockRightPortion);
                AssertEx.AreEqual(grid.Pane.RectangleToScreen(grid.Pane.ClientRectangle), ToRectangle(placement.Bounds));
            });

            // RelativeTo splits beside another window, on the side asked for ...
            placement = McpConnector.SetWindowPlacement(gridId, new WindowPlacement
            {
                RelativeTo = targetsId, Alignment = nameof(DockPaneAlignment.Bottom), Proportion = 0.4
            });
            AssertEx.AreEqual(targets.DockState.ToString(), placement.DockState);
            RunUI(() =>
            {
                AssertEx.IsFalse(ReferenceEquals(targets.Pane, grid.Pane),
                    "Splitting beside the Targets view should give the grid a pane of its own.");
                AssertEx.IsTrue(grid.Pane.Top > targets.Pane.Top, "The grid should sit below the Targets view.");
            });

            // ... or joins its tab group.
            McpConnector.SetWindowPlacement(gridId,
                new WindowPlacement { RelativeTo = targetsId, Alignment = WindowPlacement.ALIGNMENT_TAB });
            RunUI(() => AssertEx.IsTrue(ReferenceEquals(targets.Pane, grid.Pane),
                "Joining the Targets view's tab group should put the grid in its pane."));

            // Floating again, at a rectangle, in one request.
            placement = McpConnector.SetWindowPlacement(gridId,
                new WindowPlacement { DockState = nameof(DockState.Floating), Bounds = ToRectangle(requested) });
            AssertEx.AreEqual(nameof(DockState.Floating), placement.DockState);
            AssertEx.AreEqual(requested, ToRectangle(placement.Bounds));
            RunUI(() => AssertEx.AreEqual(requested, grid.ParentForm.Bounds));

            // Requests a dockable window cannot honor.
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowPlacement(gridId, new WindowPlacement { WindowState = nameof(FormWindowState.Maximized) });
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowPlacement(gridId, new WindowPlacement { DockState = nameof(DockState.Hidden) });
            });
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.SetWindowPlacement(gridId, new WindowPlacement { RelativeTo = targetsId, Alignment = @"sideways" });
            });
            AssertEx.AreEqual(requested, ToRectangle(McpConnector.SetWindowPlacement(gridId).Bounds));
        }

        // The reported bounds and screen are what the form itself reports.
        private void AssertMatchesForm(WindowPlacement placement, Form form)
        {
            RunUI(() =>
            {
                AssertEx.AreEqual(form.Bounds, ToRectangle(placement.Bounds));
                AssertEx.AreEqual(Screen.FromControl(form).Bounds, ToRectangle(placement.Screen));
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
