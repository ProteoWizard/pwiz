﻿/*
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
using System.Runtime.InteropServices;

namespace TestRunnerLib.PInvoke
{
    public static class Gdi32Test
    {
        // ReSharper disable once InconsistentNaming
        public enum GDCFlags : uint
        {
            // ReSharper disable InconsistentNaming IdentifierTypo
            VERTRES = 10,
            DESKTOPVERTRES = 117
            // ReSharper restore InconsistentNaming IdentifierTypo
        }

        [DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(IntPtr hdc, GDCFlags flag);
    }
}
