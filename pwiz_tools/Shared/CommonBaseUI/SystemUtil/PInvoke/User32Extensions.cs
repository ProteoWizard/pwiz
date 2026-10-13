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

using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.PInvoke
{
    public static class User32Extensions
    {
        public static int GetScrollPos(this Control control, Orientation orientation)
        {
            return User32.GetScrollPos(control.Handle, orientation);
        }

        public static void HideCaret(this Control control)
        {
            User32.HideCaret(control.Handle);
        }

        public static void SetScrollPos(this Control control, Orientation orientation, int pos)
        {
            User32.SetScrollPos(control.Handle, orientation, pos, true);
            // Post a scroll message to make the control actually scroll its content.
            // SetScrollPos only moves the scrollbar thumb without scrolling the viewport.
            var msg = orientation == Orientation.Horizontal
                ? User32.WinMessageType.WM_HSCROLL
                : User32.WinMessageType.WM_VSCROLL;
            User32.PostMessageA(control.Handle, msg,
                User32.SB_THUMBPOSITION + 0x10000 * pos, 0);
        }
    }
}
