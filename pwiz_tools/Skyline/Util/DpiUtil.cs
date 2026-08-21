/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5) <noreply .at. anthropic.com>
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

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Helpers for scaling pixel-based layout to the current display DPI now that
    /// Skyline runs system-DPI-aware (issue #4599). Persisted window geometry follows
    /// the convention "store 96-DPI logical units, scale on restore": values saved by
    /// DPI-unaware builds were logical 96-DPI pixels already, so the convention is
    /// backward compatible with existing user settings.
    /// </summary>
    public static class DpiUtil
    {
        public const int REFERENCE_DPI = 96;

        /// <summary>
        /// Ratio of the control's rendering DPI to the classic 96 DPI baseline
        /// (1.0 at 100% display scaling, 1.25 at 125%, etc.).
        /// </summary>
        public static float GetFactor(Control control)
        {
            return control.DeviceDpi / (float)REFERENCE_DPI;
        }

        /// <summary>
        /// Scales a 96-DPI pixel measurement to the control's rendering DPI.
        /// </summary>
        public static int Scale(Control control, int pixels)
        {
            return (int)Math.Round(pixels * GetFactor(control));
        }

        /// <summary>
        /// Converts a persisted 96-DPI logical size to device pixels for restoring
        /// window geometry.
        /// </summary>
        public static Size ScaleFromLogical(Control control, Size logicalSize)
        {
            var factor = GetFactor(control);
            return new Size((int)Math.Round(logicalSize.Width * factor),
                (int)Math.Round(logicalSize.Height * factor));
        }

        /// <summary>
        /// Converts a device-pixel size to 96-DPI logical units for persisting
        /// window geometry.
        /// </summary>
        public static Size ScaleToLogical(Control control, Size deviceSize)
        {
            var factor = GetFactor(control);
            return new Size((int)Math.Round(deviceSize.Width / factor),
                (int)Math.Round(deviceSize.Height / factor));
        }
    }
}
