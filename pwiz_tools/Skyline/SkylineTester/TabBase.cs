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

namespace SkylineTester
{
    public abstract class TabBase
    {
        public static SkylineTesterWindow MainWindow;

        public virtual void Open()
        {
        }

        public virtual bool Run()
        {
            return true;
        }

        public virtual void Cancel()
        {
            MainWindow.CommandShell.Stop();
        }

        public virtual bool Stop(bool success)
        {
            return true;
        }

        public static void StartLog(string runName, string logFile = null, bool switchToOutputTab = false)
        {
            MainWindow.ClearLog();
            MainWindow.LastRunName = runName;
            MainWindow.CommandShell.LogFile = logFile ?? MainWindow.DefaultLogFile;
            MainWindow.CommandShell.AddImmediate("\n# {0} started {1}".With(runName, DateTime.Now.ToString("f")));
            MainWindow.RefreshLogs();
            if (switchToOutputTab)
                MainWindow.ShowOutput();
        }

        public static void RunUI(Action action)
        {
            MainWindow.Invoke(action);
        }
    }
}
