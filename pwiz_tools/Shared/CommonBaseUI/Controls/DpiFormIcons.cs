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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace pwiz.Common.Controls
{
    /// <summary>
    /// Adds frames at the display's small and large icon sizes to a form's icon. WinForms
    /// gives the window the frame nearest each size and, on a tie, the first one in the
    /// file, so at 150% a 16/32/48 px icon puts its 16 px frame in the 24 px title bar,
    /// stretched. A missing size is resampled from the smallest larger frame instead,
    /// until the icon files carry every size (issue #4599).
    /// </summary>
    public static class DpiFormIcons
    {
        private const int ICON_DIR_SIZE = 6;
        private const int ICON_DIR_ENTRY_SIZE = 16;
        private const int BITMAP_INFO_HEADER_SIZE = 40;
        private const int ICON_TYPE = 1;

        // Icons already extended, keyed by the bytes of the original icon file. Resources
        // properties return a new Icon on every read, so the instance cannot be the key.
        private static readonly Dictionary<string, Icon> _icons = new Dictionary<string, Icon>();
        private static readonly object LOCK = new object();
        private static Icon _defaultFormIcon;

        /// <summary>
        /// The icon Windows shows for a form that sets none: the application's own, which
        /// it takes from the executable at a single size. Set once at startup; while it is
        /// null such forms are left alone.
        /// </summary>
        public static Icon ApplicationIcon { get; set; }

        /// <summary>
        /// Gives a form an icon with the display's small and large sizes, see
        /// <see cref="GetIcon"/>. A form that sets no icon reads back WinForms' shared
        /// default, which it never shows, so it gets <see cref="ApplicationIcon"/> instead.
        /// </summary>
        public static void PrepareFormIcon(Form form)
        {
            if (!form.ShowIcon || !DpiScale.IsScaled(form))
                return;
            var icon = form.Icon;
            if (ReferenceEquals(icon, GetDefaultFormIcon()))
            {
                if (ApplicationIcon == null)
                    return;
                icon = ApplicationIcon;
            }
            form.Icon = GetIcon(form, icon);
        }

        /// <summary>
        /// The icon with a frame added for each of the display's small and large icon sizes
        /// it lacks, or the icon itself at 100%, when it has them all, or when its data
        /// cannot be read as an icon file. Results are cached and shared, so callers must
        /// not dispose them.
        /// </summary>
        public static Icon GetIcon(Control control, Icon icon)
        {
            return GetIcon(control, icon, null, null);
        }

        /// <summary>
        /// <see cref="GetIcon(Control, Icon)"/> with each missing frame taken from a per-DPI
        /// drawing of the same icon, such as an IconBuilder file, where the loader returns one
        /// for the pixel size, and resampled from the icon's own frames otherwise. The source
        /// name identifies the drawing in the cache.
        /// </summary>
        public static Icon GetIcon(Control control, Icon icon, string sourceName, Func<int, Image> loadFrame)
        {
            if (icon == null || !DpiScale.IsScaled(control))
                return icon;

            byte[] data;
            using (var stream = new MemoryStream())
            {
                icon.Save(stream);
                data = stream.ToArray();
            }
            var frames = ReadFrames(data);
            if (frames == null)
                return icon;
            var missingSizes = new[] { SystemInformation.SmallIconSize.Width, SystemInformation.IconSize.Width }
                .Distinct().Where(size => frames.All(f => f.Width != size)).ToArray();
            if (missingSizes.Length == 0)
                return icon;

            var key = sourceName + @":" + Convert.ToBase64String(data);
            lock (LOCK)
            {
                if (_icons.TryGetValue(key, out var extended))
                    return extended;
                var allFrames = frames.ToList();
                foreach (var size in missingSizes)
                    allFrames.Add(CreateFrame(icon, frames, size, loadFrame));
                using (var stream = new MemoryStream(WriteIcon(allFrames)))
                {
                    extended = new Icon(stream);
                }
                _icons.Add(key, extended);
                return extended;
            }
        }

        /// <summary>
        /// The instance Form.Icon returns for every form without an icon of its own.
        /// </summary>
        private static Icon GetDefaultFormIcon()
        {
            lock (LOCK)
            {
                if (_defaultFormIcon == null)
                {
                    using (var form = new Form())
                    {
                        _defaultFormIcon = form.Icon;
                    }
                }
                return _defaultFormIcon;
            }
        }

        /// <summary>
        /// The frames of an icon file, or null when the data is not one.
        /// </summary>
        private static List<IconFrame> ReadFrames(byte[] data)
        {
            if (data.Length < ICON_DIR_SIZE)
                return null;
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                reader.ReadUInt16();    // Reserved
                if (reader.ReadUInt16() != ICON_TYPE)
                    return null;
                int count = reader.ReadUInt16();
                if (data.Length < ICON_DIR_SIZE + count * ICON_DIR_ENTRY_SIZE)
                    return null;
                var frames = new List<IconFrame>();
                for (var i = 0; i < count; i++)
                {
                    var frame = new IconFrame
                    {
                        Width = ReadDimension(reader),
                        Height = ReadDimension(reader),
                        ColorCount = reader.ReadByte(),
                        Reserved = reader.ReadByte(),
                        Planes = reader.ReadUInt16(),
                        BitCount = reader.ReadUInt16()
                    };
                    var length = reader.ReadUInt32();
                    var offset = reader.ReadUInt32();
                    if (offset + (long) length > data.Length)
                        return null;
                    frame.Data = new byte[length];
                    Array.Copy(data, offset, frame.Data, 0, length);
                    frames.Add(frame);
                }
                return frames;
            }
        }

        /// <summary>
        /// A 32-bit frame of the given size: the loader's image when it returns one, else
        /// resampled bicubically from the smallest frame at least that large, or from the
        /// largest frame when none is.
        /// </summary>
        private static IconFrame CreateFrame(Icon icon, IList<IconFrame> frames, int size, Func<int, Image> loadFrame)
        {
            using (var loaded = loadFrame?.Invoke(size))
            {
                if (loaded != null)
                {
                    using (var bitmap = new Bitmap(loaded, size, size))
                    {
                        return CreateFrame(bitmap);
                    }
                }
            }
            var source = frames.Where(f => f.Width >= size).OrderBy(f => f.Width).FirstOrDefault()
                         ?? frames.OrderByDescending(f => f.Width).First();
            using (var sourceIcon = new Icon(icon, source.Width, source.Height))
            using (var sourceBitmap = sourceIcon.ToBitmap())
            using (var bitmap = DpiScale.Resample(sourceBitmap, new Size(size, size)))
            {
                return CreateFrame(bitmap);
            }
        }

        private static IconFrame CreateFrame(Bitmap bitmap)
        {
            return new IconFrame
            {
                Width = bitmap.Width,
                Height = bitmap.Height,
                Planes = 1,
                BitCount = 32,
                Data = EncodeDib(bitmap)
            };
        }

        /// <summary>
        /// A 32-bit ARGB bitmap as icon image data: a BITMAPINFOHEADER with the doubled
        /// height an icon uses, the bottom-up BGRA pixels, then the AND mask, set where the
        /// pixel is fully transparent for consumers that ignore alpha.
        /// </summary>
        private static byte[] EncodeDib(Bitmap bitmap)
        {
            int width = bitmap.Width, height = bitmap.Height;
            var pixels = new byte[width * height * 4];
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                for (var y = 0; y < height; y++)
                    Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width * 4, width * 4);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            var maskStride = (width + 31) / 32 * 4;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(BITMAP_INFO_HEADER_SIZE);
                writer.Write(width);
                writer.Write(height * 2);
                writer.Write((ushort) 1);   // Planes
                writer.Write((ushort) 32);  // Bits per pixel
                writer.Write(0);            // BI_RGB
                writer.Write(pixels.Length + maskStride * height);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                for (var y = height - 1; y >= 0; y--)
                    writer.Write(pixels, y * width * 4, width * 4);
                for (var y = height - 1; y >= 0; y--)
                {
                    var row = new byte[maskStride];
                    for (var x = 0; x < width; x++)
                    {
                        if (pixels[(y * width + x) * 4 + 3] == 0)
                            row[x / 8] |= (byte) (0x80 >> (x % 8));
                    }
                    writer.Write(row);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] WriteIcon(IList<IconFrame> frames)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort) 0);
                writer.Write((ushort) ICON_TYPE);
                writer.Write((ushort) frames.Count);
                var offset = ICON_DIR_SIZE + frames.Count * ICON_DIR_ENTRY_SIZE;
                foreach (var frame in frames)
                {
                    writer.Write((byte) (frame.Width >= 256 ? 0 : frame.Width));
                    writer.Write((byte) (frame.Height >= 256 ? 0 : frame.Height));
                    writer.Write(frame.ColorCount);
                    writer.Write(frame.Reserved);
                    writer.Write(frame.Planes);
                    writer.Write(frame.BitCount);
                    writer.Write(frame.Data.Length);
                    writer.Write(offset);
                    offset += frame.Data.Length;
                }
                foreach (var frame in frames)
                    writer.Write(frame.Data);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static int ReadDimension(BinaryReader reader)
        {
            int value = reader.ReadByte();
            return value == 0 ? 256 : value;
        }

        private sealed class IconFrame
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public byte ColorCount { get; set; }
            public byte Reserved { get; set; }
            public ushort Planes { get; set; }
            public ushort BitCount { get; set; }
            public byte[] Data { get; set; }
        }
    }
}
