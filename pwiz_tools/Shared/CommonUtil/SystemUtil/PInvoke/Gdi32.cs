/*
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.PInvoke
{
    public static class Gdi32
    {
        // ReSharper disable once InconsistentNaming
        public enum DeviceCap : uint
        {
            // ReSharper disable InconsistentNaming IdentifierTypo
            VERTRES = 10,
            DESKTOPVERTRES = 117
            // ReSharper restore InconsistentNaming IdentifierTypo
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    
        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(IntPtr hdc, DeviceCap flag);

        /// <summary>
        /// Draws a control onto a <see cref="Graphics"/>, limited to a clip rectangle, by sending it WM_PRINT (as
        /// <see cref="Control.DrawToBitmap"/> does) into the graphics' own device context. Only the part of the
        /// control inside the clip is painted, however large the control, and no temporary bitmap is needed.
        /// </summary>
        /// <param name="g">The graphics to draw onto</param>
        /// <param name="control">The control to draw, which must be owned by the calling thread</param>
        /// <param name="bounds">Where the control's outer (window) bounds fall, in the graphics' coordinates</param>
        /// <param name="clip">The part of the graphics the control may draw on</param>
        public static void PrintControl(Graphics g, Control control, Rectangle bounds, Rectangle clip)
        {
            var visible = Rectangle.Intersect(bounds, clip);
            if (visible.IsEmpty)
                return;
            var hdc = g.GetHdc();
            try
            {
                int savedDc = SaveDC(hdc);
                try
                {
                    SetViewportOrgEx(hdc, bounds.X, bounds.Y, IntPtr.Zero);
                    // The clip rectangle is in the control's own coordinates, now that the origin is moved
                    IntersectClipRect(hdc, visible.Left - bounds.X, visible.Top - bounds.Y,
                        visible.Right - bounds.X, visible.Bottom - bounds.Y);
                    User32.SendMessage(control.Handle, User32.WinMessageType.WM_PRINT, hdc,
                        (IntPtr)(PrintFlags.CHILDREN | PrintFlags.CLIENT | PrintFlags.ERASEBKGND | PrintFlags.NONCLIENT));
                }
                finally
                {
                    RestoreDC(hdc, savedDc);
                }
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }

        // WM_PRINT lParam: what the window draws into the device context
        [Flags]
        private enum PrintFlags
        {
            // ReSharper disable InconsistentNaming IdentifierTypo
            NONCLIENT = 0x02,  // Window frame, caption and scroll bars
            CLIENT = 0x04,  // Client area
            ERASEBKGND = 0x08,  // Erase the background first
            CHILDREN = 0x10  // Visible child windows
            // ReSharper restore InconsistentNaming IdentifierTypo
        }

        [DllImport("gdi32.dll")]
        private static extern int SaveDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool RestoreDC(IntPtr hdc, int nSavedDC);

        [DllImport("gdi32.dll")]
        private static extern bool SetViewportOrgEx(IntPtr hdc, int x, int y, IntPtr lpPoint);

        [DllImport("gdi32.dll")]
        private static extern int IntersectClipRect(IntPtr hdc, int left, int top, int right, int bottom);
    }
}