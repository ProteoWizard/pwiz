using System;
using System.Configuration;
using System.Deployment.Application;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository.Hierarchy;
using SharedAutoQcBatch.Properties;

namespace SharedAutoQcBatch
{
    public static class Program
    {
        public static string LOG_NAME;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }

        public static string AppName
        {
            get
            {
                var appNameAndVersion = Process.GetCurrentProcess().MainWindowTitle;
                return appNameAndVersion.Substring(0, appNameAndVersion.LastIndexOf(' '));
            }
        }

    }
}
