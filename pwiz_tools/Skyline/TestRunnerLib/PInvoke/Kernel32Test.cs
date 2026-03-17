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
using Microsoft.Win32.SafeHandles;

namespace TestRunnerLib.PInvoke
{
    public static class Kernel32Test
    {
        [Flags]
        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        [Flags]
        private enum EXECUTION_STATE : uint
        {
            awaymode_required = 0x00000040,
            continuous = 0x80000000,
            system_required = 0x00000001,
            display_required = 0x00000002
        }

        /// <summary>Checks whether our child process is being debugged.</summary>
        /// From https://www.codeproject.com/articles/670193/csharp-detect-if-debugger-is-attached
        /// The "remote" in CheckRemoteDebuggerPresent does not imply that the debugger
        /// necessarily resides on a different computer; instead, it indicates that the 
        /// debugger resides in a separate and parallel process.
        /// Use the IsDebuggerPresent function to detect whether the calling process 
        /// is running under the debugger.
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport("kernel32.dll")]
        // ReSharper disable once IdentifierTypo
        public static extern SafeWaitHandle CreateWaitableTimer(IntPtr lpTimerAttributes, bool bManualReset, string lpTimerName);

        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlEventHandler handler, bool add);
        public delegate bool ConsoleCtrlEventHandler(CtrlType sig);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        // ReSharper disable once IdentifierTypo
        public static extern bool SetWaitableTimer(SafeWaitHandle hTimer,
            [In] ref long pDueTime,
            int lPeriod,
            IntPtr pfnCompletionRoutine,
            IntPtr lpArgToCompletionRoutine,
            bool fResume);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        public class SystemSleep : IDisposable
        {
            private readonly EXECUTION_STATE _previousState;

            public SystemSleep(bool displayRequired)
            {
                var newState = EXECUTION_STATE.continuous |
                               EXECUTION_STATE.system_required;
                if (displayRequired)
                {
                    newState |= EXECUTION_STATE.display_required;
                }
                else
                {
                    newState |= EXECUTION_STATE.awaymode_required;
                }
                // Prevent system sleep.
                _previousState = SetThreadExecutionState(newState);
            }

            public void Dispose()
            {
                SetThreadExecutionState(_previousState);
            }
        }
    }
}
