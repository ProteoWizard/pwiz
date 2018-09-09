/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace SkylineTester
{
    static class ProcessUtilities
    {
        public static void KillProcessTree(Process process)
        {
            int id;
            try
            {
                id = process.Id;
                process.Kill();
            }
            catch (Exception)
            {
                // ReSharper disable once RedundantJumpStatement
                return;
            }

            KillChild(id, "git");
            KillChild(id, "bjam");
            KillChild(id, "bsdtar");
        }

        private struct ParentedProcess
        {
            public Process Process;
            public int ParentId;
        }
        private static void KillChild(int parentId, string processName)
        {
            // Allow for multiple layers of git processes parented by each other
            var dictIdToParentedProcess = new Dictionary<int, ParentedProcess>();
            dictIdToParentedProcess.Add(parentId, new ParentedProcess {ParentId = 0, Process = null});
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    var mo = new ManagementObject("win32_process.handle='" + process.Id + "'");
                    mo.Get();
                    int processParentId = Convert.ToInt32(mo["ParentProcessId"]);
                    dictIdToParentedProcess.Add(process.Id, new ParentedProcess {ParentId = processParentId, Process = process});
                }
                catch
                {
                    // Do nothing
                }
            }

            foreach (var pp in dictIdToParentedProcess.Values)
            {
                if (dictIdToParentedProcess.ContainsKey(pp.ParentId) && pp.Process != null)
                {
                    try
                    {
                        pp.Process.Kill();
                    }
                    catch
                    {
                        // Do nothing
                    }
                }
            }
        }
    }
}
