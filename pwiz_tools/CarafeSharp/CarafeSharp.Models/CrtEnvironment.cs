/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
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

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// The Windows C runtime's copy of the environment, which native libraries such as libtorch read with getenv.
    /// .NET's <see cref="Environment"/> reads and writes the operating system's copy, which the C runtime took its own
    /// copy of when the process started and does not see again.
    /// </summary>
    /// <remarks>
    /// Bound through the API set libtorch's c10.dll imports getenv from, which Windows always resolves to the system's
    /// C runtime. Bound as ucrtbase.dll by name, it loaded the copy Skyline ships beside its executable instead, a
    /// second C runtime whose environment libtorch never reads.
    /// </remarks>
    internal static class CrtEnvironment
    {
        private const string CRT_ENVIRONMENT = @"api-ms-win-crt-environment-l1-1-0.dll";

        /// <summary>The variable as libtorch reads it, or null when it is not set.</summary>
        public static string Get(string name)
        {
            return Marshal.PtrToStringAnsi(getenv(name));
        }

        /// <summary>
        /// Sets the variable in the C runtime's copy and the operating system's, or removes it from both when
        /// <paramref name="value"/> is null or empty. False when the C runtime refuses.
        /// </summary>
        public static bool Set(string name, string value)
        {
            return _wputenv_s(name, value ?? string.Empty) == 0;
        }

        [DllImport(CRT_ENVIRONMENT, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false)]
        private static extern IntPtr getenv(string name);

        [DllImport(CRT_ENVIRONMENT, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int _wputenv_s(string name, string value);
    }
}
