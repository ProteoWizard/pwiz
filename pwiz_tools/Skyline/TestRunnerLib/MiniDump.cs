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
using System.IO;
using System.Runtime.InteropServices;

namespace TestRunnerLib
{
    public class MiniDump
    {
        [Flags]
        public enum MINIDUMP_TYPE
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            MiniDumpWithHandleData = 0x00000004,
            MiniDumpFilterMemory = 0x00000008,
            MiniDumpScanMemory = 0x00000010,
            MiniDumpWithUnloadedModules = 0x00000020,
            MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
            MiniDumpFilterModulePaths = 0x00000080,
            MiniDumpWithProcessThreadData = 0x00000100,
            MiniDumpWithPrivateReadWriteMemory = 0x00000200,
            MiniDumpWithoutOptionalData = 0x00000400,
            MiniDumpWithFullMemoryInfo = 0x00000800,
            MiniDumpWithThreadInfo = 0x00001000,
            MiniDumpWithCodeSegs = 0x00002000,
            MiniDumpWithoutAuxiliaryState = 0x00004000,
            MiniDumpWithFullAuxiliaryState = 0x00008000,
            MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
            MiniDumpIgnoreInaccessibleMemory = 0x00020000,
            MiniDumpWithTokenInformation = 0x00040000,
            MiniDumpWithModuleHeaders = 0x00080000,
            MiniDumpFilterTriage = 0x00100000,
            MiniDumpWithAvxXStateContext = 0x00200000,
            MiniDumpWithIptTrace = 0x00400000,
            MiniDumpValidTypeFlags = 0x007fffff
        };

        [DllImport(@"Dbghelp.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, IntPtr hFile, MINIDUMP_TYPE DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);

        public static bool WriteMiniDump(string dumpFilePath)
        {
            using (var process = Process.GetCurrentProcess())
            {
                using (var file = new FileStream(dumpFilePath, FileMode.Create))
                {
                    if (file.SafeFileHandle == null)
                        return false;

                    // Task manager uses: MiniDumpWithFullMemory | MiniDumpWithHandleData | MiniDumpWithUnloadedModules | MiniDumpWithFullMemoryInfo | MiniDumpWithThreadInfo | MiniDumpIgnoreInaccessibleMemory | MiniDumpWithIptTrace
                    const MINIDUMP_TYPE flags = MINIDUMP_TYPE.MiniDumpWithDataSegs |
                                                MINIDUMP_TYPE.MiniDumpWithFullMemory |
                                                MINIDUMP_TYPE.MiniDumpWithHandleData |
                                                MINIDUMP_TYPE.MiniDumpScanMemory |
                                                MINIDUMP_TYPE.MiniDumpWithUnloadedModules |
                                                MINIDUMP_TYPE.MiniDumpWithIndirectlyReferencedMemory |
                                                MINIDUMP_TYPE.MiniDumpWithProcessThreadData |
                                                MINIDUMP_TYPE.MiniDumpWithPrivateReadWriteMemory |
                                                MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo |
                                                MINIDUMP_TYPE.MiniDumpWithThreadInfo |
                                                MINIDUMP_TYPE.MiniDumpWithCodeSegs |
                                                MINIDUMP_TYPE.MiniDumpWithFullAuxiliaryState |
                                                MINIDUMP_TYPE.MiniDumpWithPrivateWriteCopyMemory |
                                                MINIDUMP_TYPE.MiniDumpIgnoreInaccessibleMemory |
                                                MINIDUMP_TYPE.MiniDumpWithTokenInformation |
                                                MINIDUMP_TYPE.MiniDumpWithModuleHeaders |
                                                MINIDUMP_TYPE.MiniDumpWithAvxXStateContext |
                                                MINIDUMP_TYPE.MiniDumpWithIptTrace;


                    
                    return MiniDumpWriteDump(process.Handle, (uint)process.Id, file.SafeFileHandle.DangerousGetHandle(), flags,
                        IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }
    }
}
