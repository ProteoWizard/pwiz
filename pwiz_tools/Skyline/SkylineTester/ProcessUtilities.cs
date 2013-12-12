using System;
using System.Diagnostics;
using System.Management;

namespace SkylineTester
{
    static class ProcessUtilities
    {
        public static void KillProcessTree(Process process)
        {
            var id = process.Id;

            try
            {
                process.Kill();
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
                return;
            }

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var mo = new ManagementObject("win32_process.handle='" + p.Id + "'");
                    mo.Get();
                    var parentId = Convert.ToInt32(mo["ParentProcessId"]);
                    if (parentId == id)
                        KillProcessTree(p);
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                }
            }
        }
    }
}
