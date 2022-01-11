using System;
using System.Diagnostics;
using System.IO;
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
            string configFilePath = args[1];

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
