using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Security.Permissions;

namespace seems
{
	static class Program
	{
		static seems MainForm;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		[SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
		public static void Main( string[] args )
		{
			// Add the event handler for handling UI thread exceptions to the event.
			Application.ThreadException += new ThreadExceptionEventHandler( UIThread_UnhandledException );

			// Set the unhandled exception mode to force all Windows Forms errors to go through
			// our handler.
			Application.SetUnhandledExceptionMode( UnhandledExceptionMode.CatchException );
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault( false );

			// Add the event handler for handling non-UI thread exceptions to the event. 
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler( CurrentDomain_UnhandledException );

			MainForm = new seems( args );
			Application.Run( MainForm );
		}

		private static void UIThread_UnhandledException( object sender, ThreadExceptionEventArgs e )
		{
			/*Process newSeems = new Process();
			newSeems.StartInfo.FileName = Application.ExecutablePath;
			if( MainForm.CurrentFilepath.Length > 0 )
				newSeems.StartInfo.Arguments = "\"" + MainForm.CurrentFilepath + "\" " + MainForm.CurrentScanIndex;
			newSeems.Start();
			Process.GetCurrentProcess().Kill();*/

			string message = e.Exception.Message;
			if( e.Exception.InnerException != null )
				message += "\n\nAdditional information: " + e.Exception.InnerException.Message;
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