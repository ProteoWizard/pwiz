//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MSConvertGUI
{
    static class Program
    {
        public static MainForm MainWindow { get; private set; }

        public static event EventHandler<string> StderrCaptured;
        internal static void OnStderrCaptured(string e) => StderrCaptured?.Invoke(null, e);

        /// <summary>
        /// Sends everything written to <see cref="Console.Error"/> to <see cref="StderrCaptured"/>,
        /// which the progress window adds to each running job's log. The library writes its
        /// --continueOnError skip messages there, and vendor SDKs their errors; a WinForms app has
        /// no console, so without this they are lost. The cpp/CLI build did the same through
        /// pwiz_bindings_cli.dll's SetCerrCallback().
        /// </summary>
        internal static void CaptureStderr() => Console.SetError(TextWriter.Synchronized(new StderrForwarder()));

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Hook the vendor SDK assembly resolver before any Reader_* type is touched.
            // Without this, opening a Thermo / Bruker / Waters file fails with TypeLoadException
            // because the SDK DLLs aren't shipped in the installer — they get downloaded into
            // %LOCALAPPDATA%\ProteoWizard\vendor\<Vendor>-<Version>\ on first use.
            Pwiz.Vendor.Common.VendorSdkLoader.RegisterAssemblyResolver();


            // single instance code taken from
            // http://forge.fenchurch.mc.vanderbilt.edu/scm/viewvc.php/branches/IDPicker-3/Program.cs?revision=431&root=idpicker&view=markup

            var singleInstanceHandler = new SingleInstanceHandler(Application.ExecutablePath) { Timeout = 200 };

            singleInstanceHandler.Launching += (sender, e) =>
            {
                CaptureStderr();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                MSConvertRemoteAccountServices.Initialize();
                var singleInstanceArgs = e.Args.ToList();
                MainWindow = new MainForm(singleInstanceArgs);
                Application.Run(MainWindow);
            };
            try
            {
                singleInstanceHandler.Connect(args);
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }
        #region Exception handling
        public static void HandleException (Exception e)
        {
            MessageBox.Show(e.ToString(), "Error");
            return;
        }
        #endregion

        /// <summary>Forwards each write to <see cref="OnStderrCaptured"/>. The progress window
        /// buffers the pieces until a line ends.</summary>
        private sealed class StderrForwarder : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value) => Write(value.ToString());

            public override void Write(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return;
                // The caller is often a writer mid-conversion; a log that cannot be shown
                // (a closing window, say) must not abort it.
                try { OnStderrCaptured(value); }
                catch (Exception) { }
            }

            public override void Write(char[] buffer, int index, int count) => Write(new string(buffer, index, count));
        }
    }
}
