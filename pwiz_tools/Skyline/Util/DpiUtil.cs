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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using pwiz.Common.Controls;
using pwiz.Skyline.Properties;

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
        public const int REFERENCE_DPI = DpiScale.REFERENCE_DPI;

        /// <summary>
        /// Ratio of the control's rendering DPI to the 96 DPI baseline; see
        /// <see cref="DpiScale.Factor"/>. Callers with no control at hand pass null.
        /// </summary>
        public static float GetFactor(Control control)
        {
            return DpiScale.Factor(control);
        }

        public static bool IsScaled(Control control)
        {
            return DpiScale.IsScaled(control);
        }

        /// <summary>
        /// Scales a 96-DPI pixel measurement to the control's rendering DPI.
        /// </summary>
        public static int Scale(Control control, int pixels)
        {
            return DpiScale.Scale(control, pixels);
        }

        /// <summary>
        /// Scales a 96-DPI pixel measurement to the DPI of a Graphics, for paint code that
        /// has no control at hand.
        /// </summary>
        public static int Scale(Graphics g, int pixels)
        {
            return (int)Math.Round(pixels * g.DpiX / REFERENCE_DPI);
        }

        public static Size ScaleSize(Control control, Size size)
        {
            return DpiScale.ScaleSize(control, size);
        }

        /// <summary>
        /// Converts device pixels to the 96-DPI logical units in which window geometry is
        /// persisted (values saved by DPI-unaware builds were logical already, so the
        /// convention is backward compatible with existing settings).
        /// </summary>
        public static Size ScaleToLogical(Control control, Size deviceSize)
        {
            return DpiScale.ToLogical(control, deviceSize);
        }

        public static int ScaleToLogical(Control control, int devicePixels)
        {
            return DpiScale.ToLogical(control, devicePixels);
        }

        /// <summary>
        /// Scales a 96-DPI image to the control's rendering DPI; see <see cref="DpiScale.ScaleImage"/>.
        /// </summary>
        public static Image ScaleImage(Control control, Image image, Color? transparentKey = null)
        {
            return DpiScale.ScaleImage(control, image, transparentKey);
        }

        /// <summary>
        /// Returns a Skyline icon at the pixel size the control's DPI calls for; see
        /// <see cref="DpiIcons.GetIcon"/>. Pass <c>nameof(Resources.X)</c> as the name.
        /// </summary>
        public static Image GetIcon(Control control, string name, Image native, Color? transparentKey = null)
        {
            return DpiIcons.GetIcon(typeof(Resources), control, name, native, transparentKey);
        }

        /// <summary>
        /// The pre-rendered pixel sizes IconBuilder emits for a native icon size; see
        /// <see cref="DpiIcons.GetIconSizes"/>.
        /// </summary>
        public static int[] GetIconSizes(int nativeSize)
        {
            return DpiIcons.GetIconSizes(nativeSize);
        }

        /// <summary>
        /// The manifest resource name of one of Skyline's embedded icon files, or null when
        /// none is embedded; see <see cref="DpiIcons.GetIconResourceName"/>.
        /// </summary>
        public static string GetIconResourceName(string name, int size)
        {
            return DpiIcons.GetIconResourceName(typeof(Resources), name, size);
        }

        /// <summary>
        /// The Skyline Resources.resx name of a small icon bitmap, matched by pixel content,
        /// or null; see <see cref="DpiIcons.GetIconName"/>.
        /// </summary>
        public static string GetIconName(Image image, Color transparentKey = default)
        {
            return DpiIcons.GetIconName(typeof(Resources), image, transparentKey);
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
        /// Keeps the designer's bottom anchors on tab-page controls from misplacing them at
        /// high DPI on .NET, where WinForms sizes a TabPage only after its children are
        /// scaled. The anchors are held from the top until the form has loaded and the
        /// pages have their final size, then restored as designed, so the resx keeps the
        /// anchors the designer shows (issue #4599).
        /// </summary>
        public static void DeferBottomAnchors(Form form, params TabPage[] pages)
        {
            var deferred = new List<KeyValuePair<Control, AnchorStyles>>();
            foreach (var page in pages)
            {
                foreach (Control control in page.Controls)
                {
                    if ((control.Anchor & AnchorStyles.Bottom) == 0)
                        continue;
                    deferred.Add(new KeyValuePair<Control, AnchorStyles>(control, control.Anchor));
                    control.Anchor = (control.Anchor & ~AnchorStyles.Bottom) | AnchorStyles.Top;
                }
            }
            form.Load += (sender, args) =>
            {
                foreach (var entry in deferred)
                    entry.Key.Anchor = entry.Value;
            };
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
            if (!IsScaled(control))
                return;
            var factor = GetFactor(control);
            byte[] transformed;
            try
            {
                var doc = XDocument.Load(path);
                if (!ScaleFloatingWindowSizes(doc, 1 / factor))
                    return;
                // Serialize completely in memory first: a failure part-way through must not
                // leave a truncated file where the caller is about to commit it.
                var stream = new MemoryStream();
                using (var writer = XmlWriter.Create(stream,
                           new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
                {
                    doc.Save(writer);
                }
                transformed = stream.ToArray();
            }
            catch (Exception)
            {
                // A layout that cannot be transformed is saved in device pixels - degraded
                // but functional, and preferable to failing the document save.
                return;
            }
            // Outside the fallback on purpose: a failed write can leave the file truncated,
            // and the error must reach the caller so its FileSaver discards the file instead
            // of committing it over the user's layout.
            File.WriteAllBytes(path, transformed);
        }

        /// <summary>
        /// Stream form of <see cref="NormalizeDockLayoutFile"/>, for a layout captured
        /// with DockPanel.SaveAsXml that will later be reloaded through
        /// <see cref="DenormalizeDockLayoutStream"/>. Returns the original stream untouched
        /// at 100% scaling or if the stream cannot be transformed.
        /// </summary>
        public static Stream NormalizeDockLayoutStream(Control control, Stream layoutStream)
        {
            return IsScaled(control) ? TransformDockLayoutStream(layoutStream, 1 / GetFactor(control)) : layoutStream;
        }

        /// <summary>
        /// Counterpart of <see cref="NormalizeDockLayoutFile"/>: scales floating-window
        /// sizes in a docking-layout stream from 96-DPI logical units to the current
        /// display DPI before DigitalRune parses it. Returns the original stream untouched
        /// at 100% scaling or if the stream cannot be transformed.
        /// </summary>
        public static Stream DenormalizeDockLayoutStream(Control control, Stream layoutStream)
        {
            return IsScaled(control) ? TransformDockLayoutStream(layoutStream, GetFactor(control)) : layoutStream;
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
