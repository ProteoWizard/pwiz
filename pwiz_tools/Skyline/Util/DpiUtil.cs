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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
        /// Scales a 96-DPI pixel measurement using the DPI of a Graphics surface, for
        /// static drawing code that has no control reference.
        /// </summary>
        public static int Scale(Graphics g, int pixels)
        {
            return (int)Math.Round(pixels * g.DpiX / REFERENCE_DPI);
        }

        /// <summary>
        /// Scales a 96-DPI size to the control's rendering DPI.
        /// </summary>
        public static Size ScaleSize(Control control, Size size)
        {
            var factor = GetFactor(control);
            return new Size((int)Math.Round(size.Width * factor), (int)Math.Round(size.Height * factor));
        }

        /// <summary>
        /// Prepares a 96-DPI icon for an ImageList whose ImageSize has been scaled to the
        /// current DPI. At 100% scaling the original image is returned untouched, preserving
        /// the list's existing color-key behavior. At higher scaling the image is converted
        /// to 32-bit ARGB, its transparency key (if any) is applied before interpolation so
        /// the key color cannot bleed into the glyph edges, and it is resampled with
        /// high-quality bicubic interpolation - noticeably better than the nearest-neighbor
        /// scaling ImageList applies internally. A stopgap until higher-resolution icon
        /// assets exist (issue #4599).
        /// </summary>
        public static Image ScaleImageForList(Control control, Image image, Color? transparentKey = null)
        {
            var factor = GetFactor(control);
            if (Math.Abs(factor - 1) < 0.01f)
                return image;
            var source = new Bitmap(image);
            var scaled = new Bitmap((int)Math.Round(image.Width * factor),
                (int)Math.Round(image.Height * factor), PixelFormat.Format32bppArgb);
            using (source)
            using (var g = Graphics.FromImage(scaled))
            {
                if (transparentKey.HasValue)
                    source.MakeTransparent(transparentKey.Value);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(Point.Empty, scaled.Size));
            }
            return scaled;
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
