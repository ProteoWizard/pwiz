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
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MSConvertGUI
{
    static class Program
    {
        public static MainForm MainWindow { get; private set; }

        public static event EventHandler<string> StderrCaptured;
        internal static void OnStderrCaptured(string e) => StderrCaptured?.Invoke(null, e);

        // The cpp/CLI build registered a P/Invoke callback into pwiz_bindings_cli.dll's
        // SetCerrCallback() so vendor SDK stderr lines (Thermo / Bruker errors) could
        // bubble up to the GUI's log viewer. pwiz-sharp has no cpp/CLI bridge; vendor
        // SDKs run inside the same .NET process and write to the standard Console.Error
        // stream directly. Console output redirection happens at the MainForm level.

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
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
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
    }
}
