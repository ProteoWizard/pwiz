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

// TODO: add Clipboard extension methods
// TODO: move to pwiz.Common.SystemUtil.Win32 namespace?
namespace pwiz.Common.SystemUtil.DllImport
{
    public static class User32
    {
        // TODO: improve typing by wrapping in an enum? Even an enum per prefix (WM, PBM, SWP)?
        // TODO: standardize on either decimal or hex?
        public const int WM_SETREDRAW = 11;
        public const uint PBM_SETSTATE = 0x0410; // 1040

        public enum AW
        {
            /// <summary>
            /// Hide the form
            /// </summary>
            HIDE = 0x10000,  
            /// <summary>
            /// Activate the form
            /// </summary>
            ACTIVATE = 0x20000
        }

        public enum SWP
        {
            // ReSharper disable IdentifierTypo
            NOMOVE = 0x0002,
            NOSIZE = 0x0001,
            NOACTIVATE = 0x0010,
            SHOWWINDOW = 0x0040
            // ReSharper disable IdentifierTypo
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

        /// <summary>
        /// Windows API function to animate a window.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool AnimateWindow(IntPtr hWnd, int dwTime, int dwFlags);

        [DllImport("user32.dll")]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int buflen);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32.dll")]
        public static extern bool HideCaret(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);

        // TODO: standardize on one sendMessage 
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        public static bool SetWindowPos(Form targetWindow, IntPtr referenceWindowHandle, 
            int X, int Y, int cx, int cy, params SWP[] flags)
        {
            int flagsInt = 0;
            Array.ForEach(flags, delegate(SWP flag) { flagsInt |= (int)flag; });
    
            return SetWindowPos(targetWindow.Handle, referenceWindowHandle, X, Y, cx, cy, (uint)flagsInt);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }
}