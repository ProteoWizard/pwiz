using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedAutoQcBatch
{
    public static class Program
    {

        //public static string AppName { get; private set; }

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
