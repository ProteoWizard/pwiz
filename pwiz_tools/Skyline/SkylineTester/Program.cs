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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SkylineTester
{
    static class Program
    {
        public static bool IsRunning { get; private set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // The SkylineTester installation puts SkylineTester one directory too high.
            var nestedDirectory = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                SkylineTesterWindow.SkylineTesterFiles);
            var nestedSkylineTester = Path.Combine(nestedDirectory, "SkylineTester.exe");
            if (File.Exists(nestedSkylineTester))
            {
                var restartSkylineTester = new Process
                {
                    StartInfo =
                    {
                        FileName = nestedSkylineTester, 
                        Arguments = args.Length > 0 ? args[0].Quote() : "",
                        WorkingDirectory = nestedDirectory
                    }
                };
                restartSkylineTester.Start();
                return;
            }

            if (args.Length == 1 && args[0].EndsWith(".zip"))
            {
                AllocConsole();
                CreateZipInstallerWindow.CreateZipFile(args[0]);
                Thread.Sleep(2000);
                return;
            }

            IsRunning = true;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SkylineTesterWindow(args));
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

    }
}
