/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace pwiz.Common.Controls
{
    /// <summary>
    /// The one source for display-DPI scaling now that Skyline runs system-DPI-aware
    /// (issue #4599): the factor, the "is scaling active" test, pixel and size scaling in
    /// both directions, and the icon resampler. Every path is a no-op at 100% scaling.
    /// </summary>
    public static class DpiScale
    {
        public const int REFERENCE_DPI = 96;

        private static float _systemFactor;

        /// <summary>
        /// Ratio of the control's rendering DPI to the 96-DPI baseline (1.0 at 100% display
        /// scaling, 1.5 at 150%). Control.DeviceDpi is set from the system DPI when the
        /// control is constructed, so it is valid before the handle exists. With no control
        /// at hand the system DPI is read from the screen once and cached: in a
        /// system-DPI-aware process it is fixed for the lifetime of the process.
        /// </summary>
        public static float Factor(Control control)
        {
            if (control != null)
                return control.DeviceDpi / (float) REFERENCE_DPI;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_systemFactor == 0)
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    _systemFactor = g.DpiX / REFERENCE_DPI;
                }
            }
            return _systemFactor;
        }

        public static bool IsScaled(Control control)
        {
            return Math.Abs(Factor(control) - 1) > 0.01f;
        }

        /// <summary>
        /// Scales a 96-DPI pixel measurement to the control's rendering DPI.
        /// </summary>
        public static int Scale(Control control, int pixels)
        {
            return (int) Math.Round(pixels * Factor(control));
        }

        public static Size ScaleSize(Control control, Size size)
        {
            var factor = Factor(control);
            return new Size((int) Math.Round(size.Width * factor), (int) Math.Round(size.Height * factor));
        }

        /// <summary>
        /// Converts device pixels back to 96-DPI logical units, for values that are persisted.
        /// </summary>
        public static int ToLogical(Control control, int devicePixels)
        {
            return (int) Math.Round(devicePixels / Factor(control));
        }

        public static Size ToLogical(Control control, Size deviceSize)
        {
            var factor = Factor(control);
            return new Size((int) Math.Round(deviceSize.Width / factor), (int) Math.Round(deviceSize.Height / factor));
        }

        /// <summary>
        /// Scales a 96-DPI image to the control's rendering DPI. At 100% the image is
        /// returned untouched. Otherwise the input is disposed, since the caller keeps only
        /// the scaled copy (ImageList.Images.Add copies, image properties replace the
        /// reference), so no GDI+ bitmap is orphaned per icon. A transparency key is applied
        /// before interpolation so the key color cannot bleed into the glyph edges.
        /// </summary>
        public static Image ScaleImage(Control control, Image image, Color? transparentKey = null)
        {
            if (!IsScaled(control))
                return image;
            var scaled = Resample(image, ScaleSize(control, image.Size), transparentKey);
            image.Dispose();
            return scaled;
        }

        /// <summary>
        /// Bicubic resample into a 32-bit ARGB bitmap of the given size, noticeably better
        /// than the nearest-neighbor stretch ImageList applies on its own. The result is
        /// tagged 96-DPI so consumers that honor DPI metadata treat it as pixel-sized.
        /// A stopgap until higher-resolution icon assets exist.
        /// </summary>
        public static Bitmap Resample(Image image, Size size, Color? transparentKey = null)
        {
            var result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            result.SetResolution(REFERENCE_DPI, REFERENCE_DPI);
            using (var source = new Bitmap(image))
            using (var g = Graphics.FromImage(result))
            using (var attributes = new ImageAttributes())
            {
                if (transparentKey.HasValue)
                    source.MakeTransparent(transparentKey.Value);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                // Without a wrap mode the filter samples transparent black past the bitmap
                // edge, leaving a faint grey frame around every icon
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source, new Rectangle(Point.Empty, size), 0, 0, source.Width, source.Height,
                    GraphicsUnit.Pixel, attributes);
            }
            return result;
        }
    }
}
