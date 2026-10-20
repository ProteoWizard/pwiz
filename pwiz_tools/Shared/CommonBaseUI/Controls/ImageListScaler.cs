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

using System.Collections.Generic;
using System.Drawing;
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
        /// <summary>
        /// Scales the list and every icon in it to the display DPI. Icons added afterwards
        /// go through <see cref="AddScaled"/> or <see cref="AddRangeScaled"/>, which resample
        /// to the list's new size; a plain Add would have ImageList stretch them with
        /// nearest-neighbor sampling. The list keeps its transparent color, so keyed icons
        /// added later are still keyed.
        /// </summary>
        public static void ScaleToDpi(Control control, ImageList imageList)
        {
            if (imageList == null || !DpiScale.IsScaled(control))
                return;
            var originals = new List<KeyValuePair<string, Image>>();
            for (int i = 0; i < imageList.Images.Count; i++)
                originals.Add(new KeyValuePair<string, Image>(imageList.Images.Keys[i], imageList.Images[i]));
            var size = DpiScale.ScaleSize(control, imageList.ImageSize);
            // Changing the size empties the list, so it is rebuilt from the originals below.
            imageList.ColorDepth = ColorDepth.Depth32Bit;
            imageList.ImageSize = size;
            imageList.Images.Clear();
            foreach (var original in originals)
            {
                var scaled = Resample(imageList, original.Value);
                if (string.IsNullOrEmpty(original.Key))
                    imageList.Images.Add(scaled);
                else
                    imageList.Images.Add(original.Key, scaled);
            }
        }

        /// <summary>
        /// Adds an icon to a list that <see cref="ScaleToDpi"/> may already have scaled,
        /// resampling it to the list's size when they differ. At 100% nothing is resampled.
        /// The image is not disposed: callers often pass shared resources.
        /// </summary>
        public static void AddScaled(ImageList imageList, Image image)
        {
            imageList.Images.Add(image.Size == imageList.ImageSize ? image : Resample(imageList, image));
        }

        public static void AddRangeScaled(ImageList imageList, IEnumerable<Image> images)
        {
            foreach (var image in images)
                AddScaled(imageList, image);
        }

        private static Bitmap Resample(ImageList imageList, Image image)
        {
            // A color-keyed list applies its key only to exact matches, so the key goes on
            // before interpolation: afterwards the blended edge pixels would no longer match
            var key = imageList.TransparentColor;
            return DpiScale.Resample(image, imageList.ImageSize, key.A > 0 ? key : null);
        }
    }
}
