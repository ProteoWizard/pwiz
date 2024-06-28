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

namespace pwiz.Skyline.Model.Carafe
{
    public class CarafeLibraryBuilder : IiRTCapableLibraryBuilder, IProgressMonitor
    {
        private readonly string _mzMlFilePath;
        private readonly string _proteinDatabaseFilePath;
        private readonly string _spectralLibraryFilePath;
        private static string _carafeAppBinary = @"C:\Users\Jason\workspaces\carafe\build\CarafeApp.jar";
        private static string _javaBinary = @"C:\Program Files\Common Files\Oracle\Java\javapath\java.exe";

        public CarafeLibraryBuilder(string libName, string libOutPath, string mzMLFilePath, string proteinDatabaseFilePath,
            string spectralLibraryFilePath)
        {
            _mzMlFilePath = mzMLFilePath;
            _proteinDatabaseFilePath = proteinDatabaseFilePath;
            _spectralLibraryFilePath = spectralLibraryFilePath;
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
            Debug.WriteLine("HELLO from Carafe!");
            Debug.WriteLine($"mzMLFilePath: {_mzMlFilePath}");
            Debug.WriteLine($"proteinDatabaseFilePath: {_proteinDatabaseFilePath}");
            Debug.WriteLine($"spectralLibraryFilePath: {_spectralLibraryFilePath}");
            
            //Start a new process to use java.exe to execute the carafe.jar
            RunCarafeApp();

            return true;
        }

        private bool RunCarafeApp()
        {
            var pr = new ProcessRunner();
            // var psi = new ProcessStartInfo(JavaDownloadInfo.JavaBinary, $@"-jar {_carafeAppBinary}")
            var psi = new ProcessStartInfo(_javaBinary, $@"-jar {_carafeAppBinary}")
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
