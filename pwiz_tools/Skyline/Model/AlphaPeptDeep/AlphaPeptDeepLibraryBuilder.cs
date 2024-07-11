using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.AlphaPeptDeep
{
    public class AlphaPeptDeepLibraryBuilder : IiRTCapableLibraryBuilder, IProgressMonitor
    {
        private static string _pythonBinary = @"C:\Users\Jason\AppData\Local\Microsoft\WindowsApps\python.exe";
        private static string _alphaPeptDeepCli = @"C:\Users\Jason\AppData\Local\Packages\PythonSoftwareFoundation.Python.3.12_qbz5n2kfra8p0\LocalCache\local-packages\Python312\Scripts\peptdeep.exe";
        private static string _alphaPeptDeepSettings = @"C:\Users\Jason\workspaces\alphapeptdeep_dia\settings.yaml";

        private static string _carafeAppBinary = @"C:\Users\Jason\workspaces\carafe\build\CarafeApp.jar";
        private static string _javaBinary = @"C:\Program Files\Common Files\Oracle\Java\javapath\java.exe";

        public AlphaPeptDeepLibraryBuilder(string libName, string libOutPath)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
        }

        public string AmbiguousMatchesMessage
        {
            //TODO(xgwang): implement
            get { return null; }
        }

        public IrtStandard IrtStandard
        {
            //TODO(xgwang): implement
            get { return null; }
        }

        public string BuildCommandArgs
        {
            //TODO(xgwang): implement
            get { return null; }
        }

        public string BuildOutput
        {
            //TODO(xgwang): implement
            get { return null; }
        }

        public LibrarySpec LibrarySpec { get; private set; }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            //TODO(xgwang): implement
            Debug.WriteLine("HELLO from AlphaPeptDeep!");

            //Start a new process to use java.exe to execute the carafe.jar
            RunAlphaPeptDeep();

            return true;
        }

        private bool RunAlphaPeptDeep()
        {
            // TODO: update to execute python in process
            var pr = new ProcessRunner();
            // var psi = new ProcessStartInfo(_javaBinary, $@"-jar {_carafeAppBinary}")
            var psi = new ProcessStartInfo(_alphaPeptDeepCli, $@"library {_alphaPeptDeepSettings}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };
            IProgressStatus status = new ProgressStatus().ChangeSegments(0, 1);
            pr.Run(psi, string.Empty, this, ref status, ProcessPriorityClass.BelowNormal, true);
            return true;
        }


        //TODO(xgwang): implement
        public bool IsCanceled => false;

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            //TODO(xgwang): implement
            return UpdateProgressResponse.normal;
        }

        public bool HasUI => false;
    }
}