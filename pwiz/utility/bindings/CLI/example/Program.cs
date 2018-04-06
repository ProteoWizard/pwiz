//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2015 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Drawing;
using System.Security.Permissions;
using pwiz.CLI.msdata;

namespace pwizCLI
{
	static class Program
	{
	    private static TextBox _fileTextBox;
	    private static Button _openButton;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		[SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
		public static void Main( string[] args )
		{
			// Add the event handler for handling UI thread exceptions to the event.
			Application.ThreadException += UIThread_UnhandledException;

			// Set the unhandled exception mode to force all Windows Forms errors to go through
			// our handler.
			Application.SetUnhandledExceptionMode( UnhandledExceptionMode.CatchException );
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault( false );

			// Add the event handler for handling non-UI thread exceptions to the event. 
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                var main = new Form
                {
                    Text = "ProteoWizard CLI bindings example project: choose an MSData file to open",
                    ClientSize = new Size(640, 32)
                };

                _fileTextBox = new TextBox
                {
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                    AutoCompleteSource = AutoCompleteSource.FileSystem,
                    Location = new Point(6, 6),
                    Width = 500
                };

                _openButton = new Button
                {
                    Text = "Open MSData File",
                    Width = 120,
                    Anchor = AnchorStyles.Right,
                    Location = new Point(510, 6)
                };

                main.Controls.Add(_fileTextBox);
                main.Controls.Add(_openButton);
                _openButton.Click += openButton_Click;

                Application.Run(main);
            }
            catch (Exception e)
            {
                string message = e.ToString();
                if (e.InnerException != null)
                    message += "\n\nAdditional information: " + e.InnerException.ToString();
                MessageBox.Show(message,
                                "Unhandled Exception",
                                MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                                0, false);
            }
		}

        static void openButton_Click(object sender, EventArgs e)
        {
            string filepath = _fileTextBox.Text;
            if (!File.Exists(filepath))
            {
                MessageBox.Show("That filepath does not exist.", "404 File not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var msd = new MSDataFile(filepath);
                var sl = msd.run.spectrumList;
                MessageBox.Show(String.Format("The file has {0} spectra.", sl.size()), "Spectra Count");
            }
            catch (Exception ex)
            {
                string message = ex.ToString();
                if (ex.InnerException != null)
                    message += "\n\nAdditional information: " + ex.InnerException.ToString();
                MessageBox.Show(message,
                                "Error opening file",
                                MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                                0, false);
            }

        }

        #region Exception handling
        private static void UIThread_UnhandledException( object sender, ThreadExceptionEventArgs e )
		{
			string message = e.Exception.ToString();
			if( e.Exception.InnerException != null )
                message += "\n\nAdditional information: " + e.Exception.InnerException.ToString();
			MessageBox.Show( message,
							"Unhandled Exception",
							MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
							0, false );
		}

		private static void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e )
		{
			MessageBox.Show( (e.ExceptionObject is Exception ? (e.ExceptionObject as Exception).Message : "Unknown error."),
							"Unhandled Exception",
							MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
							0, false );
        }
        #endregion
    }
}
