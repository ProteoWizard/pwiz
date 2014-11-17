using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Ionic.Zip;
using SkylineNightly.Properties;

namespace SkylineNightly
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && string.Compare(args[0], Nightly.SCHEDULED_ARG, StringComparison.OrdinalIgnoreCase) == 0)
            {
                var nightly = new Nightly();
                nightly.Run();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SkylineNightly());
        }
    }
}
