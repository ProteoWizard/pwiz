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

namespace pwiz.Common.SystemUtil.PInvoke
{
    // CONSIDER: replace all out params with .NET types
    public static class User32
    {
        public const int CF_ENHMETAFILE = 14;
        
        public const int SB_THUMBPOSITION = 4;
        
        public static IntPtr False = new IntPtr(0);
        public static IntPtr True = new IntPtr(1);

        [Flags]
        public enum AnimateWindowFlags : uint
        {
            // ReSharper disable InconsistentNaming
            ROLL = 0x0, // Default, rolls out from edge when showing and into edge when hiding
            HORIZONTAL_POSITIVE = 0x1, // Right
            HORIZONTAL_NEGATIVE = 0x2, // Left
            VERTICAL_POSITIVE = 0x4, // Down
            VERTICAL_NEGATIVE = 0x8, // Up
            CENTER = 0x10,
            HIDE = 0x10000, // Hide the form
            ACTIVATE = 0x20000, // Activate the form
            SLIDE = 0x40000,
            BLEND = 0x80000
            // ReSharper restore InconsistentNaming
        }

        [Flags]
        public enum SetWindowPosFlags : uint
        {
            // ReSharper disable InconsistentNaming IdentifierTypo
            NOMOVE = 0x0002,
            NOSIZE = 0x0001,
            NOZORDER = 0x0004,
            NOACTIVATE = 0x0010,
            SHOWWINDOW = 0x0040
            // ReSharper restore InconsistentNaming IdentifierTypo
        }

        public enum WinMessageType : uint
        {
            // ReSharper disable InconsistentNaming IdentifierTypo
            PBM_SETSTATE = 0x0410,
            WM_SETREDRAW = 0x000B,
            WM_PAINT = 0x000F,
            WM_ERASEBKGND = 0x0014,
            WM_SETCURSOR = 0x0020,
            WM_MOUSEACTIVATE = 0x0021,
            WM_CALCSIZE = 0x0083,
            WM_NCHITTEST = 0x0084,
            WM_NCPAINT = 0x0085,
            WM_CHAR = 0x0102,
            WM_TIMER = 0x0113,
            WM_HSCROLL = 0x0114,
            WM_VSCROLL = 0x0115,
            WM_CHANGEUISTATE = 0x0127,
            WM_MOUSEMOVE = 0x0200,
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_MOUSELEAVE = 0x02A3
            // ReSharper restore InconsistentNaming IdentifierTypo
        }

        // Constants for WM_CHANGEUISTATE wParam
        // LOWORD = action (UIS_SET), HIWORD = flags (UISF_HIDEFOCUS | UISF_HIDEACCEL)
        // ReSharper disable InconsistentNaming
        public const int UIS_SET = 1;  // LOWORD action: set UI state flags
        public const int UISF_HIDEFOCUS = 0x1;  // Hide focus rectangles
        public const int UISF_HIDEACCEL = 0x2;  // Hide mnemonic underscores
        /// <summary>
        /// Combined wParam for WM_CHANGEUISTATE to hide both focus rectangles and mnemonic underscores.
        /// </summary>
        public static readonly IntPtr UISF_HIDEALL = (IntPtr)(UIS_SET | ((UISF_HIDEFOCUS | UISF_HIDEACCEL) << 16));
        // ReSharper restore InconsistentNaming

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        // ReSharper disable once InconsistentNaming IdentifierTypo
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        // ReSharper disable InconsistentNaming IdentifierTypo
        public struct PAINTSTRUCT
        {
            // ReSharper disable UnusedField.Compiler
#pragma warning disable 649
            public IntPtr hdc;
            public int fErase;
            public Rectangle rcPaint;
            public int fRestore;
            public int fIncUpdate;
            public int Reserved1;
            public int Reserved2;
            public int Reserved3;
            public int Reserved4;
            public int Reserved5;
            public int Reserved6;
            public int Reserved7;
            public int Reserved8;
#pragma warning restore 649
            // ReSharper restore UnusedField.Compiler
        }
        // ReSharper restore InconsistentNaming IdentifierTypo

        [StructLayout(LayoutKind.Sequential)]
        // ReSharper disable once InconsistentNaming
        public struct POINT
        {
            public int x;
            public int y;

            public Point Point => new Point(x, y);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }

            public Rectangle Rectangle => new Rectangle(left, top, right - left, bottom - top);

            public static RECT FromRectangle(Rectangle rect)
            {
                return new RECT(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }
        }

        [DllImport("user32.dll")]
        public static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);

        // CONSIDER (ekoneil): make dwFlags type AnimateWindowFlags after reconciling differing approaches to window animation in CustomTip vs FormAnimator
        [DllImport("user32.dll")]
        public static extern bool AnimateWindow(IntPtr hWnd, int dwTime, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT ps);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EmptyClipboard();
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);

        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(int tid, EnumThreadWindowsProc callback, IntPtr lp);
        public delegate bool EnumThreadWindowsProc(IntPtr hWnd, IntPtr lp);

        [DllImport("user32.dll")]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int buflen); 
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hRgn, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetOpenClipboardWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetScrollPos(IntPtr hWnd, Orientation orientation);

        [DllImport("user32.dll")]
        public static extern bool GetScrollRange(IntPtr hWnd, Orientation orientation, out int lpMinPos, out int lpMaxPos);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll", EntryPoint = "OpenClipboard", SetLastError = true)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        public static extern bool PostMessageA(IntPtr hWnd, WinMessageType msgType, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, WinMessageType msgType, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height, SetWindowPosFlags uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int ShowWindow(IntPtr hWnd, short cmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref PInvokeCommon.SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        public static Control GetFocusedControl()
        {
            Control focusedControl = null;
            // To get hold of the focused control:
            var focusedHandle = GetFocus();
            if (focusedHandle != IntPtr.Zero)
            {
                // If the focused Control is not a .Net control, then this will return null.
                focusedControl = Control.FromHandle(focusedHandle);
            }
            return focusedControl;
        }

        [DllImport("user32.dll")]
        public static extern bool HideCaret(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int SetScrollPos(IntPtr hWnd, Orientation orientation, int nPos, bool bRedraw);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetFocus();
    }
}