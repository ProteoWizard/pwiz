/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Find;

namespace pwiz.Skyline.Util
{
    public class TextRendererHelper
    {
        public TextRendererHelper()
        {
            ForeColor = SystemColors.ControlText;
            BackColor = Color.Transparent;
            Font = SystemFonts.DefaultFont;
            HighlightFont = new Font(Font, FontStyle.Bold);
        }
        public Color ForeColor { get; set; }
        public Color BackColor { get; set; }
        public Font Font { get; set; }
        public Font HighlightFont { get; set; }
        public void DrawHighlightedText(Graphics graphics, Rectangle descriptionBounds, FindMatch findMatch)
        {
            if (descriptionBounds.Width < 0)
            {
                return;
            }
            var graphicsState = graphics.Save();
            try
            {
                graphics.SetClip(descriptionBounds);
                int ichHighlightBegin = findMatch.RangeStart;
                int ichHighlightEnd = findMatch.RangeEnd;
                var displayText = findMatch.DisplayText;
                var beginText = displayText.Substring(0, ichHighlightBegin);
                var highlightedText = displayText.Substring(ichHighlightBegin, ichHighlightEnd - ichHighlightBegin);
                var endText = displayText.Substring(ichHighlightEnd);
                // Measure the width of the three parts of text
                const int xPad = 0;
                const TextFormatFlags format = TextFormatFlags.SingleLine |
                    TextFormatFlags.PreserveGraphicsClipping |
                    TextFormatFlags.NoPadding;
                Size sizeMax = new Size(int.MaxValue, int.MaxValue);
                int dxBegin =
                    TextRenderer.MeasureText(graphics, beginText, Font, sizeMax, format).Width - xPad;
                int dxHighlight =
                    TextRenderer.MeasureText(graphics, highlightedText, HighlightFont, sizeMax, format).Width - xPad;
                int dxEnd =
                    TextRenderer.MeasureText(graphics, endText, Font, sizeMax, format).Width - xPad;
                int dxTotal = dxBegin + dxHighlight + dxEnd;

                int dxBeginUnclipped = dxBegin;
                // If the text won't all fit in the space provided, figure out what should be clipped,
                // trying to keep the bold text centered in the middle of the line.
                if (dxTotal > descriptionBounds.Width)
                {
                    int dxHalf = (descriptionBounds.Width - dxHighlight) / 2;
                    if (dxBegin > dxHalf && dxEnd > dxHalf)
                    {
                        dxBegin = dxHalf;
                    }
                    else if (dxBegin > dxHalf)
                    {
                        dxBegin = descriptionBounds.Width - dxHighlight - dxEnd;
                    }
                    else
                    {
                        dxEnd = descriptionBounds.Width - dxHighlight - dxBegin;
                    }
                }
                // Draw the text before the highlight
                var rect = new Rectangle(
                    descriptionBounds.Left + dxBegin - dxBeginUnclipped,
                    descriptionBounds.Top,
                    dxBeginUnclipped, descriptionBounds.Height);
                TextRenderer.DrawText(graphics, beginText, Font, rect, ForeColor, BackColor, format);
                // Draw the highlighted text
                rect.X += dxBeginUnclipped;
                rect.Width = dxHighlight;
                TextRenderer.DrawText(graphics, highlightedText, HighlightFont, rect, ForeColor, BackColor, format);
                // Draw the text after the highlight
                rect.X += dxHighlight;
                rect.Width = dxEnd;
                TextRenderer.DrawText(graphics, endText, Font, rect, ForeColor, BackColor, format);
            }
            finally
            {
                graphics.Restore(graphicsState);
            }
        }
    }
}
