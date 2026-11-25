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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Formats process exit codes, naming the Windows NTSTATUS codes a crash leaves.
    /// Kept apart from PInvoke.Kernel32, which only Windows can call.
    /// </summary>
    public static class ProcessExitCode
    {
        /// <summary>
        /// Common NTSTATUS codes returned as process exit codes.
        /// Values can be compared directly to process exit codes cast to this enum.
        /// </summary>
        public enum NtStatus : uint
        {
            STATUS_BREAKPOINT = 0x80000003,
            STATUS_ACCESS_VIOLATION = 0xC0000005,
            STATUS_INVALID_HANDLE = 0xC0000008,
            STATUS_NO_MEMORY = 0xC0000017,
            STATUS_ILLEGAL_INSTRUCTION = 0xC000001D,
            STATUS_DLL_NOT_FOUND = 0xC0000135,
            STATUS_CONTROL_C_EXIT = 0xC000013A,
            STATUS_DLL_INIT_FAILED = 0xC0000142,
            STATUS_POSSIBLE_DEADLOCK = 0xC0000194,
            STATUS_HEAP_CORRUPTION = 0xC0000374,
            STATUS_STACK_BUFFER_OVERRUN = 0xC0000409,
            STATUS_STACK_OVERFLOW = 0xC00000FD,
            STATUS_DELAY_LOAD_FAILED = 0xC06D007E,
            STATUS_MODULE_NOT_FOUND = 0xC06D007F,
        }

        /// <summary>
        /// Formats a process exit code for human-readable display.
        /// Shows decimal, hex, and a name for common NTSTATUS codes.
        /// </summary>
        /// <param name="exitCode">The process exit code</param>
        /// <returns>A formatted string like "-1073741819 (0xC0000005 STATUS_ACCESS_VIOLATION)"</returns>
        public static string Format(int exitCode)
        {
            uint unsigned = unchecked((uint)exitCode);
            string hex = @"0x" + unsigned.ToString(@"X8");
            string name = Enum.GetName(typeof(NtStatus), unsigned);
            return name != null ? $@"{exitCode} ({hex} {name})" : $@"{exitCode} ({hex})";
        }
    }
}
