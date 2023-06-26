/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace pwiz.Common.Controls
{
    public class AutoScrollTextBox : TextBox
    {
        // Constants for extern calls to various scrollbar functions
        //private const int SB_HORZ = 0x0;
        private const int SB_VERT = 0x1;
        //private const int WM_HSCROLL = 0x114;
        private const int WM_VSCROLL = 0x115;
        private const int WM_SETREDRAW = 11;
        private const int SB_THUMBPOSITION = 4;
        //private const int SB_BOTTOM = 7;
        //private const int SB_OFFSET = 13;

        [DllImport("user32.dll")]
        private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetScrollPos(IntPtr hWnd, int nBar);
        [DllImport("user32.dll")]
        private static extern bool PostMessageA(IntPtr hWnd, int nBar, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool GetScrollRange(IntPtr hWnd, int nBar, out int lpMinPos, out int lpMaxPos);
        [DllImport("user32.dll")]
        private static extern bool LockWindowUpdate(IntPtr hWndLock);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        /// <summary>
        /// Stop updating control (helps prevent flickering).
        /// </summary>
        public void SuspendDrawing()
        {
            SendMessage(Handle, WM_SETREDRAW, 0, IntPtr.Zero);
        }

        public void ResumeDrawing()
        {
            SendMessage(Handle, WM_SETREDRAW, 1, IntPtr.Zero);
            Invalidate(true);
            Update();
        }

        // adapted from StackOverflow questions:
        // https://stackoverflow.com/questions/1743448/auto-scrolling-text-box-uses-more-memory-than-expected

        public void AppendLineWithAutoScroll(string line)
        {
            bool bottomFlag = false;
            int VSmax;
            int sbOffset;
            int savedVpos;

            // Win32 magic to keep the textbox scrolling to the newest append to the textbox unless
            // the user has moved the scrollbox up
            sbOffset = (ClientSize.Height - SystemInformation.HorizontalScrollBarHeight) / Font.Height;
            savedVpos = GetScrollPos(Handle, SB_VERT);
            GetScrollRange(Handle, SB_VERT, out _, out VSmax);
            if (savedVpos >= (VSmax - sbOffset - 1))
                bottomFlag = true;
            SuspendDrawing();
            AppendText(line);
            if (bottomFlag)
            {
                GetScrollRange(Handle, SB_VERT, out _, out VSmax);
                savedVpos = VSmax - sbOffset;
            }
            SetScrollPos(Handle, SB_VERT, savedVpos, true);
            PostMessageA(Handle, WM_VSCROLL, SB_THUMBPOSITION + 0x10000 * savedVpos, 0);
            ResumeDrawing();
        }
    }
}