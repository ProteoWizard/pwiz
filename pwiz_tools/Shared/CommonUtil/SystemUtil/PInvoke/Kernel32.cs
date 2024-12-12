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
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace pwiz.Common.SystemUtil.PInvoke
{
    public static class Kernel32
    {
        [Flags]
        // ReSharper disable InconsistentNaming IdentifierTypo
        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
        // ReSharper restore InconsistentNaming IdentifierTypo

        public static void AttachConsoleToParentProcess()
        {
            const int parentProcessId = -1;

            AttachConsole(parentProcessId);
        }

        [DllImport(nameof(Kernel32), CharSet = CharSet.Unicode)]
        public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        /// <summary>Checks whether our child process is being debugged.</summary>
        /// From https://www.codeproject.com/articles/670193/csharp-detect-if-debugger-is-attached
        /// The "remote" in CheckRemoteDebuggerPresent does not imply that the debugger
        /// necessarily resides on a different computer; instead, it indicates that the 
        /// debugger resides in a separate and parallel process.
        /// Use the IsDebuggerPresent function to detect whether the calling process 
        /// is running under the debugger.
        [DllImport(nameof(Kernel32), SetLastError = true, ExactSpelling = true)]
        public static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport(nameof(Kernel32))]
        // ReSharper disable once IdentifierTypo
        public static extern SafeWaitHandle CreateWaitableTimer(IntPtr lpTimerAttributes,
            bool bManualReset,
            string lpTimerName);

        [DllImport(nameof(Kernel32))]
        public static extern int GetCurrentThreadId();

        [DllImport(nameof(Kernel32), CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);

        [DllImport(nameof(Kernel32), CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport(nameof(Kernel32), SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetTempFileName(string lpPathName, string lpPrefixString,
            uint uUnique, [Out] StringBuilder lpTempFileName);

        [DllImport(nameof(Kernel32))]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlEventHandler handler, bool add);
        public delegate bool ConsoleCtrlEventHandler(CtrlType sig);

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

        public class SystemSleep : IDisposable
        {
            private readonly EXECUTION_STATE _previousState;

            public SystemSleep()
            {
                // Prevent system sleep.
                _previousState = SetThreadExecutionState(
                    EXECUTION_STATE.awaymode_required |
                    EXECUTION_STATE.continuous |
                    EXECUTION_STATE.system_required);
            }

            public void Dispose()
            {
                SetThreadExecutionState(_previousState);
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

            [Flags]
            // ReSharper disable InconsistentNaming
            private enum EXECUTION_STATE : uint
            {
                // ReSharper disable once IdentifierTypo
                awaymode_required = 0x00000040,
                continuous = 0x80000000,
                system_required = 0x00000001
            }
            // ReSharper restore InconsistentNaming
        }

    }
}