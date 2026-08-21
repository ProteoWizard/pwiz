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

        private static float _factor;

        /// <summary>
        /// Ratio of the process's rendering DPI to the classic 96 DPI baseline
        /// (1.0 at 100% display scaling, 1.25 at 125%, etc.). Control.DeviceDpi is NOT
        /// used: on .NET Framework 4.7.2 it stays 96 in a system-DPI-aware process (it is
        /// only maintained under per-monitor-V2 awareness), before AND after handle
        /// creation. The screen DC is the reliable source, and because the system DPI is
        /// fixed for the process lifetime in system-aware mode, the value is read once and
        /// cached - this property is called from per-node paint paths. The control
        /// parameter is kept for the day per-monitor V2 makes DPI genuinely per-control
        /// (.NET 8 port, issue #4599).
        /// </summary>
        public static float GetFactor(Control control)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_factor == 0)
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    _factor = g.DpiX / REFERENCE_DPI;
                }
            }
            return _factor;
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
            // Tag as 96-DPI so consumers that honor DPI metadata (e.g. DrawImageUnscaled)
            // treat the bitmap as pixel-sized instead of re-inflating it.
            scaled.SetResolution(REFERENCE_DPI, REFERENCE_DPI);
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
        /// Scales a tool strip's glyphs to the current DPI: raises ImageScalingSize and
        /// replaces each item's 96-DPI image with a bicubic pre-scaled copy, which looks
        /// noticeably better than the linear scaling ToolStrip applies on its own. Each
        /// item's ImageTransparentColor is applied before interpolation so the key color
        /// cannot fringe the glyph. Images assigned to items later (e.g. mode-UI buttons)
        /// still display at the scaled size via ImageScalingSize, with ToolStrip's own
        /// scaling quality. No-op at 100% scaling. A stopgap until higher-resolution
        /// glyph assets exist (issue #4599).
        /// </summary>
        public static void ScaleToolStripImages(ToolStrip toolStrip)
        {
            var factor = GetFactor(toolStrip);
            if (Math.Abs(factor - 1) < 0.01f)
                return;
            toolStrip.ImageScalingSize = ScaleSize(toolStrip, toolStrip.ImageScalingSize);
            foreach (ToolStripItem item in toolStrip.Items)
            {
                if (item.Image == null)
                    continue;
                var key = item.ImageTransparentColor;
                item.Image = ScaleImageForList(toolStrip, item.Image,
                    key.IsEmpty ? (Color?)null : key);
            }
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
