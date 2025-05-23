﻿/*
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
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;
using SkylineTester.Properties;

namespace SkylineTester
{
    static class Program
    {
        public static bool IsRunning { get; private set; }
        public static bool UserKilledTestRun { get; set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // The current recommendation from MSFT for future-proofing HTTPS https://docs.microsoft.com/en-us/dotnet/framework/network-programming/tls
            // is don't specify TLS levels at all, let the OS decide. But we worry that this will mess up Win7 and Win8 installs, so we continue to specify explicitly
            try
            {
                var Tls13 = (SecurityProtocolType)12288; // From decompiled SecurityProtocolType - compiler has no definition for some reason
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | Tls13;
            }
            catch (NotSupportedException)
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12; // Probably an older Windows Server
            }

            if (Settings.Default.SettingsUpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.SettingsUpgradeRequired = false;
                Settings.Default.Save();
            }

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
                ExitWithStatusCodeForSkylineNightly();
            }

            if (args.Length == 1 && args[0].EndsWith(".zip"))
            {
                try
                {
                    Kernel32.AttachConsoleToParentProcess();
                    CreateZipInstallerWindow.CreateZipFile(args[0], args[0].ToLower().EndsWith("withtestdata.zip"));
                    Thread.Sleep(2000);
                }
                catch (Exception e)
                {
                    Console.WriteLine("FAILURE: Installer zip file \"{0}\" not created:", args[0]);
                    Console.WriteLine(e);
                    Environment.Exit(2);
                }
                ExitWithStatusCodeForSkylineNightly();
            }

            IsRunning = true;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SkylineTesterWindow(args));
            ExitWithStatusCodeForSkylineNightly();
        }

        private static void ExitWithStatusCodeForSkylineNightly()
        {
            Environment.Exit(UserKilledTestRun ? 0xDEAD : 0);
        }
    }
}
