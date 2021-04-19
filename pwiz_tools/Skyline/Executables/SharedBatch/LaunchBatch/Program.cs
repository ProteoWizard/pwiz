using System;
using System.Collections.Generic;
using System.Configuration;
using System.Deployment.Application;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using LaunchBatch.Properties;

namespace LaunchBatch
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var appReferencePath = args[0];

            // join remaining args (will be more than one if filepath has spaces)
            args[0] = string.Empty;
            string configFilePath = "\"" + string.Join(" ", args).Trim() + "\"";

            if (!File.Exists(appReferencePath))
            {
                MessageBox.Show(Resources.Program_Main_Failed_to_start_application_Try_launching_the_application_from_the_start_menu, Resources.Program_Main_Error);
                return;
            }

            try
            {
                // Launch the .appref-ms file.
                Process.Start(appReferencePath, configFilePath);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, Resources.Program_Main_Error);
            }
        }
    }
}
