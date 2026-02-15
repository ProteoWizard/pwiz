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
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Common.Controls
{
    public class AutoScrollTextBox : TextBox
    {
        /// <summary>
        /// Stop updating control (helps prevent flickering).
        /// </summary>
        public void SuspendDrawing()
        {
            User32.SendMessage(Handle, User32.WinMessageType.WM_SETREDRAW, User32.False, IntPtr.Zero);
        }

        public void ResumeDrawing()
        {
            User32.SendMessage(Handle, User32.WinMessageType.WM_SETREDRAW, User32.True, IntPtr.Zero);
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
            savedVpos = this.GetScrollPos(Orientation.Vertical);
            User32.GetScrollRange(Handle, Orientation.Vertical, out _, out VSmax);
            if (savedVpos >= (VSmax - sbOffset - 1))
                bottomFlag = true;
            SuspendDrawing();
            AppendText(line);
            if (bottomFlag)
            {
                User32.GetScrollRange(Handle, Orientation.Vertical, out _, out VSmax);
                savedVpos = VSmax - sbOffset;
            }
            this.SetScrollPos(Orientation.Vertical, savedVpos);
            User32.PostMessageA(Handle,
                User32.WinMessageType.WM_VSCROLL,
                User32.SB_THUMBPOSITION + 0x10000 * savedVpos,
                0);
            ResumeDrawing();
        }
    }
}