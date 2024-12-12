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
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace pwiz.Common.SystemUtil.PInvoke
{
    public static class Kernel32
    {
        public static void AttachConsoleToParentProcess()
        {
            const int parentProcessId = -1;

            AttachConsole(parentProcessId);
        }

        [DllImport(nameof(Kernel32))]
        public static extern int GetCurrentThreadId();

        [DllImport(nameof(Kernel32))]
        // ReSharper disable once IdentifierTypo
        public static extern SafeWaitHandle CreateWaitableTimer(IntPtr lpTimerAttributes,
            bool bManualReset,
            string lpTimerName);

        [DllImport(nameof(Kernel32), SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        // ReSharper disable once IdentifierTypo
        public static extern bool SetWaitableTimer(SafeWaitHandle hTimer,
            [In] ref long pDueTime,
            int lPeriod,
            IntPtr pfnCompletionRoutine,
            IntPtr lpArgToCompletionRoutine,
            bool fResume);

        [DllImport(nameof(Kernel32), SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

    }
}