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
using System.Linq;
using System.Management;

namespace SkylineTester
{
    static class ProcessUtilities
    {
        public static void KillProcessTree(Process process)
        {

            var processWithId = new ProcessWithId(process);
            processWithId.KillAll();
        }

        private struct ProcessWithId
        {
            private Process _process;
            public int Id { get; private set; }

            public ProcessWithId(Process process) : this()
            {
                _process = process;
                try
                {
                    Id = process.Id;
                }
                catch
                {
                    Id = 0;
                }
            }

            public void KillAll()
            {
                KillChildren();
                Kill();
            }

            private void Kill()
            {
                try
                {
                    _process.Kill();
                }
                catch
                {
                    // Ignore failure
                }
            }

            private void KillChildren()
            {
                // Allow for multiple layers of git processes parented by each other
                var dictParentIdToProcessList = new Dictionary<int, List<ProcessWithId>>();
                var arrayNames = new[] {"git", "git-remote-https", "bjam", "bsdtar"};
                foreach (var process in arrayNames.SelectMany(Process.GetProcessesByName))
                {
                    try
                    {
                        var mo = new ManagementObject("win32_process.handle='" + process.Id + "'");
                        mo.Get();
                        int processParentId = Convert.ToInt32(mo["ParentProcessId"]);
                        List<ProcessWithId> processList;
                        if (!dictParentIdToProcessList.TryGetValue(processParentId, out processList))
                        {
                            processList = new List<ProcessWithId>();
                            dictParentIdToProcessList.Add(processParentId, processList);
                        }
                        processList.Add(new ProcessWithId(process));
                    }
                    catch
                    {
                        // Do nothing
                    }
                }

                KillChildren(Id, dictParentIdToProcessList);
            }

            private static void KillChildren(int parentId, Dictionary<int, List<ProcessWithId>> dictParentIdToProcessList)
            {
                List<ProcessWithId> processList;
                if (!dictParentIdToProcessList.TryGetValue(parentId, out processList))
                    return;
                foreach (var processWithId in processList)
                {
                    int id = processWithId.Id;
                    if (id == 0)
                        continue;

                    // Kill children before killing the parent
                    KillChildren(id, dictParentIdToProcessList);
                    processWithId.Kill();
                }
            }
        }
    }
}
