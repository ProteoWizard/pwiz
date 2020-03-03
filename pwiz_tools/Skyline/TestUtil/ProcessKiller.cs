﻿/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

namespace pwiz.SkylineTestUtil
{
    public class ProcessKiller : IDisposable
    {
        private readonly string _processName;

        public ProcessKiller(string processName)
        {
            _processName = processName;
            KillNamedProcess();
        }

        public void Dispose()
        {
            KillNamedProcess();
        }

        private void KillNamedProcess()
        {
            var processList = Process.GetProcessesByName(_processName);
            foreach (var process in processList)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                    // Could fail for a number of reasons, including process is already shutting down
                }
            }
        }
    }
}