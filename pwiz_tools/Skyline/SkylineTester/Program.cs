using System;
using System.Diagnostics;
using System.IO;
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
            const string NestedSkylineTester = "SkylineTester Files\\SkylineTester.exe";
            if (File.Exists(NestedSkylineTester))
            {
                var restartSkylineTester = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = NestedSkylineTester,
                        Arguments = "\"" + Path.Combine(Environment.CurrentDirectory, "SkylineTester Results") + "\"",
                        WorkingDirectory = Path.GetFullPath("SkylineTester Files")
                    }
                };
                restartSkylineTester.Start();
                return;
            }

            IsRunning = true;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SkylineTesterWindow(args));
        }
    }
}
