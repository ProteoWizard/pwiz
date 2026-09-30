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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Result of <see cref="ScreenCapture.EnsurePermission"/>. The pipe-thread
    /// caller never blocks on a user click: a first-time prompt schedules the
    /// dialog asynchronously and returns <see cref="PermissionResult.pending"/>,
    /// letting the LLM surface the prompt and retry.
    /// <see cref="PermissionResult.unavailable"/> covers transient inability to
    /// host a dialog at all (Skyline mid-startup or mid-shutdown) so the LLM
    /// is not falsely told a dialog has opened.
    /// </summary>
    public enum PermissionResult
    {
        granted,
        denied,
        pending,
        unavailable
    }

    /// <summary>
    /// Production screen capture utility for Skyline forms.
    /// Provides DPI-aware capture with redaction of non-Skyline windows, and off-screen
    /// rendering for when the screen cannot supply the image.
    /// </summary>
    public static class ScreenCapture
    {
        private static volatile bool _sessionPermissionGranted;
        private static volatile bool _sessionDenied;
        // 0 = no prompt outstanding, 1 = prompt scheduled or open.
        // Treated as a bool but stored as int so Interlocked.CompareExchange
        // can give us a single atomic check-and-set across concurrent pipe threads.
        private static int _promptPending;

        /// <summary>
        /// Resets the session-level screen capture permission. Used by tests.
        /// </summary>
        public static void ResetSessionPermission()
        {
            _sessionPermissionGranted = false;
            _sessionDenied = false;
            Interlocked.Exchange(ref _promptPending, 0);
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

            var dockableForm = ctrl as DockableForm;
            if (dockableForm != null && IsDocked(dockableForm))
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
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(screenRect.Location, Point.Empty, screenRect.Size);
            }
            catch (Exception)
            {
                // CopyFromScreen can fail if the desktop session disconnects mid-capture
                // (e.g. Remote Desktop disconnect). Return blank bitmap rather than crashing.
            }
            return bmp;
        }

        /// <summary>
        /// Returns an image of a Skyline form. The image may be incomplete if parts of the form could not be drawn,
        /// but if no image of the form could be made at all, this throws. Must be called on the form's thread.
        /// </summary>
        public static Bitmap GetFormImage(Control targetForm)
        {
            // Copy the screen when it shows the whole form. Otherwise render the form off-screen: there is no
            // desktop to copy from (e.g. a disconnected Remote Desktop session), the form is not on the monitors
            // (e.g. the offscreen test mode), or a window of another application covers it. If rendering fails and
            // the desktop is available, fall back to a screen copy with the covering windows redacted.
            if (!IsDesktopAvailable())
            {
                return RenderControl(GetRenderedControl(targetForm)) ??
                       throw new InvalidOperationException(JsonUiService.LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE);
            }
            var screenRect = GetWindowRectangle(targetForm);
            if (IsShownOnScreen(screenRect, targetForm))
                return CaptureScreen(screenRect);
            Exception renderException = null;
            try
            {
                var rendered = RenderControl(GetRenderedControl(targetForm));
                if (rendered != null)
                    return rendered;
            }
            catch (Exception e)
            {
                renderException = e;
            }
            try
            {
                return CaptureAndRedact(screenRect, targetForm);
            }
            catch (Exception e) when (renderException != null)
            {
                // AggregateException's own message does not include its inner exceptions' messages
                throw new AggregateException(TextUtil.LineSeparate(renderException.Message, e.Message),
                    renderException, e);
            }
        }

        /// <summary>
        /// Renders a control, and the forms and user controls nested inside it, into a new bitmap without reading
        /// the screen, so it works when the control is covered by other windows or there is no desktop at all.
        /// Nested forms and user controls are rendered individually and drawn over their parent, because printing
        /// the parent alone (as <see cref="Control.DrawToBitmap"/> does) does not reliably include them (e.g.
        /// docked panes).
        /// <para>This also takes the screenshots attached to an error report, when Skyline may already be in a bad
        /// state, so it must not make things worse: it skips anything it cannot safely draw (disposed, hidden,
        /// without a handle, or owned by another thread whose message loop may be stuck), returning null if that is
        /// the control itself. A nested form or user control that throws while drawing is left out; the control
        /// itself throwing is thrown to the caller. Must be called on the control's thread.</para>
        /// </summary>
        public static Bitmap RenderControl(Control control)
        {
            if (!CanRender(control))
                return null;
            var bitmap = new Bitmap(control.Width, control.Height);
            try
            {
                var origin = GetScreenLocation(control);
                var bounds = new Rectangle(Point.Empty, control.Size);
                using var g = Graphics.FromImage(bitmap);
                DrawOnto(g, control, origin, bounds);
                DrawNestedControls(g, control, origin, bounds);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
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

            // Collect screen rects of non-Skyline windows above our target in z-order
            var foreignRects = GetForeignWindowRects(screenRect, GetTopLevelHandle(targetForm));
            if (foreignRects.Count == 0)
                return bmp;

            // Redact foreign window regions
            using var g = Graphics.FromImage(bmp);
            using var redactRegion = new Region(Rectangle.Empty);
            foreach (var foreignRect in foreignRects)
            {
                var bmpRect = new Rectangle(
                    foreignRect.X - screenRect.X,
                    foreignRect.Y - screenRect.Y,
                    foreignRect.Width, foreignRect.Height);
                redactRegion.Union(bmpRect);
            }
            using var brush = new SolidBrush(Color.Cyan);
            g.FillRegion(brush, redactRegion);
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
            uint currentPid = Kernel32.GetCurrentProcessId();
            var foreignRects = new List<Rectangle>();
            var scalingFactor = GetScalingFactor();

            foreach (var hWnd in User32.EnumWindows()) // z-order, top to bottom
            {
                if (!User32.IsWindowVisible(hWnd))
                    continue;

                User32.GetWindowThreadProcessId(hWnd, out uint windowPid);

                // Stop once we reach our target window in z-order -- nothing below it can obscure the target.
                if (windowPid == currentPid && hWnd == targetHandle)
                    break;

                var rect = new User32.RECT();
                User32.GetWindowRect(hWnd, ref rect);
                // Scale from logical to physical coordinates to match screenRect
                var windowRect = rect.Rectangle * scalingFactor;
                var intersection = Rectangle.Intersect(screenRect, windowRect);

                if (intersection.Width == 0 || intersection.Height == 0)
                    continue; // no overlap, skip

                if (windowPid == currentPid)
                    continue; // skip Skyline-owned windows above target

                foreignRects.Add(intersection);
            }

            return foreignRects;
        }

        // Whether the screen shows the whole form, so that a copy of the screen is its image: the rectangle lies on
        // the monitors and no window of another application covers it. A band as wide as the window border is
        // ignored at the edges. The rectangle keeps part of the frame's invisible resize border, which a maximized
        // window pushes past the monitor edge and under the taskbar, and other windows' invisible borders reach
        // into it without hiding anything.
        private static bool IsShownOnScreen(Rectangle screenRect, Control targetForm)
        {
            int edge = GetBorderWidth(targetForm);
            var inner = Rectangle.Inflate(screenRect, -edge, -edge);
            if (inner.Width <= 0 || inner.Height <= 0)
                return false;
            return IsOnMonitors(inner) && GetForeignWindowRects(inner, GetTopLevelHandle(targetForm)).Count == 0;
        }

        // The width of the frame around the form's top-level window, invisible resize border included, in the
        // physical pixels of a screen rectangle; the system's frame width for a window drawn without a frame.
        private static int GetBorderWidth(Control targetForm)
        {
            var topLevel = targetForm.TopLevelControl ?? targetForm;
            int border = (topLevel.Width - topLevel.ClientSize.Width) / 2;
            if (border <= 0)
                border = SystemInformation.FrameBorderSize.Width;
            return (new Rectangle(0, 0, border, border) * GetScalingFactor()).Width;
        }

        // Whether every pixel of the rectangle is on a monitor. Monitors do not overlap, so the rectangle is covered
        // exactly when the areas it shares with each of them add up to its own.
        private static bool IsOnMonitors(Rectangle screenRect)
        {
            var scalingFactor = GetScalingFactor();
            long onMonitors = 0;
            foreach (var screen in Screen.AllScreens)
            {
                var shared = Rectangle.Intersect(screenRect, screen.Bounds * scalingFactor);
                onMonitors += (long)shared.Width * shared.Height;
            }
            return onMonitors == (long)screenRect.Width * screenRect.Height;
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
        /// Returns the current screen-capture permission state. Must be called from a
        /// background thread (e.g. the JSON-RPC pipe thread); the first-time prompt
        /// path schedules the modal dialog on the UI thread via
        /// <see cref="Control.BeginInvoke(Delegate)"/> and returns
        /// <see cref="PermissionResult.pending"/> immediately, so the calling thread
        /// never blocks on a user click. Subsequent calls while the dialog is open
        /// also return <see cref="PermissionResult.pending"/> without opening a
        /// second dialog.
        /// </summary>
        public static PermissionResult EnsurePermission()
        {
            // The prompt needs a UI window to own and marshal it. Normally that is the main window,
            // but while the StartPage is showing the main window does not exist yet, so fall back to
            // it. The pipe thread can reach this line before either window exists (early startup) or
            // after the main window has been cleared (shutdown), in which case we cannot prompt.
            var ownerWindow = (Form)Program.MainWindow ?? Program.StartWindow;
            if (ownerWindow == null)
                return PermissionResult.unavailable;

            Assume.IsTrue(ownerWindow.InvokeRequired);

            if (Settings.Default.AllowMcpScreenCapture || _sessionPermissionGranted)
                return PermissionResult.granted;
            if (_sessionDenied)
                return PermissionResult.denied;

            // Atomic transition into the pending state. If another pipe thread
            // already won this race, fall through and return pending without
            // scheduling a second dialog.
            if (Interlocked.CompareExchange(ref _promptPending, 1, 0) != 0)
                return PermissionResult.pending;

            // SafeBeginInvoke returns false if the owner window has lost its handle
            // (e.g. Skyline is shutting down). Clear the gate and report
            // unavailable rather than pending so the LLM is not told to wait
            // on a dialog that will never open.
            if (!CommonActionUtil.SafeBeginInvoke(ownerWindow, ShowPermissionDialog))
            {
                Interlocked.Exchange(ref _promptPending, 0);
                return PermissionResult.unavailable;
            }
            return PermissionResult.pending;
        }

        private static void ShowPermissionDialog()
        {
            try
            {
                using var dlg = new ScreenCapturePermissionDlg();
                var owner = (Form)Program.MainWindow ?? Program.StartWindow;
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    _sessionPermissionGranted = true;
                    if (dlg.DoNotAskAgain)
                    {
                        Settings.Default.AllowMcpScreenCapture = true;
                        Settings.Default.Save();
                    }
                }
                else
                {
                    _sessionDenied = true;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _promptPending, 0);
            }
        }

        // Private helpers

        // Finds the top-level window handle for z-order comparison.
        // For docked panels, this returns SkylineWindow.
        // For floating panels, this returns the FloatingWindow.
        private static IntPtr GetTopLevelHandle(Control targetForm)
        {
            return (FormUtil.FindTopLevelOwner(targetForm) ?? targetForm).Handle;
        }

        // Returns the control whose area GetWindowRectangle captures from the screen: the pane of a docked form
        // (which includes its caption), the floating window holding a floating form, or else the control itself.
        private static Control GetRenderedControl(Control ctrl)
        {
            if (ctrl is DockableForm dockableForm && IsDocked(dockableForm))
                return dockableForm.Pane;
            return FindParent<FloatingWindow>(ctrl) ?? ctrl;
        }

        private static bool IsDocked(DockableForm dockableForm)
        {
            var dockedStates = new[] { DockState.DockBottom, DockState.DockLeft, DockState.DockRight, DockState.DockTop, DockState.Document };
            return dockedStates.Contains(dockableForm.DockState);
        }

        // Draws the forms and user controls nested inside a control, each at its position relative to the
        // rendered control's origin. A child is clipped to its parent's client area so a child scrolled out of
        // view does not draw over its surroundings. Controls are drawn from the bottom of the z-order up (the
        // Controls collection lists the topmost first), so overlapping children end up stacked as on screen.
        private static void DrawNestedControls(Graphics g, Control parent, Point origin, Rectangle clip)
        {
            var clientRect = parent.RectangleToScreen(parent.ClientRectangle);
            clientRect.Offset(-origin.X, -origin.Y);
            clip.Intersect(clientRect);
            if (clip.IsEmpty)
                return;
            var children = parent.Controls.Cast<Control>().ToArray();
            for (int i = children.Length - 1; i >= 0; i--)
            {
                var child = children[i];
                if (!CanRender(child))
                    continue;
                if (child is Form || child is UserControl)
                {
                    try
                    {
                        DrawOnto(g, child, origin, clip);
                    }
                    catch (Exception e)
                    {
                        // Leave out the child that failed and keep drawing the rest
                        Messages.WriteAsyncDebugMessage(@"Exception rendering {0}: {1}", child.GetType().Name, e);
                    }
                }
                DrawNestedControls(g, child, origin, clip);
            }
        }

        // Draws a single control onto the bitmap at its position relative to the origin, limited to the clip.
        // Only the visible part is painted (see Gdi32.PrintControl), however large the control.
        private static void DrawOnto(Graphics g, Control control, Point origin, Rectangle clip)
        {
            var location = GetScreenLocation(control);
            var bounds = new Rectangle(location.X - origin.X, location.Y - origin.Y, control.Width, control.Height);
            Gdi32.PrintControl(g, control, bounds, clip);
        }

        // Whether a control can be drawn without side effects or risk: reading its Handle would create a missing
        // one, and a control owned by another thread would be sent WM_PRINT on that thread, which may never respond.
        private static bool CanRender(Control control)
        {
            if (control == null || control.IsDisposed || control.Disposing || !control.IsHandleCreated)
                return false;
            if (control.InvokeRequired || !control.Visible)
                return false;
            if (control is Form form && form.WindowState == FormWindowState.Minimized)
                return false;
            return control.Width > 0 && control.Height > 0;
        }

        // The screen location of the control's outer (window) bounds.
        private static Point GetScreenLocation(Control control)
        {
            return control.Parent != null ? control.Parent.PointToScreen(control.Location) : control.Location;
        }

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
