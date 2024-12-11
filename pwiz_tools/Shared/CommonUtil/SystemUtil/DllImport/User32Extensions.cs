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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using ProgressBar = System.Windows.Forms.ProgressBar;

namespace pwiz.Common.SystemUtil.DllImport
{
    public static class User32Extensions
    {
        public static void BringWindowToSameLevelWithoutActivating(this Form targetWindow, IntPtr referenceWindowHandle)
        {
            // Use SetWindowPos to adjust z-order without activating
            targetWindow.SetWindowPos(referenceWindowHandle, 0, 0, 0, 0,
                User32.SWP.NOMOVE,
                User32.SWP.NOSIZE,
                User32.SWP.NOACTIVATE,
                User32.SWP.SHOWWINDOW);
        }

        public static void HideCaret(this Control control)
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
