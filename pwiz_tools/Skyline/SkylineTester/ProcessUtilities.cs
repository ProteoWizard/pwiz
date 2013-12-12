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
