using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SProCoP;

namespace SProCoP
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ArgCollector.CollectArgs(null, null, null);
        }
    }
}
