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
        private class ProcessParent
        {
            public Process Process;
            public int ParentId;
        }

        public static void KillProcessTree(Process process)
        {
            var parentMap = new List<ProcessParent>();
            var allProcesses = Process.GetProcesses();
            foreach (var p in allProcesses)
            {
                try
                {
                    var mo = new ManagementObject("win32_process.handle='" + p.Id + "'");
                    mo.Get();
                    var parentId = Convert.ToInt32(mo["ParentProcessId"]);
                    parentMap.Add(new ProcessParent {Process = p, ParentId = parentId});
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                }
            }

            KillProcessTree(process, parentMap);
        }

        private static void KillProcessTree(Process process, IList<ProcessParent> parentMap)
        {
            var id = process.Id;

            try
            {
                process.Kill();
            }
            catch (Exception)
            {
                return;
            }

            foreach (var processParent in parentMap.Where(p => p.ParentId == id))
            {
                KillProcessTree(processParent.Process, parentMap);
            }
        }
    }
}
