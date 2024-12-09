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

using System.Runtime.InteropServices;
using System;
using System.Drawing;
using System.Text;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Helper class for calling Win32 APIs rather than using [DllImport] attributes directly.
    /// This consolidates function declarations but does not try to hide all Win32 abstractions
    /// from callers. A limited number of Skyline classes are allowed to use Win32 APIs
    /// directly if putting those calls here is onerous.
    /// </summary>
    public static class DllImport
    {
        public static class User32
        {
            // TODO: improve typing by wrapping in an enum?
            // TODO: standardize on exactly one of decimal or hex?
            public const int WM_SETREDRAW = 11;
            public const uint PBM_SETSTATE = 0x0410; // 1040

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

            [DllImport("user32.dll")]
            public static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int buflen);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

            [DllImport("user32.dll")]
            public static extern bool HideCaret(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }

        public static class Gdi32
        { 
            public enum DeviceCap
            {
                // ReSharper disable IdentifierTypo
                VERTRES = 10,
                DESKTOPVERTRES = 117,
                // ReSharper restore IdentifierTypo
            }

            [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    
            [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
            public static extern bool DeleteDC(IntPtr hDC);

            [DllImport(dllName: "gdi32.dll")]
            private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

            public static int GetDeviceCaps(IntPtr hdc, DeviceCap value)
            {
                return DllImport.Gdi32.GetDeviceCaps(hdc, (int)value);
            }

            [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        }

        public static class Kernel32
        {
            [DllImport("kernel32.dll")]
            public static extern int GetCurrentThreadId();
        }
    }
}