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
using System.Threading;

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
                    _process.CloseMainWindow();

                    // Wait for exit on a new thread to avoid deadlocking
                    var exitWaitThread = new Thread(WaitForProcessExit);
                    exitWaitThread.Start();
                }
                catch
                {
                    // Ignore failure
                }
            }

            private void WaitForProcessExit()
            {
                // It would seem that we could use Process.WaitForExit(millis) but that
                // has been seen to wait indefinitely under a debugger of a deadlock.
                // So, just to be safe, a busy wait is used here.
                for (int i = 0; i < 5; i++)
                {
                    if (_process.HasExited)
                        return;
                    Thread.Sleep(100);
                }
                _process.Kill();
            }

            private void KillChildren(int? pid = null, List<Process> childProcesses = null)
            {
                bool isRoot = pid == null;
                pid ??= Process.GetCurrentProcess().Id;
                childProcesses ??= new List<Process>();

                var searcher = new ManagementObjectSearcher
                    ("Select * From Win32_Process Where ParentProcessID=" + pid.Value);
                ManagementObjectCollection moc = searcher.Get();
                foreach (ManagementObject mo in moc)
                {
                    try
                    {
                        KillChildren(Convert.ToInt32(mo["ProcessID"]), childProcesses);
                    }
                    catch
                    {
                        // Do nothing
                    }
                }

                if (isRoot)
                {
                    foreach(var child in childProcesses)
                        try
                        {
                            child.Kill();
                        }
                        catch
                        {
                            // Do nothing
                        }
                }
                else
                {
                    try
                    {
                        childProcesses.Add(Process.GetProcessById(pid.Value));
                    }
                    catch (ArgumentException)
                    {
                        // Process already exited.
                    }
                }
            }
        }
    }
}
