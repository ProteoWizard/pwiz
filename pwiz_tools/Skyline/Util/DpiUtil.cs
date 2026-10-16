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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

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
        /// (1.0 at 100% display scaling, 1.25 at 125%, etc.). In system-DPI-aware mode the
        /// system DPI is fixed for the process lifetime and Control.DeviceDpi carries the
        /// same value, so the screen DC is read once and cached: this is called from
        /// per-node paint paths and from code with no control at hand (callers pass null).
        /// The control parameter is kept for the day per-monitor V2 makes DPI genuinely
        /// per-control (issue #4599).
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
            // The caller keeps only the scaled copy (ImageList.Images.Add copies, and
            // image-property call sites replace the reference), so dispose the input to
            // avoid orphaning a GDI+ bitmap per scaled icon.
            image.Dispose();
            var scaled = new Bitmap((int)Math.Round(source.Width * factor),
                (int)Math.Round(source.Height * factor), PixelFormat.Format32bppArgb);
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
                // Without a wrap mode the filter samples transparent black past the bitmap
                // edge, leaving a faint grey frame around every icon
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(source, new Rectangle(Point.Empty, scaled.Size), 0, 0, source.Width, source.Height,
                        GraphicsUnit.Pixel, attributes);
                }
            }
            return scaled;
        }

        /// <summary>
        /// Draws an image at its own pixel size, vertically centered in a row, into an
        /// explicit destination rectangle. DrawImageUnscaled honors the image's DPI
        /// metadata and would re-inflate bitmaps created in a high-DPI process, so
        /// owner-drawn lists and trees share this instead (issue #4599).
        /// </summary>
        public static void DrawImageCentered(Graphics g, Image image, int x, int rowTop, int rowHeight)
        {
            g.DrawImage(image, new Rectangle(x, rowTop + (rowHeight - image.Height) / 2,
                image.Width, image.Height));
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

        /// <summary>
        /// Converts a device-pixel measurement to 96-DPI logical units for persisting
        /// (e.g. a splitter distance).
        /// </summary>
        public static int ScaleToLogical(Control control, int devicePixels)
        {
            return (int)Math.Round(devicePixels / GetFactor(control));
        }

        /// <summary>
        /// Fills a TabPage with a control from its designer position to the page's bottom
        /// edge and to the given right margin (device pixels), on every resize. On .NET a
        /// control anchored to the bottom of a TabPage comes out short at high DPI, and the
        /// Dock.Fill-with-Padding workaround shifts the page's other anchored controls by
        /// the padding (WinForms anchors them to the padded display rectangle), so the
        /// control is sized by hand instead. The right margin is a parameter because the
        /// control's own width cannot be trusted either (a ZedGraphControl measured 422
        /// wide on a 1059-wide page) (issue #4599).
        /// </summary>
        public static void FillTabPage(TabPage page, Control control, int right)
        {
            int left = control.Left, top = control.Top;
            control.Anchor = AnchorStyles.None;
            page.Resize += (sender, args) => FitTabPage(page, control, left, top, right);
            FitTabPage(page, control, left, top, right);
        }

        private static void FitTabPage(TabPage page, Control control, int left, int top, int right)
        {
            control.Bounds = new Rectangle(left, top, page.ClientSize.Width - left - right, page.ClientSize.Height - top);
        }

        /// <summary>
        /// Rewrites a freshly saved docking-layout (.view) file so floating-window SIZES
        /// are stored in 96-DPI logical units, keeping the file portable across machines
        /// with different display scaling (files saved by DPI-unaware builds are logical
        /// by definition, so the convention is backward compatible and old Skyline
        /// versions can read new files unchanged). Positions stay in physical screen
        /// coordinates - where the user put the window on THEIR screen - and off-screen
        /// cases are handled by EnsureFloatingWindowsVisible on load. The docked layout
        /// is stored as fractions by DigitalRune and needs no treatment. No-op at 100%.
        /// </summary>
        public static void NormalizeDockLayoutFile(Control control, string path)
        {
            var factor = GetFactor(control);
            if (Math.Abs(factor - 1) < 0.01f)
                return;
            try
            {
                var doc = XDocument.Load(path);
                if (!ScaleFloatingWindowSizes(doc, 1 / factor))
                    return;
                // Serialize completely in memory first: a failure part-way through must not
                // leave a truncated file where the caller is about to commit it.
                var transformed = new MemoryStream();
                using (var writer = XmlWriter.Create(transformed,
                           new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
                {
                    doc.Save(writer);
                }
                File.WriteAllBytes(path, transformed.ToArray());
            }
            catch (Exception)
            {
                // A layout that cannot be transformed is saved in device pixels - degraded
                // but functional, and preferable to failing the document save.
            }
        }

        /// <summary>
        /// Stream form of <see cref="NormalizeDockLayoutFile"/>, for a layout captured
        /// with DockPanel.SaveAsXml that will later be reloaded through
        /// <see cref="DenormalizeDockLayoutStream"/>. Returns the original stream untouched
        /// at 100% scaling or if the stream cannot be transformed.
        /// </summary>
        public static Stream NormalizeDockLayoutStream(Control control, Stream layoutStream)
        {
            var factor = GetFactor(control);
            return Math.Abs(factor - 1) < 0.01f ? layoutStream : TransformDockLayoutStream(layoutStream, 1 / factor);
        }

        /// <summary>
        /// Counterpart of <see cref="NormalizeDockLayoutFile"/>: scales floating-window
        /// sizes in a docking-layout stream from 96-DPI logical units to the current
        /// display DPI before DigitalRune parses it. Returns the original stream untouched
        /// at 100% scaling or if the stream cannot be transformed.
        /// </summary>
        public static Stream DenormalizeDockLayoutStream(Control control, Stream layoutStream)
        {
            var factor = GetFactor(control);
            return Math.Abs(factor - 1) < 0.01f ? layoutStream : TransformDockLayoutStream(layoutStream, factor);
        }

        private static Stream TransformDockLayoutStream(Stream layoutStream, float factor)
        {
            var startPosition = layoutStream.CanSeek ? layoutStream.Position : 0;
            try
            {
                var doc = XDocument.Load(layoutStream);
                var transformed = new MemoryStream();
                using (var writer = XmlWriter.Create(transformed,
                           new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false }))
                {
                    ScaleFloatingWindowSizes(doc, factor);
                    doc.Save(writer);
                }
                transformed.Position = 0;
                return transformed;
            }
            catch (Exception)
            {
                if (layoutStream.CanSeek)
                    layoutStream.Position = startPosition;
                return layoutStream;
            }
        }

        /// <summary>
        /// Multiplies the width and height components of every FloatingWindow Bounds
        /// attribute ("x, y, w, h") by the factor. Returns true if anything changed.
        /// </summary>
        private static bool ScaleFloatingWindowSizes(XDocument doc, float factor)
        {
            var anyChanged = false;
            foreach (var boundsAttr in doc.Descendants(@"FloatingWindow")
                         .Select(fw => fw.Attribute(@"Bounds")).Where(attr => attr != null))
            {
                var parts = boundsAttr.Value.Split(',');
                if (parts.Length != 4)
                    continue;
                var values = new int[4];
                var parsed = true;
                for (var i = 0; i < 4; i++)
                    parsed = parsed && int.TryParse(parts[i].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out values[i]);
                if (!parsed)
                    continue;
                values[2] = (int)Math.Round(values[2] * factor);
                values[3] = (int)Math.Round(values[3] * factor);
                boundsAttr.Value = string.Join(@", ",
                    values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
                anyChanged = true;
            }
            return anyChanged;
        }
    }
}
