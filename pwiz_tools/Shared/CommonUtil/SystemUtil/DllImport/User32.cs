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
using System.Text;
using System.Windows.Forms;

// TODO: namespace - (1) SystemUtil.DllImport, (2) SystemUtil.Win32, or (3) SystemUtil.PInvoke?
namespace pwiz.Common.SystemUtil.DllImport
{
    // Allowed uses of InteropServices
    //      Common.SystemUtil.DllImport.* - allowed to use InteropServices
    //      pwiz_tools\Shared\zedgraph\ZedGraph\ZedGraphControl.ContextMenu.cs - avoid changing ZedGraph

    public static class User32
    {
        // TODO: add Clipboard extension methods

        // TODO: setting constant values - use (1) decimal or (2) hex?
        public const int WM_SETREDRAW = 11;
        public const int WM_VSCROLL = 0x0115;
        public const int SB_THUMBPOSITION = 4;

        public const uint PBM_SETSTATE = 0x0410; // 1040

        [Flags]
        public enum AnimateWindowFlags : uint
        {
            HORIZONTAL_POSITIVE = 0x1,
            HORIZONTAL_NEGATIVE = 0x2,
            VERTICAL_POSITIVE = 0x4,
            VERTICAL_NEGATIVE = 0x8,
            CENTER = 0x10,
            /// <summary>
            /// Hide the form
            /// </summary>
            HIDE = 0x10000,  
            /// <summary>
            /// Activate the form
            /// </summary>
            ACTIVATE = 0x20000,
            SLIDE = 0x40000,
            BLEND = 0x80000
    }

        // TODO: declaring constants - use (1) constant primitives, (2) enum, (3) fields on a static class?
        // TODO: constant naming convention - (1) win32 style (example: SWP) or (2) readable (example: SetWindowPosFlags)?
        [Flags]
        public enum SetWindowPosFlags : uint
        {
            // ReSharper disable IdentifierTypo
            NOMOVE = 0x0002,
            NOSIZE = 0x0001,
            NOACTIVATE = 0x0010,
            SHOWWINDOW = 0x0040
            // ReSharper disable IdentifierTypo
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;

            public Point Point
            {
                get
                {
                    return new Point(x, y);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            // ReSharper disable FieldCanBeMadeReadOnly.Global
            public int left;
            public int top;
            public int right;
            public int bottom;
            // ReSharper restore FieldCanBeMadeReadOnly.Global

            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }

            public Rectangle Rectangle
            {
                get
                {
                    return new Rectangle(left, top, right - left, bottom - top);
                }
            }

            public static RECT FromRectangle(Rectangle rect)
            {
                return new RECT(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;

            public Size Size
            {
                get
                {
                    return new Size(cx, cy);
                }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);

        /// <summary>
        /// Windows API function to animate a window.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool AnimateWindow(IntPtr hWnd, int dwTime, int dwFlags);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

        // TODO: review delegate example
        public delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lp);
        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(int tid, EnumThreadWndProc callback, IntPtr lp);
        
        [DllImport("user32.dll")]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int buflen);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetScrollPos(IntPtr hWnd, int nBar);

        [DllImport("user32.dll")]
        public static extern bool GetScrollRange(IntPtr hWnd, int nBar, out int lpMinPos, out int lpMaxPos);

        // TODO: make extension method
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32.dll")]
        internal static extern bool HideCaret(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);

        [DllImport("user32.dll")]
        public static extern bool PostMessageA(IntPtr hWnd, int nBar, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);

        // TODO: standardize on one sendMessage 
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        public static Control GetFocusedControl()
        {
            Control focusedControl = null;
            // To get hold of the focused control:
            IntPtr focusedHandle = GetFocus();
            if (focusedHandle != IntPtr.Zero)
            {
                // If the focused Control is not a .Net control, then this will return null.
                focusedControl = Control.FromHandle(focusedHandle);
            }
            return focusedControl;
        }
    }
}