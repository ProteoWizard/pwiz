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
    // TODO: add static check preventing use of InteropServics. Except for allowed uses:
    //      pwiz_tools\Shared\CommonUtil\SystemUtil\PInvoke\* - win32 interop
    //      pwiz_tools\Shared\zedgraph\ZedGraph\ZedGraphControl.ContextMenu.cs - avoid changing ZedGraph
    //      pwiz_tools\Skyline\TestUtil\TestFunctional.cs - uses ExternalException to catch Clipboard errors
    //      pwiz_tools\Skyline\Util\UtilUIExtra.cs - uses ExternalException to catch Clipboard errors
    //      pwiz_tools\Skyline\Util\MemoryInfo.cs - most of this class is a win32 struct only used therein
    //      pwiz_tools\Skyline\TestUtil\FileLockingProcessFinder.cs - 100 LOC used only locally to debug file locking problems
    //      pwiz_tools\Skyline\TestRunnerLib\RunTests.cs - 62 LOC related to heap management
    //      pwiz_tools\Skyline\TestRunner\UnusedPortFinder.cs - > 150 LOC related to marshaling info for TCP port management

    public static class User32
    {
        // TODO: standardize constant values on hex (ideal). Minimally, be consistent for related constants.
        // ReSharper disable InconsistentNaming IdentifierTypo
        public const int CF_ENHMETAFILE = 14;
        public const int GWL_STYLE = -16;
        public const int SB_THUMBPOSITION = 4;
        public const int WM_SETREDRAW = 11;
        public const int WM_VSCROLL = 0x0115;
        public const int WS_HSCROLL = 0x00100000;
        public const int WS_VSCROLL = 0x00200000;

        public const uint PBM_SETSTATE = 0x0410; // 1040

        public const ulong TARGETWINDOW = WS_BORDER | WS_VISIBLE;
        public const ulong WS_BORDER = 0x00800000L;
        public const ulong WS_VISIBLE = 0x10000000L;
        // ReSharper restore InconsistentNaming IdentifierTypo

        public static IntPtr FALSE = new IntPtr(0);
        public static IntPtr TRUE = new IntPtr(1);

        [Flags]
        public enum AnimateWindowFlags : uint
        {
            // ReSharper disable InconsistentNaming
            HORIZONTAL_POSITIVE = 0x1,
            HORIZONTAL_NEGATIVE = 0x2,
            VERTICAL_POSITIVE = 0x4,
            VERTICAL_NEGATIVE = 0x8,
            CENTER = 0x10,
            HIDE = 0x10000, // Hide the form
            ACTIVATE = 0x20000, // Activate the form
            SLIDE = 0x40000,
            BLEND = 0x80000
            // ReSharper restore InconsistentNaming
        }

        public enum HandleType
        {
            total = -1,
            gdi = 0,
            user = 1
        }

        [Flags]
        public enum SetWindowPosFlags : uint
        {
            // ReSharper disable InconsistentNaming IdentifierTypo
            NOMOVE = 0x0002,
            NOSIZE = 0x0001,
            NOACTIVATE = 0x0010,
            SHOWWINDOW = 0x0040
            // ReSharper restore InconsistentNaming IdentifierTypo
        }

        public enum WindowsMessageType : uint
        {
            // ReSharper disable InconsistentNaming IdentifierTypo
            PAINT = 0x000F,
            ERASEBKGND = 0x0014,
            SETCURSOR = 0x0020,
            MOUSEACTIVATE = 0x0021,
            CALCSIZE = 0x0083,
            NCHITTEST = 0x0084,
            NCPAINT = 0x0085,
            CHAR = 0x0102,
            TIMER = 0x0113,
            MOUSEMOVE = 0x0200,
            LBUTTONDOWN = 0x0201,
            LBUTTONUP = 0x0202,
            MOUSELEAVE = 0x02A3
            // ReSharper restore InconsistentNaming IdentifierTypo
        }

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
        // ReSharper disable once InconsistentNaming
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

            public Rectangle Rectangle => new Rectangle(left, top, right - left, bottom - top);

            public static RECT FromRectangle(Rectangle rect)
            {
                return new RECT(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }
        }

        // TODO: declaring DLL name - use (1) DllImport(nameof(User32)) or (2) DllImport("user32.dll")
        [DllImport(nameof(User32))]
        public static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);

        // TODO (ekoneil): remove
        [DllImport("user32.dll")]
        public static extern bool AnimateWindow(IntPtr hWnd, int dwTime, int dwFlags);

        [DllImport("user32.dll")]
        public static extern bool AnimateWindow(IntPtr hWnd, int dwTime, AnimateWindowFlags flags);

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
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int buflen);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hRgn, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        public static extern IntPtr GetOpenClipboardWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetScrollPos(IntPtr hWnd, int nBar);

        [DllImport("user32.dll")]
        public static extern bool GetScrollRange(IntPtr hWnd, int nBar, out int lpMinPos, out int lpMaxPos);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern ulong GetWindowLongA(IntPtr hWnd, int nIndex);

        // TODO: add extension method?
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);

        [DllImport("user32.dll", EntryPoint = "OpenClipboard", SetLastError = true)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        public static extern bool PostMessageA(IntPtr hWnd, int nBar, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);

        // TODO: standardize on one sendMessage 
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetKeyboardState(byte[] lpKeyState);

        // TODO (ekoneil): delete
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndAfter, int X, int Y, int Width, int Height, uint flags);

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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        internal static extern int GetGuiResources(IntPtr hProcess, int uiFlags);

        [DllImport("user32.dll")]
        internal static extern bool HideCaret(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);
    }
}