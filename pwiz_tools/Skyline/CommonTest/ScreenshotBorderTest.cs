/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.GUI;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    /// <summary>
    /// The window boundary repaint of a screen capture: what lay behind the window's rounded corners
    /// and blended frame is painted over, the interior is left as it was, and a capture whose border
    /// needs nothing (one color, or all white as a graph's is) comes back untouched. The corner shape
    /// depends on the Windows version the test runs on, as the repaint itself does.
    /// </summary>
    [TestClass]
    public class ScreenshotBorderTest : AbstractUnitTest
    {
        private const int WIDTH = 120;
        private const int HEIGHT = 90;
        private static readonly Color BORDER_COLOR = Color.FromArgb(0x70, 0x70, 0x70);
        private static readonly Color INTERIOR = Color.FromArgb(0xC8, 0xDC, 0xF0);
        private static readonly Color DIRT_EDGE = Color.FromArgb(0x10, 0x10, 0x10);
        private static readonly Color DIRT_CORNER = Color.FromArgb(0xE0, 0x20, 0x20);

        [TestMethod]
        public void TestCleanupBorder()
        {
            VerifyFormBorder();
            VerifyToolWindowBorder();
            VerifyUntouched();
        }

        /// <summary>
        /// A top-level form: the dirt along the edges and in the corners is gone, the middle of each
        /// edge is repainted, and the interior is untouched. On Windows 11 the corners are rounded,
        /// so the corner pixel itself is outside the window and white; before it the border is a
        /// plain rectangle in the standard color.
        /// </summary>
        private static void VerifyFormBorder()
        {
            using var bmp = CreateDirtyCapture();
            using var cleaned = bmp.CleanupBorder();
            AssertEdgesRepainted(cleaned, HEIGHT - 1);
            AssertEx.AreEqual(INTERIOR, cleaned.GetPixel(WIDTH / 2, HEIGHT / 2));
            AssertEx.AreEqual(INTERIOR, cleaned.GetPixel(2, HEIGHT / 2));
            AssertCorner(cleaned);
        }

        /// <summary>
        /// A floating tool window: on Windows 11 the whole boundary is repainted with the smaller
        /// corner radius; before it only the top edge is transparent, so only that line is repainted
        /// and the bottom edge keeps what the screen showed.
        /// </summary>
        private static void VerifyToolWindowBorder()
        {
            using var bmp = CreateDirtyCapture();
            using var cleaned = bmp.CleanupBorder(true);
            AssertEx.AreNotEqual(DIRT_EDGE, cleaned.GetPixel(WIDTH / 2, 0), @"the top edge of a tool window was not repainted");
            AssertEx.AreEqual(INTERIOR, cleaned.GetPixel(WIDTH / 2, HEIGHT / 2));
            if (ScreenshotBorder.IsWindows11())
            {
                AssertEdgesRepainted(cleaned, HEIGHT - 1);
                AssertCorner(cleaned);
            }
            else
            {
                AssertEx.AreEqual(BORDER_COLOR, cleaned.GetPixel(WIDTH / 2, 0));
                AssertEx.AreEqual(DIRT_EDGE, cleaned.GetPixel(WIDTH / 2, HEIGHT - 1));
            }
        }

        /// <summary>
        /// Nothing is drawn on a capture whose border is one color (there is no dirt to cover, and a
        /// curve would be drawn over a square frame) or all white (a graph), and the same object
        /// comes back.
        /// </summary>
        private static void VerifyUntouched()
        {
            using var oneColor = new Bitmap(WIDTH, HEIGHT);
            Fill(oneColor, INTERIOR, new Rectangle(0, 0, WIDTH, HEIGHT));
            AssertEx.IsTrue(ReferenceEquals(oneColor, oneColor.CleanupBorder()));
            AssertEx.AreEqual(INTERIOR, oneColor.GetPixel(0, 0));

            using var graph = new Bitmap(WIDTH, HEIGHT);
            Fill(graph, Color.White, new Rectangle(0, 0, WIDTH, HEIGHT));
            Fill(graph, INTERIOR, new Rectangle(WIDTH / 4, HEIGHT / 4, WIDTH / 2, HEIGHT / 2));
            AssertEx.IsTrue(ReferenceEquals(graph, graph.CleanupBorder()));
            AssertEx.AreEqual(Color.White.ToArgb(), graph.GetPixel(0, 0).ToArgb());
        }

        /// <summary>
        /// What a screen copy of a Windows 11 form looks like: the interior color, a one pixel frame
        /// in another color, and the background showing through in the rounded corners.
        /// </summary>
        private static Bitmap CreateDirtyCapture()
        {
            var bmp = new Bitmap(WIDTH, HEIGHT);
            Fill(bmp, DIRT_EDGE, new Rectangle(0, 0, WIDTH, HEIGHT));
            Fill(bmp, INTERIOR, new Rectangle(1, 1, WIDTH - 2, HEIGHT - 2));
            const int corner = ScreenshotBorder.CORNER_FORM_WINDOWS11;
            Fill(bmp, DIRT_CORNER, new Rectangle(0, 0, corner, corner));
            Fill(bmp, DIRT_CORNER, new Rectangle(WIDTH - corner, 0, corner, corner));
            Fill(bmp, DIRT_CORNER, new Rectangle(0, HEIGHT - corner, corner, corner));
            Fill(bmp, DIRT_CORNER, new Rectangle(WIDTH - corner, HEIGHT - corner, corner, corner));
            return bmp;
        }

        private static void Fill(Bitmap bmp, Color color, Rectangle rect)
        {
            using var g = Graphics.FromImage(bmp);
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, rect);
        }

        /// <summary>
        /// The middle of the top and bottom edges no longer shows the screen's frame color. The
        /// repaint is antialiased on Windows 11, so the exact value is not asserted there.
        /// </summary>
        private static void AssertEdgesRepainted(Bitmap cleaned, int bottom)
        {
            foreach (int y in new[] { 0, bottom })
            {
                var pixel = cleaned.GetPixel(WIDTH / 2, y);
                AssertEx.AreNotEqual(DIRT_EDGE, pixel, @"the edge still shows the screen's frame color at y = " + y);
                if (!ScreenshotBorder.IsWindows11())
                    AssertEx.AreEqual(BORDER_COLOR, pixel);
            }
        }

        /// <summary>The corner pixel: white outside a rounded corner, the border itself on a square one.</summary>
        private static void AssertCorner(Bitmap cleaned)
        {
            var corner = cleaned.GetPixel(0, 0);
            AssertEx.AreNotEqual(DIRT_CORNER, corner, @"the corner still shows what lay behind the window");
            AssertEx.AreEqual(ScreenshotBorder.IsWindows11() ? Color.White.ToArgb() : BORDER_COLOR.ToArgb(), corner.ToArgb());
        }
    }
}
