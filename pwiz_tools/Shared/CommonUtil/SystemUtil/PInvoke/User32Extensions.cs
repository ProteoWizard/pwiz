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
using System.Diagnostics;
using System.Windows.Forms;

namespace pwiz.Common.SystemUtil.PInvoke
{
    public static class User32Extensions
    {
        /// <summary>
        /// Adjust z-order without activating
        /// </summary>
        public static void BringWindowToSameLevelWithoutActivating(this Form targetWindow, IntPtr referenceWindowHandle)
        {
            const User32.SetWindowPosFlags flags = User32.SetWindowPosFlags.NOMOVE | 
                                                   User32.SetWindowPosFlags.NOSIZE | 
                                                   User32.SetWindowPosFlags.NOACTIVATE | 
                                                   User32.SetWindowPosFlags.SHOWWINDOW;

            User32.SetWindowPos(targetWindow.Handle, referenceWindowHandle, 0, 0, 0, 0, flags);
        }

        public static int GetGuiResources(this Process process, User32.HandleType type)
        {
            return User32.GetGuiResources(process.Handle, (int)type);
        }

        public static int GetScrollPos(this Control control, User32.ScrollOrientation orientation)
        {
            return User32.GetScrollPos(control.Handle, orientation);
        }

        public static void HideCaret(this Control control)
        {
            User32.HideCaret(control.Handle);
        }

        public static void SetForegroundWindow(this Control control)
        {
            User32.SetForegroundWindow(control.Handle);
        }

        public static void SetScrollPos(this Control control, User32.ScrollOrientation orientation, int pos)
        {
            User32.SetScrollPos(control.Handle, orientation, pos, true);
        }
    }
}
