/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace pwiz.Skyline.Util
{
    public class ExceptionPointers
    {
        public ExceptionPointers(EXCEPTION_POINTERS ex)
        {
            ExceptionRecord = Marshal.PtrToStructure<EXCEPTION_RECORD>(ex.ExceptionRecord);
            ContextRecord = Marshal.PtrToStructure<CONTEXT_RECORD>(ex.ContextRecord);
        }

        public static ExceptionPointers Current
        {
            get
            {
                var exPtrs = Marshal.GetExceptionPointers();
                if (exPtrs == IntPtr.Zero)
                    return null;

                return new ExceptionPointers(Marshal.PtrToStructure<EXCEPTION_POINTERS>(exPtrs));
            }
        }

        private const string NAME_VALUE_FORMAT = @"{0} = {1:X8}";
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format(NAME_VALUE_FORMAT, nameof(ExceptionRecord.ExceptionCode),
                ExceptionRecord.ExceptionCode));
            sb.AppendLine(string.Format(NAME_VALUE_FORMAT, nameof(ExceptionRecord.ExceptionFlags),
                ExceptionRecord.ExceptionFlags));
            // ReSharper disable FormatStringProblem
            sb.AppendLine(string.Format(NAME_VALUE_FORMAT, nameof(ExceptionRecord.ExceptionAddress),
                ExceptionRecord.ExceptionAddress.ToString(@"X16")));
            sb.AppendLine(string.Format(NAME_VALUE_FORMAT, nameof(ContextRecord.Rip),
                ContextRecord.Rip.ToString(@"X16")));

            for (var i = 0; i < ExceptionRecord.NumberParameters; ++i)
            {
                // Interesting observation I've made: For managed exceptions ExceptionRecord.ExceptionInformation[4] contains the base address of clr.dll
                // Not sure what ExceptionRecord.ExceptionInformation[0] is, but could be useful. Maybe it can be used to obtain a managed exception object
                sb.AppendLine(string.Format(NAME_VALUE_FORMAT,
                    nameof(ExceptionRecord.NumberParameters) + string.Format(@"[{0}]", i),
                    ExceptionRecord.ExceptionInformation[i].ToString(@"X16")));
            }

            // ReSharper restore FormatStringProblem

            return sb.ToString();
        }

        public static string GetModuleList()
        {
            var sb = new StringBuilder();
            using (var proc = Process.GetCurrentProcess())
            {
                foreach (ProcessModule mod in proc.Modules)
                {
                    sb.AppendLine(string.Format(@"{0}: {1} - {2}", mod.ModuleName, mod.BaseAddress.ToString(@"X16"),
                        (mod.BaseAddress + mod.ModuleMemorySize).ToString(@"X16")));
                }
            }

            return sb.ToString();
        }

        public EXCEPTION_RECORD ExceptionRecord { get; private set; }
        public CONTEXT_RECORD ContextRecord { get; private set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EXCEPTION_POINTERS
    {
        public IntPtr ExceptionRecord;
        public IntPtr ContextRecord;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct EXCEPTION_RECORD
    {
        public UInt32 ExceptionCode; // For CLR exceptions this seems to encode some prefix and a 3 character string
        public UInt32 ExceptionFlags;
        public IntPtr ExceptionRecord;
        public IntPtr ExceptionAddress;
        public UInt32 NumberParameters;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
        public IntPtr[] ExceptionInformation;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CONTEXT_RECORD
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 248)]
        public char[] Pad;
        // We really only care about Rip, the other registers won't be very helpful
        public IntPtr Rip;
    }
}
