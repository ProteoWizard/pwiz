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
            [DllImport("user32.dll")]
            public static extern bool HideCaret(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }
    }
}