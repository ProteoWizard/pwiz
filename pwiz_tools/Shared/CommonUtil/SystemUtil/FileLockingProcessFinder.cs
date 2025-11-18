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
 
  The work in this file is based on code found at
  https://itecnote.com/tecnote/c-how-to-find-out-which-process-is-locking-a-file-using-net/
 
 */

//
// Implements FileLockingProcessFinder.GetProcessesUsingFile(<full-path-to-file>)
// Useful for debugging file locking problems
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using pwiz.Common.CommonResources;

namespace pwiz.Common.SystemUtil
{
    public static class FileLockingProcessFinder
    {
        [StructLayout(LayoutKind.Sequential)]
        struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        const int RmRebootReasonNone = 0;
        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;

        enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;

            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmRegisterResources(uint pSessionHandle,
            UInt32 nFiles,
            string[] rgsFilenames,
            UInt32 nApplications,
            [In] RM_UNIQUE_PROCESS[] rgApplications,
            UInt32 nServices,
            string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
        static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll")]
        static extern int RmGetList(uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
            ref uint lpdwRebootReasons);

        /// <summary>
        /// Find out what process(es) have a lock on the specified file.
        /// </summary>
        /// <param name="fullPathToFile">Path of the file.</param>
        /// <returns>Processes locking the file</returns>
        /// <remarks>See also:
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa373661(v=vs.85).aspx
        /// http://wyupdate.googlecode.com/svn-history/r401/trunk/frmFilesInUse.cs (no copyright in code at time of viewing)
        /// 
        /// </remarks>
        public static List<Process> GetProcessesUsingFile(string fullPathToFile)
        {
            uint handle;
            string key = Guid.NewGuid().ToString();
            List<Process> processes = new List<Process>();

            int res = RmStartSession(out handle, 0, key);
            if (res != 0)
                throw new Exception(@"Could not begin restart session.  Unable to determine file locker.");

            try
            {
                const int ERROR_MORE_DATA = 234;
                uint pnProcInfoNeeded = 0,
                    pnProcInfo = 0,
                    lpdwRebootReasons = RmRebootReasonNone;

                string[] resources = new[] { fullPathToFile }; // Just checking on one resource.

                res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);

                if (res != 0)
                    throw new Exception(@"Could not register resource.");

                //Note: there's a race condition here -- the first call to RmGetList() returns
                //      the total number of process. However, when we call RmGetList() again to get
                //      the actual processes this number may have increased.
                res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

                if (res == ERROR_MORE_DATA)
                {
                    // Create an array to store the process results
                    RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    // Get the list
                    res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);
                    if (res == 0)
                    {
                        processes = new List<Process>((int)pnProcInfo);

                        // Enumerate all of the results and add them to the 
                        // list to be returned
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                processes.Add(Process.GetProcessById(processInfo[i].Process.dwProcessId));
                            }
                            // catch the error -- in case the process is no longer running
                            catch (ArgumentException)
                            {
                            }
                        }
                    }
                    else
                        throw new Exception(@"Could not list processes locking resource.");
                }
                else if (res != 0)
                    throw new Exception(@"Could not list processes locking resource. Failed to get size of result.");
            }
            finally
            {
                RmEndSession(handle);
            }

            return processes;
        }

        public static void DeleteDirectoryWithFileLockingDetails(string dirPath)
        {
            const int maxRetryCount = 4;
            const int delayMilliseconds = 500;

            int retry = 0;
            for (; ; )
            {
                try
                {
                    Directory.Delete(dirPath, true);
                    return; // Success
                }
                catch (Exception x)
                {
                    if (retry++ < maxRetryCount)
                    {
                        Thread.Sleep(delayMilliseconds);
                        continue;   // Keep trying
                    }

                    // Try to get locking information and throw a new exception with more info
                    var lockingException = ToFileLockingException(x, dirPath);
                    if (!ReferenceEquals(x, lockingException))
                        throw lockingException;

                    // But just throw this exception without altering it if that fails
                    throw;
                }
            }
        }

        private const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);

        public static Exception ToFileLockingException(Exception x, string dirPath)
        {
            // If it's a file locking issue, wrap the exception to report the locking process
            if (x is IOException { HResult: ERROR_SHARING_VIOLATION } ioException)
            {
                var match = Regex.Match(ioException.Message, "'([^']+)'");
                if (match.Success)
                {
                    string lockedFileName = match.Captures[0].Value.Trim('\'');
                    var isDirectory = Directory.Exists(lockedFileName);
                    string[] lockedFilePaths = isDirectory ? 
                        Array.Empty<string>() : // It's a locked directory, not a locked file
                        Directory.GetFiles(dirPath, lockedFileName, SearchOption.AllDirectories);
                    if (lockedFilePaths.Length == 0 && !isDirectory)
                    {
                        return new IOException(
                            string.Format(MessageResources.FileLockingProcessFinder_ToFileLockingException_The_file___0___was_locked_but_has_since_been_deleted_from___1__, lockedFileName, dirPath), x);
                    }
                    else
                    {
                        string lockedFilePath = isDirectory ? lockedFileName : lockedFilePaths[0];
                        int currentProcessId = Process.GetCurrentProcess().Id;
                        Func<int, string> pidOrThisProcess = pid =>
                            pid == currentProcessId ? MessageResources.FileLockingProcessFinder_ToFileLockingException_this_process : $@"PID: {pid}";
                        var processesLockingFile = GetProcessesUsingFile(lockedFilePath);
                        var names = string.Join(@", ",
                            processesLockingFile.Select(p => $@"{p.ProcessName} ({pidOrThisProcess(p.Id)})"));
                        return new IOException(string.Format(MessageResources.FileLockingProcessFinder_ToFileLockingException_The_file___0___is_locked_by___1_, lockedFilePath, names), x);
                    }
                }
            }

            return x;
        }
    }
}
