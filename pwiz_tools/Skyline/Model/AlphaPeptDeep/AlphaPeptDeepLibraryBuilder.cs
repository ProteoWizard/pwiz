using System;
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.AlphaPeptDeep
{
    public class AlphapeptdeepLibraryBuilder : IiRTCapableLibraryBuilder
    {
        private const string PEPTDEEP_EXECUTABLE = "peptdeep.exe";

        private string PythonVirtualEnvironmentScriptsDir { get; }
        private string PeptdeepExecutablePath => Path.Combine(PythonVirtualEnvironmentScriptsDir, PEPTDEEP_EXECUTABLE);

        public AlphapeptdeepLibraryBuilder(string libName, string libOutPath, string pythonVirtualEnvironmentScriptsDir)
        { 
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
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
            IProgressStatus progressStatus = new ProgressStatus();
            try
            {
                var result = RunAlphapeptdeep(progress, ref progressStatus);
                progress.UpdateProgress(progressStatus = progressStatus.Complete());
                return result;
            }
            catch (Exception exception)
            {
                progress.UpdateProgress(progressStatus.ChangeErrorException(exception));
                return false;
            }
        }

        private bool RunAlphapeptdeep(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progressStatus = progressStatus.ChangeSegments(0, 5);

            var inputFilePath = PrepareInputFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            var outputFilePath = GetOutputFilePath(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            var settingsFilePath = PrepareSettingsFile(inputFilePath, outputFilePath, progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            if (!ExecutePeptdeep(settingsFilePath, progress, ref progressStatus))
            {
                return false;
            }
            progressStatus = progressStatus.NextSegment();

            if (!ImportSpectralLibrary(outputFilePath, progress, ref progressStatus))
            {
                return false;
            }

            return true;
        }

        private string PrepareInputFile(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Preparing input file")
                .ChangePercentComplete(0));
            //TODO(xgwang): implement
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
            return "";
        }

        private string GetOutputFilePath(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Getting output file path")
                .ChangePercentComplete(0));
            //TODO(xgwang): implement
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
            return "";
        }

        private string PrepareSettingsFile(string inputFilePath, string outputFilePath, IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Preparing settings file")
                .ChangePercentComplete(0));
            //TODO(xgwang): implement
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
            return "";
        }

        private bool ExecutePeptdeep(string settingsFilePath, IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Executing peptdeep")
                .ChangePercentComplete(0));
            //TODO(xgwang): implement
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
            return true;
        }

        private bool ImportSpectralLibrary(string libraryFilePath, IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Importing spectral library")
                .ChangePercentComplete(0));
            //TODO(xgwang): implement
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
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