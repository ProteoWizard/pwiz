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
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.DllImport
{
    public static class User32Extensions
    {
        /// <summary>
        /// Adjust z-order without activating
        /// </summary>
        public static void BringWindowToSameLevelWithoutActivating(this Form targetWindow, IntPtr referenceWindowHandle)
        {
            int flags = (int)User32.SWP.NOMOVE | (int)User32.SWP.NOSIZE | (int)User32.SWP.NOACTIVATE | (int)User32.SWP.SHOWWINDOW;

            User32.SetWindowPos(targetWindow.Handle, referenceWindowHandle, 0, 0, 0, 0, (uint)flags);
        }

        public static int GetScrollPos(this Control control, Orientation sd)
        {
            return User32.GetScrollPos(control.Handle, (int)sd);
        }

        public static void HideCaret(this TextBox control)
        {
            User32.HideCaret(control.Handle);
        }

        public static void SetForegroundWindow(this Control control)
        {
            User32.SetForegroundWindow(control.Handle);
        }

        public static void SetScrollPos(this Control control, Orientation sd, int pos)
        {
            User32.SetScrollPos(control.Handle, (int)sd, pos, true);
        }
    }
}
