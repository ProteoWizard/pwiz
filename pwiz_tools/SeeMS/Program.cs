//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.IO.Pipes;
using System.ComponentModel;
using System.Security.Permissions;
using System.Runtime.InteropServices;
//using EDAL;
//using BDal.CxT.Lc;

namespace seems
{
	static class Program
	{
	    [DllImport("kernel32.dll")]
	    static extern bool AttachConsole(int dwProcessId);
	    private const int ATTACH_PARENT_PROCESS = -1;

        static seemsForm MainForm;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		[SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
		public static void Main( string[] args )
		{
		    // redirect console output to parent process;
		    // must be before any calls to Console.WriteLine()
		    AttachConsole(ATTACH_PARENT_PROCESS);

            /*MSAnalysisClass a = new MSAnalysisClass();
            a.Open( @"C:\test\100 fmol BSA\0_B4\1\1SRef" );
            MSSpectrumCollection c = a.MSSpectrumCollection;
            MSSpectrum s = c[1];
            MSSpectrumParameterCollection sp = s.MSSpectrumParameterCollection;
            MSSpectrumParameter p = sp[1];*/
            /*IAnalysisFactory factory = new AnalysisFactory();
            IAnalysis a = factory.Open( @"C:\test\MM48pos_20uM_1-A,8_01_9111.d" );
            ITraceDeclaration[] tdList = a.GetTraceDeclarations();
            List<ITraceDataCollection> tdcList = new List<ITraceDataCollection>();
            foreach( ITraceDeclaration td in tdList )
                tdcList.Add( a.GetTraceDataCollection( td.TraceId ) );
            ISpectrumSourceDeclaration[] ssdList = a.GetSpectrumSourceDeclarations();
            List<ISpectrumCollection> scList = new List<ISpectrumCollection>();
            foreach( ISpectrumSourceDeclaration ssd in ssdList )
                scList.Add( a.GetSpectrumCollection( ssd.SpectrumCollectionId ) );*/

            // Add the event handler for handling UI thread exceptions to the event.
            Application.ThreadException += new ThreadExceptionEventHandler( UIThread_UnhandledException );

			// Set the unhandled exception mode to force all Windows Forms errors to go through
			// our handler.
			Application.SetUnhandledExceptionMode( UnhandledExceptionMode.CatchException );
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault( false );

			// Add the event handler for handling non-UI thread exceptions to the event. 
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler( CurrentDomain_UnhandledException );


            var singleInstanceHandler = new SingleInstanceHandler(Application.ExecutablePath) { Timeout = 200 };
            singleInstanceHandler.Launching += (sender, e) =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var singleInstanceArgs = e.Args.ToArray();

                MainForm = new seemsForm();
                MainForm.ParseArgs(singleInstanceArgs);
                if (!MainForm.IsDisposed)
                    Application.Run(MainForm);
            };

            try
            {
                singleInstanceHandler.Connect(args);
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

		private static void UIThread_UnhandledException( object sender, ThreadExceptionEventArgs e )
		{
			/*Process newSeems = new Process();
			newSeems.StartInfo.FileName = Application.ExecutablePath;
			if( MainForm.CurrentFilepath.Length > 0 )
				newSeems.StartInfo.Arguments = "\"" + MainForm.CurrentFilepath + "\" " + MainForm.CurrentScanIndex;
			newSeems.Start();
			Process.GetCurrentProcess().Kill();*/

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
			/*Process newSeems = new Process();
			newSeems.StartInfo.FileName = Application.ExecutablePath;
			if( MainForm.CurrentGraphForm.CurrentSourceFilepath.Length > 0 )
				newSeems.StartInfo.Arguments = "\"" + MainForm.CurrentGraphForm.CurrentSourceFilepath + "\" " + MainForm.CurrentGraphForm.CurrentGraphItemIndex;
			newSeems.Start();
			Process.GetCurrentProcess().Kill();*/

			MessageBox.Show( (e.ExceptionObject is Exception ? (e.ExceptionObject as Exception).Message : "Unknown error."),
							"Unhandled Exception",
							MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
							0, false );
		}
	}
}
