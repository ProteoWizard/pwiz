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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace pwiz.Common.Controls
{
    /// <summary>
    /// Scales an ImageList's icons to the display DPI. WinForms scales toolbars and menus
    /// itself but leaves ImageList.ImageSize at its 96-DPI design value, so list and tree
    /// icons stay small next to scaled text. Call once after InitializeComponent, before
    /// the images are drawn; at 100% scaling nothing changes.
    /// </summary>
    public static class ImageListScaler
    {
        public static float GetFactor(Control control)
        {
            return control.DeviceDpi / 96f;
        }

        public static void ScaleToDpi(Control control, ImageList imageList)
        {
            if (imageList == null)
                return;
            var factor = GetFactor(control);
            if (Math.Abs(factor - 1) < 0.01f)
                return;
            var originals = new List<KeyValuePair<string, Image>>();
            for (int i = 0; i < imageList.Images.Count; i++)
                originals.Add(new KeyValuePair<string, Image>(imageList.Images.Keys[i], imageList.Images[i]));
            var size = new Size((int) Math.Round(imageList.ImageSize.Width * factor),
                (int) Math.Round(imageList.ImageSize.Height * factor));
            // Changing the size empties the list, so it is rebuilt from the originals below.
            imageList.ColorDepth = ColorDepth.Depth32Bit;
            imageList.ImageSize = size;
            imageList.Images.Clear();
            foreach (var original in originals)
            {
                var scaled = ScaleImage(original.Value, size);
                if (string.IsNullOrEmpty(original.Key))
                    imageList.Images.Add(scaled);
                else
                    imageList.Images.Add(original.Key, scaled);
            }
        }

        /// <summary>
        /// Bicubic resample into a 32-bit ARGB bitmap at the given size. Noticeably better
        /// than the nearest-neighbor stretch ImageList applies on its own.
        /// </summary>
        public static Bitmap ScaleImage(Image image, Size size)
        {
            var result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            result.SetResolution(96, 96);
            using (var g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(image, new Rectangle(Point.Empty, size));
            }
            return result;
        }
    }
}
