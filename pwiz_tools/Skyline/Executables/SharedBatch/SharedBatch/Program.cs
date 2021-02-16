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
using SharedBatch.Properties;

namespace SharedBatch
{
    public static class Program
    {


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
    }
}
