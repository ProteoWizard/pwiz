/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Util
{
    public static class SystemMetrics
    {
        // For some reason, TextRenderer.MeasureText does not return the actual width of the text.
        // We multiply by the "FUDGE_FACTOR" so that the ListBox is wide enough that it doesn't need a horizontal scroll bar.
        private const double FUDGE_FACTOR = 1;
        public static Size MeasureText(Font font, String text, int dxAvailable)
        {
            Size size = TextRenderer.MeasureText(text, font, new Size(dxAvailable, Int16.MaxValue));
            return new Size((int) (size.Width * FUDGE_FACTOR), (int)(size.Height * FUDGE_FACTOR));
        }
        public static int GetTextWidth(Font font, String text)
        {
            return MeasureText(font, text, int.MaxValue).Width;
        }
    }
}
