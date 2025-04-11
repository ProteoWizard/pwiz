/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Common.SystemUtil
{
    public static class ProcessEx 
    {
        /// <summary>
        /// Returns true iff the process is running under Wine (the "wine_get_version" function is exported by ntdll.dll)
        /// </summary>
        public static bool IsRunningOnWine => Kernel32.GetProcAddress(Kernel32.GetModuleHandle(@"ntdll.dll"), @"wine_get_version") != IntPtr.Zero;
    }
}
