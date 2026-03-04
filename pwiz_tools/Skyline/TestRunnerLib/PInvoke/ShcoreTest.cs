/*
 * Copyright 2026 University of Washington - Seattle, WA
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

namespace TestRunnerLib.PInvoke
{
    public static class ShcoreTest
    {
        [DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        /// <summary>
        /// Returns the DPI scale factor for the monitor at the given point (1.0 = 100%, 1.5 = 150%, etc.).
        /// Falls back to NaN if the API call fails.
        /// </summary>
        public static double GetScaleFactor(int x, int y)
        {
            try
            {
                var pt = new User32Test.POINT { x = x, y = y };
                var hMonitor = User32Test.MonitorFromPoint(pt, 0);
                GetDpiForMonitor(hMonitor, 0, out uint dpiX, out _);
                return dpiX / 96.0;
            }
            catch
            {
                return double.NaN;
            }
        }
    }
}
