/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Windows.Forms;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// System renderer that draws the <see cref="StatusStrip"/> sizing grip as .NET Framework did:
    /// six square dots in a triangle, each with a highlight below and to the right. .NET 10 draws a
    /// different grip, which changed every tutorial screenshot of the main window.
    /// </summary>
    public class ClassicSizingGripRenderer : ToolStripSystemRenderer
    {
        private static readonly Rectangle[] GRIP_DOTS =
        {
            new Rectangle(8, 0, 2, 2), new Rectangle(8, 4, 2, 2), new Rectangle(8, 8, 2, 2),
            new Rectangle(4, 4, 2, 2), new Rectangle(4, 8, 2, 2), new Rectangle(0, 8, 2, 2)
        };

        protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e)
        {
            var statusStrip = e.ToolStrip as StatusStrip;
            if (statusStrip == null)
            {
                base.OnRenderStatusStripSizingGrip(e);
                return;
            }
            var gripBounds = statusStrip.SizeGripBounds;
            if (gripBounds.Width <= 0 || gripBounds.Height <= 0)
                return;

            bool rightToLeft = statusStrip.RightToLeft == RightToLeft.Yes;
            var lightDots = new Rectangle[GRIP_DOTS.Length];
            var darkDots = new Rectangle[GRIP_DOTS.Length];
            for (int i = 0; i < GRIP_DOTS.Length; i++)
            {
                var dot = GRIP_DOTS[i];
                if (rightToLeft)
                    dot.X = gripBounds.Width - dot.X - dot.Width;
                // The 10 px triangle sits 2 px above the bottom of the grip.
                dot.Offset(gripBounds.X, gripBounds.Bottom - 12);
                lightDots[i] = dot;
                dot.Offset(rightToLeft ? 1 : -1, -1);
                darkDots[i] = dot;
            }
            e.Graphics.FillRectangles(SystemBrushes.ButtonHighlight, lightDots);
            e.Graphics.FillRectangles(SystemBrushes.ButtonShadow, darkDots);
        }
    }
}
