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
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.IO.Pipes;
using System.ComponentModel;
using System.Security.Permissions;
//using EDAL;
//using BDal.CxT.Lc;

namespace seems
{
	static class Program
	{
		static seemsForm MainForm;
        static System.Threading.Mutex ipcMutex;
        static BackgroundWorker ipcWorker;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		[SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
		public static void Main( string[] args )
		{
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

            // create machine-global mutex (to allow only one SeeMS instance)
            bool success;
            ipcMutex = new Mutex( true, "seems unique instance", out success );
            if( !success )
            {
                // send args to the existing instance
                using( NamedPipeClientStream pipeClient =
                    new NamedPipeClientStream( ".", "seems pipe", PipeDirection.Out ) )
                {
                    pipeClient.Connect();
                    using( StreamWriter sw = new StreamWriter( pipeClient ) )
                    {
                        sw.WriteLine( args.Length );
                        foreach( string arg in args )
                            sw.WriteLine( arg );
                        sw.Flush();
                    }
                    //pipeClient.WaitForPipeDrain();
                }
                return;
            }

            ipcWorker = new BackgroundWorker();
            ipcWorker.DoWork += new DoWorkEventHandler( bgWorker_DoWork );
            ipcWorker.RunWorkerAsync();

			// Add the event handler for handling UI thread exceptions to the event.
			Application.ThreadException += new ThreadExceptionEventHandler( UIThread_UnhandledException );

			// Set the unhandled exception mode to force all Windows Forms errors to go through
			// our handler.
			Application.SetUnhandledExceptionMode( UnhandledExceptionMode.CatchException );
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault( false );

			// Add the event handler for handling non-UI thread exceptions to the event. 
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler( CurrentDomain_UnhandledException );

            MainForm = new seemsForm( new string[] { } );
            MainForm.Manager.ParseArgs( args );
            if( !MainForm.IsDisposed )
			    Application.Run( MainForm );
		}

        static void bgWorker_DoWork( object sender, DoWorkEventArgs e )
        {
            while( true )
            {
                using( NamedPipeServerStream pipeServer =
                        new NamedPipeServerStream( "seems pipe", PipeDirection.In ) )
                {
                    pipeServer.WaitForConnection();

                    // Read args from client instance.
                    using( StreamReader sr = new StreamReader( pipeServer ) )
                    {
                        int length = Convert.ToInt32( sr.ReadLine() );
                        string[] args = new string[length];
                        for( int i = 0; i < length; ++i )
                            args[i] = sr.ReadLine();
                        MainForm.Manager.ParseArgs( args );
                    }

                    //pipeServer.Disconnect();
                }
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

			MessageBox.Show( "This is bad news.",
							"Unhandled Exception",
							MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
							0, false );
		}
	}
}
