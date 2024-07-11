using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Tools;

namespace pwiz.Skyline.Model.AlphaPeptDeep
{
    public class AlphapeptdeepLibraryBuilder : IiRTCapableLibraryBuilder, IProgressMonitor
    {
        private const string BACK_SLASH = @"\";
        private const string SCRIPTS = @"Scripts";
        private const string PEPTDEEP_EXECUTABLE = @"peptdeep.exe";

        private static string _pythonBinary = @"C:\Users\Jason\AppData\Local\Microsoft\WindowsApps\python.exe";
        private static string _alphaPeptDeepCli = @"C:\Users\Jason\AppData\Local\Packages\PythonSoftwareFoundation.Python.3.12_qbz5n2kfra8p0\LocalCache\local-packages\Python312\Scripts\peptdeep.exe";
        private static string _alphaPeptDeepSettings = @"C:\Users\Jason\workspaces\alphapeptdeep_dia\settings.yaml";

        private readonly string _pythonVirtualEnvironmentDir;

        private string PeptdeepExecutablePath =>
            _pythonVirtualEnvironmentDir + BACK_SLASH + SCRIPTS + BACK_SLASH + PEPTDEEP_EXECUTABLE;

        public AlphapeptdeepLibraryBuilder(string libName, string libOutPath, string pythonVirtualEnvironmentDir)
        { 
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            _pythonVirtualEnvironmentDir = pythonVirtualEnvironmentDir;
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
            RunAlphapeptdeep();

            return true;
        }

        private bool RunAlphapeptdeep()
        {
            // TODO(xgwang): update to execute in a process with progress tracking
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(PeptdeepExecutablePath, $@"library {_alphaPeptDeepSettings}")
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