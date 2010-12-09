//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;

namespace IDPicker
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main (string[] args)
        {

            // Add the event handler for handling UI thread exceptions to the event.
            Application.ThreadException += new ThreadExceptionEventHandler(UIThread_UnhandledException);

            // Set the unhandled exception mode to force all Windows Forms errors to go through
            // our handler.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            // Add the event handler for handling non-UI thread exceptions to the event. 
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            Application.Run(new IDPickerForm(args));
        }

        private static void UIThread_UnhandledException (object sender, ThreadExceptionEventArgs e)
        {
            /*Process newSeems = new Process();
            newSeems.StartInfo.FileName = Application.ExecutablePath;
            if( MainForm.CurrentFilepath.Length > 0 )
                newSeems.StartInfo.Arguments = "\"" + MainForm.CurrentFilepath + "\" " + MainForm.CurrentScanIndex;
            newSeems.Start();
            Process.GetCurrentProcess().Kill();*/

            string message = e.Exception.ToString();
            if (e.Exception.InnerException != null)
                message += "\n\nAdditional information: " + e.Exception.InnerException.ToString();
            MessageBox.Show(message,
                            "Unhandled Exception",
                            MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                            0, false);
        }

        private static void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e)
        {
            /*Process newSeems = new Process();
            newSeems.StartInfo.FileName = Application.ExecutablePath;
            if( MainForm.CurrentGraphForm.CurrentSourceFilepath.Length > 0 )
                newSeems.StartInfo.Arguments = "\"" + MainForm.CurrentGraphForm.CurrentSourceFilepath + "\" " + MainForm.CurrentGraphForm.CurrentGraphItemIndex;
            newSeems.Start();
            Process.GetCurrentProcess().Kill();*/

            MessageBox.Show((e.ExceptionObject is Exception ? (e.ExceptionObject as Exception).Message : "Unknown error."),
                            "Unhandled Exception",
                            MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                            0, false);
        }
    }
}
