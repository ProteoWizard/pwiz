using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Forms;

namespace PTMValidationGUI
{
    
    public class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try {
                Application.Run(new PTMDigger());
            } catch(Exception e) { MessageBox.Show(e.StackTrace + "\n" + e.ToString());}
        }
    }
}
