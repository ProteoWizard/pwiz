/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Production screen capture utility for Skyline forms.
    /// Provides DPI-aware capture with redaction of non-Skyline windows.
    /// </summary>
    public static class ScreenCapture
    {
        private static bool _sessionPermissionGranted;

        /// <summary>
        /// Resets the session-level screen capture permission. Used by tests.
        /// </summary>
        public static void ResetSessionPermission()
        {
            _sessionPermissionGranted = false;
        }

        public class PointFactor
        {
            private readonly float _factor;

            public PointFactor(float pFactor) { _factor = pFactor; }

            public static Point operator *(Point pt, PointFactor pFactor) => new Point((int)Math.Round(pt.X * pFactor._factor), (int)Math.Round(pt.Y * pFactor._factor));
            public static Size operator *(Size sz, PointFactor pFactor) => new Size((int)Math.Round(sz.Width * pFactor._factor), (int)Math.Round(sz.Height * pFactor._factor));
            public static Rectangle operator *(Rectangle rect, PointFactor pFactor) => new Rectangle(rect.Location * pFactor, rect.Size * pFactor);
        }

        public class PointAdditive
        {
            private readonly Point _add;
            public PointAdditive(Point pAdd) { _add = pAdd; }
            public PointAdditive(int pX, int pY) { _add = new Point(pX, pY); }
            public static PointAdditive operator +(Point pt, PointAdditive pAdd) => new PointAdditive(new Point(pt.X + pAdd._add.X, pt.Y + pAdd._add.Y));
            public static Size operator +(Size sz, PointAdditive pAdd) => new Size(sz.Width + pAdd._add.X, sz.Height + pAdd._add.Y);

            public static implicit operator Point(PointAdditive add) => add._add;
        }

        // Public methods

        /// <summary>
        /// Returns the screen rectangle for a control, accounting for docking, framing, and DPI scaling.
        /// </summary>
        public static Rectangle GetWindowRectangle(Control ctrl, bool fullScreen = false, bool scale = true)
        {
            var snapshotBounds = Rectangle.Empty;

            var dockedStates = new[] { DockState.DockBottom, DockState.DockLeft, DockState.DockRight, DockState.DockTop, DockState.Document };
            var dockableForm = ctrl as DockableForm;
            if (dockableForm != null && dockedStates.Any(state => dockableForm.DockState == state))
            {
                snapshotBounds = GetDockedFormBounds(dockableForm);
            }
            else if (fullScreen)
            {
                snapshotBounds = (Rectangle)ctrl.Invoke((Func<Rectangle>)(() => Screen.FromControl(ctrl).Bounds));
            }
            else
            {
                snapshotBounds = GetFramedWindowBounds(ctrl);
            }
            return scale ? snapshotBounds * GetScalingFactor() : snapshotBounds;
        }

        public static Rectangle GetDockedFormBounds(DockableForm ctrl)
        {
            return ctrl.InvokeRequired
                ? (Rectangle)ctrl.Invoke((Func<Rectangle>)(() => GetDockedFormBoundsInternal(ctrl)))
                : GetDockedFormBoundsInternal(ctrl);
        }

        public static Rectangle GetFramedWindowBounds(Control ctrl)
        {
            ctrl = FindParent<FloatingWindow>(ctrl) ?? ctrl;
            return ctrl.InvokeRequired
                ? (Rectangle)ctrl.Invoke((Func<Rectangle>)(() => GetFramedWindowBoundsInternal(ctrl)))
                : GetFramedWindowBoundsInternal(ctrl);
        }

        public static TParent FindParent<TParent>(Control ctrl) where TParent : Control
        {
            while (ctrl != null)
            {
                if (ctrl is TParent parent)
                    return parent;
                ctrl = ctrl.Parent;
            }
            return null;
        }

        public static PointFactor GetScalingFactor()
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            IntPtr desktop = g.GetHdc();
            int logicalScreenHeight = Gdi32.GetDeviceCaps(desktop, Gdi32.DeviceCap.VERTRES);
            int physicalScreenHeight = Gdi32.GetDeviceCaps(desktop, Gdi32.DeviceCap.DESKTOPVERTRES);
            float screenScalingFactor = physicalScreenHeight / (float)logicalScreenHeight;
            g.ReleaseHdc(desktop);
            return new PointFactor(screenScalingFactor);
        }

        /// <summary>
        /// Returns true if the desktop is available for screen capture.
        /// False in Docker containers, disconnected Remote Desktop sessions, etc.
        /// </summary>
        public static bool IsDesktopAvailable()
        {
            try
            {
                using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(0, 0, 0, 0, new Size(1, 1));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Captures a raw screen region as a bitmap.
        /// </summary>
        public static Bitmap CaptureScreen(Rectangle screenRect)
        {
            var bmp = new Bitmap(screenRect.Width, screenRect.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(screenRect.Location, Point.Empty, screenRect.Size);
                }
            }
            catch (Exception)
            {
                // CopyFromScreen can fail if the desktop session disconnects mid-capture
                // (e.g. Remote Desktop disconnect). Return blank bitmap rather than crashing.
            }
            return bmp;
        }

        /// <summary>
        /// Captures a screen region and redacts pixels belonging to non-Skyline windows
        /// that are above the target form in z-order.
        /// </summary>
        /// <param name="screenRect">The screen rectangle to capture</param>
        /// <param name="targetForm">The Skyline form being captured, used to determine z-order cutoff</param>
        public static Bitmap CaptureAndRedact(Rectangle screenRect, Control targetForm)
        {
            var bmp = CaptureScreen(screenRect);

            // Find the top-level window handle for z-order comparison.
            // For docked panels, this returns SkylineWindow.
            // For floating panels, this returns the FloatingWindow.
            var topLevelOwner = FormUtil.FindTopLevelOwner(targetForm) ?? targetForm;
            var topLevelHandle = topLevelOwner.Handle;

            // Collect screen rects of non-Skyline windows above our target in z-order
            var foreignRects = GetForeignWindowRects(screenRect, topLevelHandle);
            if (foreignRects.Count == 0)
                return bmp;

            // Redact foreign window regions
            using (var g = Graphics.FromImage(bmp))
            using (var redactRegion = new Region(Rectangle.Empty))
            {
                foreach (var foreignRect in foreignRects)
                {
                    var bmpRect = new Rectangle(
                        foreignRect.X - screenRect.X,
                        foreignRect.Y - screenRect.Y,
                        foreignRect.Width, foreignRect.Height);
                    redactRegion.Union(bmpRect);
                }
                using (var brush = new SolidBrush(Color.Cyan))
                    g.FillRegion(brush, redactRegion);
            }
            return bmp;
        }

        /// <summary>
        /// Returns the screen rectangles of visible non-Skyline top-level windows
        /// that are above the target window in z-order and overlap the given screen rectangle.
        /// EnumWindows enumerates in z-order (top to bottom), so we stop once we
        /// reach our own top-level window - anything below it cannot obscure the target.
        /// </summary>
        private static List<Rectangle> GetForeignWindowRects(Rectangle screenRect,
            IntPtr targetHandle)
        {
            uint currentPid = (uint)Process.GetCurrentProcess().Id;
            var foreignRects = new List<Rectangle>();
            var scalingFactor = GetScalingFactor();

            User32.EnumWindows((hWnd, lParam) =>
            {
                if (!User32.IsWindowVisible(hWnd))
                    return true; // continue enumeration

                User32.GetWindowThreadProcessId(hWnd, out uint windowPid);

                // Check if we've reached our target window in z-order
                if (windowPid == currentPid && hWnd == targetHandle)
                    return false; // stop enumeration

                var rect = new User32.RECT();
                User32.GetWindowRect(hWnd, ref rect);
                // Scale from logical to physical coordinates to match screenRect
                var windowRect = rect.Rectangle * scalingFactor;
                var intersection = Rectangle.Intersect(screenRect, windowRect);

                if (intersection.IsEmpty)
                    return true; // no overlap, skip

                if (windowPid == currentPid)
                    return true; // skip Skyline-owned windows above target

                foreignRects.Add(intersection);

                return true; // continue enumeration
            }, IntPtr.Zero);

            return foreignRects;
        }

        /// <summary>
        /// Brings a control's form to the foreground.
        /// </summary>
        public static void ActivateForm(Control control)
        {
            User32.SetForegroundWindow(control.Handle);
            var form = control.FindForm();
            form?.Activate();
        }

        /// <summary>
        /// Saves a bitmap to a PNG file, creating directories as needed.
        /// </summary>
        public static void SaveToFile(string filePath, Bitmap bmp)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            var dirPath = Path.GetDirectoryName(filePath);
            if (dirPath != null && !Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            bmp.Save(filePath, ImageFormat.Png);
        }

        /// <summary>
        /// Checks whether screen capture is permitted, showing a confirmation dialog if needed.
        /// Must be called on the UI thread.
        /// </summary>
        /// <param name="wasFirstPrompt">True if the permission dialog was shown this call
        /// (callers may need to delay for repaint after the dialog dismisses).</param>
        /// <returns>True if permission is granted.</returns>
        public static bool EnsurePermission(out bool wasFirstPrompt)
        {
            wasFirstPrompt = false;
            if (Settings.Default.AllowMcpScreenCapture || _sessionPermissionGranted)
                return true;

            wasFirstPrompt = true;
            using (var dlg = new ScreenCapturePermissionDlg())
            {
                if (dlg.ShowDialog(Program.MainWindow) != DialogResult.OK)
                    return false;

                _sessionPermissionGranted = true;
                if (dlg.DoNotAskAgain)
                {
                    Settings.Default.AllowMcpScreenCapture = true;
                    Settings.Default.Save();
                }
            }
            return true;
        }

        // Private helpers

        private static Rectangle GetDockedFormBoundsInternal(DockableForm dockedForm)
        {
            var parentRelativeVBounds = dockedForm.Pane.Bounds;
            // The pane bounds do not include the border for Document state
            if (dockedForm.DockState == DockState.Document)
                parentRelativeVBounds.Inflate(SystemInformation.BorderSize.Width, SystemInformation.BorderSize.Width);
            return dockedForm.Pane.Parent.RectangleToScreen(parentRelativeVBounds);
        }

        private static Rectangle GetFramedWindowBoundsInternal(Control ctrl)
        {
            int width = (ctrl as Form)?.DesktopBounds.Width ?? ctrl.Width;
            // The drop shadow + border are 1/2 the difference between the window width and the client rect width
            // A border width is removed to keep the border around the window
            int borderOutsideClient = SystemInformation.BorderSize.Width;
            if (ctrl is FloatingWindow || ctrl.Size == ctrl.ClientRectangle.Size)
                borderOutsideClient = 0;
            int dropShadowWidth = (width - ctrl.ClientRectangle.Width) / 2 - borderOutsideClient;
            Size imageSize;
            Point sourcePoint;
            if (ctrl is Form)
            {
                // The snapshot size then removes the shadow width on both sides and from only the bottom
                imageSize = ctrl.Size + new PointAdditive(-2 * dropShadowWidth, -dropShadowWidth);
                // And the origin is shifted one shadow width to the right
                sourcePoint = ctrl.Location + new PointAdditive(dropShadowWidth, 0);
            }
            else
            {
                // Otherwise, it is just a control on a form without a drop shadow
                imageSize = ctrl.Size;
                sourcePoint = ctrl.Parent.PointToScreen(ctrl.Location);
            }
            return new Rectangle(sourcePoint, imageSize);
        }
    }
}
