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
        private static void OnStderrCaptured(string e) => StderrCaptured?.Invoke(null, e);

        public static class LogCallbackWrapper
        {
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void LogCallback([MarshalAs(UnmanagedType.LPWStr)] string info);

            [DllImport("pwiz_bindings_cli.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            private static extern void SetCerrCallback([MarshalAs(UnmanagedType.FunctionPtr)] LogCallback callbackPointer);

            private static LogCallback logCallback;

            internal static void Init()
            {
                logCallback = OnStderrCaptured;
                SetCerrCallback(logCallback);
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            LogCallbackWrapper.Init();

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
