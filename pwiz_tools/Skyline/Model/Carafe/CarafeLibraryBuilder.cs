using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Ionic.Zip;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model.AlphaPeptDeep;

namespace pwiz.Skyline.Model.Carafe
{
    public class CarafeLibraryBuilder : IiRTCapableLibraryBuilder
    {
        private const string BIN = @"bin";
        private const string CARAFE = @"carafe";
        private const string CARAFE_VERSION = @"0.0.1";
        private const string CMD_ARG_C = @"/C";
        private const string CMD_EXECUTABLE = @"cmd.exe";
        private const string CONDITIONAL_CMD_PROCEEDING_SYMBOL = TextUtil.AMPERSAND + TextUtil.AMPERSAND;
        private const string DOT_JAR = @".jar";
        private const string DOT_ZIP = @".zip";
        private const string DOWNLOADS = @"Downloads";
        private const string HYPHEN = TextUtil.HYPHEN;
        private const string JAVA = @"java";
        private const string JAVA_EXECUTABLE = @"java.exe";
        private const string JAVA_SDK_DOWNLOAD_URL = @"https://download.oracle.com/java/21/latest/";
        private const string OUTPUT_LIBRARY = @"output_library";
        private const string OUTPUT_LIBRARY_FILE_NAME = "SkylineAI_spectral_library.blib";
        private const string SPACE = TextUtil.SPACE;

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
        private string PythonVersion { get; }
        private string PythonVirtualEnvironmentName { get; }
        private SrmDocument Document { get; }
        [CanBeNull] private string ProteinDatabaseFilePath { get;  }
        private string ExperimentDataFilePath { get; }
        private string ExperimentDataDiaSearchResultFilePath { get; }

        private bool BuildLibraryForCurrentSkylineDocument => ProteinDatabaseFilePath.IsNullOrEmpty();
        private string PythonVirtualEnvironmentActivateScriptPath =>
            PythonInstallerUtil.GetPythonVirtualEnvironmentActivationScriptPath(PythonVersion,
                PythonVirtualEnvironmentName);
        private string RootDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CARAFE);
        private string JavaDir => Path.Combine(RootDir, JAVA);
        private string CarafeDir => Path.Combine(RootDir, CARAFE);
        private string UserDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private DirectoryInfo JavaDirInfo => new DirectoryInfo(JavaDir);
        private DirectoryInfo CarafeDirInfo => new DirectoryInfo(CarafeDir);
        private string JavaSdkDownloadFileName => @"jdk-21_windows-x64_bin.zip";
        private Uri JavaSdkUri => new Uri(JAVA_SDK_DOWNLOAD_URL + JavaSdkDownloadFileName);
        private string JavaSdkDownloadPath => Path.Combine(JavaDir, JavaSdkDownloadFileName);
        private string JavaExecutablePath { get; set; }
        private string CarafeFileBaseName => CARAFE + HYPHEN + CARAFE_VERSION;
        private string CarafeJarZipFileName => CarafeFileBaseName + DOT_ZIP;
        private string CarafeJarFileName => CarafeFileBaseName + DOT_JAR;
        private string CarafeJarZipDownloadUrl = @$"https://github.com/Noble-Lab/Carafe/releases/download/v{CARAFE_VERSION}/";
        private Uri CarafeJarZipUri => new Uri(CarafeJarZipDownloadUrl + CarafeJarZipFileName);
        private string CarafeJarZipLocalPath => Path.Combine(UserDir, DOWNLOADS, CarafeJarZipFileName);
        private Uri CarafeJarZipLocalUri => new Uri(@$"file:///{CarafeJarZipLocalPath}");
        private string CarafeJarZipDownloadPath => Path.Combine(CarafeDir, CarafeJarZipFileName);
        private string CarafeOutputLibraryDir => Path.Combine(CarafeDir, OUTPUT_LIBRARY);
        private string CarafeOutputLibraryFilePath => Path.Combine(CarafeOutputLibraryDir, OUTPUT_LIBRARY_FILE_NAME);
        private string CarafeJarFileDir => Path.Combine(CarafeDir, CarafeFileBaseName);
        private string CarafeJarFilePath => Path.Combine(CarafeJarFileDir, CarafeJarFileName);
        private IList<ArgumentAndValue> CmdFlowCommandArguments =>
            new[]
            {
                new ArgumentAndValue(@"-jar", CarafeJarFilePath),

                new ArgumentAndValue(@"-db", ProteinDatabaseFilePath),
                new ArgumentAndValue(@"-i", @"C:\Users\Jason\workspaces\test_carafe\report.tsv"),
                new ArgumentAndValue(@"-ms", @"C:\Users\Jason\workspaces\test_carafe\LFQ_Orbitrap_AIF_Human_01.mzML"),
                new ArgumentAndValue(@"-o", CarafeOutputLibraryDir),
                new ArgumentAndValue(@"-c_ion_min", @"2"),
                new ArgumentAndValue(@"-cor", @"0.8"),
                new ArgumentAndValue(@"-device", @"cpu"),
                new ArgumentAndValue(@"-enzyme", @"2"),
                new ArgumentAndValue(@"-ez", string.Empty),
                new ArgumentAndValue(@"-fast", string.Empty),
                new ArgumentAndValue(@"-fixMod", @"1"),
                new ArgumentAndValue(@"-itol", @"20"),
                new ArgumentAndValue(@"-itolu", @"ppm"),
                new ArgumentAndValue(@"-lf_frag_n_min", @"2"),
                new ArgumentAndValue(@"-lf_top_n_frag", @"20"),
                new ArgumentAndValue(@"-lf_type", @"skyline"),
                new ArgumentAndValue(@"-max_pep_mz", @"1000"),
                new ArgumentAndValue(@"-maxLength", @"35"),
                new ArgumentAndValue(@"-maxVar", @"1"),
                new ArgumentAndValue(@"-min_mz", @"200"),
                new ArgumentAndValue(@"-min_pep_mz", @"400"),
                new ArgumentAndValue(@"-minLength", @"7"),
                new ArgumentAndValue(@"-miss_c", @"1"),
                new ArgumentAndValue(@"-mode", @"general"),
                new ArgumentAndValue(@"-n_ion_min", @"2"),
                new ArgumentAndValue(@"-na", @"0"),
                new ArgumentAndValue(@"-nf", @"4"),
                new ArgumentAndValue(@"-nm", string.Empty),
                new ArgumentAndValue(@"-rf_rt_win", @"1"),
                new ArgumentAndValue(@"-rf", string.Empty),
                new ArgumentAndValue(@"-se", @"DIA-NN"),
                new ArgumentAndValue(@"-seed", @"2000"),
                new ArgumentAndValue(@"-skyline", string.Empty),
                new ArgumentAndValue(@"-tf", @"all"),
                new ArgumentAndValue(@"-valid", string.Empty),
                new ArgumentAndValue(@"-varMod", @"0")
            }; 
        

        public CarafeLibraryBuilder(
            string libName,
            string libOutPath,
            string pythonVersion,
            string pythonVirtualEnvironmentName,
            string experimentDataFilePath,
            string experimentDataDiaSearchResultFilePath,
            SrmDocument document)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataDiaSearchResultFilePath = experimentDataDiaSearchResultFilePath;
            Document = document;
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(JavaDir);
            Directory.CreateDirectory(CarafeDir);
        }

        public CarafeLibraryBuilder(
            string libName,
            string libOutPath,
            string pythonVersion,
            string pythonVirtualEnvironmentName,
            string proteinDatabaseFilePath,
            string experimentDataFilePath,
            string experimentDataDiaSearchResultFilePath,
            SrmDocument document)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            ProteinDatabaseFilePath = proteinDatabaseFilePath;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataDiaSearchResultFilePath = experimentDataDiaSearchResultFilePath;
            Document = document;
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(JavaDir);
            Directory.CreateDirectory(CarafeDir);
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            IProgressStatus progressStatus = new ProgressStatus();
            try
            {
                RunCarafe(progress, ref progressStatus);
                progress.UpdateProgress(progressStatus = progressStatus.Complete());
                return true;
            }
            catch (Exception exception)
            {
                progress.UpdateProgress(progressStatus.ChangeErrorException(exception));
                return false;
            }
        }

        private void RunCarafe(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progressStatus = progressStatus.ChangeSegments(0, 3);

            SetupJavaEnvironment(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            ExecuteCarafe(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();

            ImportSpectralLibrary(progress, ref progressStatus);
        }
        private void SetupJavaEnvironment(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Setting up Java environment")
                .ChangePercentComplete(0));

            var isJavaValid = ValidateJava();
            var isCarafeValid = ValidateCarafe();
            if (isJavaValid && isCarafeValid)
            {
                return;
            }

            if (!isJavaValid)
            {
                // clean java dir
                JavaDirInfo.Delete(true);
                Directory.CreateDirectory(JavaDir);

                // download java sdk package
                using var webClient = new MultiFileAsynchronousDownloadClient(progress, 1);
                if (!webClient.DownloadFileAsync(JavaSdkUri, JavaSdkDownloadPath, out var exception))
                    throw new Exception(
                        @"Failed to download java sdk package", exception);

                // unzip java sdk package
                using var javaSdkZip = ZipFile.Read(JavaSdkDownloadPath);
                javaSdkZip.ExtractAll(JavaDir);
                SetJavaExecutablePath();
            }

            if (!isCarafeValid)
            {
                // clean carafe dir
                CarafeDirInfo.Delete(true);
                Directory.CreateDirectory(CarafeDir);

                // download carafe jar package
                using var webClient = new MultiFileAsynchronousDownloadClient(progress, 1);
                // TODO(xgwang): update to CarafeJarZipUri after carafe repo goes public
                if (!webClient.DownloadFileAsync(CarafeJarZipLocalUri, CarafeJarZipDownloadPath, out var exception))
                    throw new Exception(
                        @"Failed to download carafe jar package", exception);

                // unzip carafe jar package
                using var carafeJarZip = ZipFile.Read(CarafeJarZipDownloadPath);
                carafeJarZip.ExtractAll(CarafeDir);
            }


            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }

        private bool ValidateJava()
        {
            var dirs = Directory.GetDirectories(JavaDir);
            if (dirs.Length != 1)
            {
                return false;
            }
            var javaSdkDir = dirs[0];
            var javaExecutablePath = Path.Combine(javaSdkDir, BIN, JAVA_EXECUTABLE);
            if (!File.Exists(javaExecutablePath))
            {
                return false;
            }
            JavaExecutablePath = javaExecutablePath;
            return true;
        }

        private bool ValidateCarafe()
        {
            if (!File.Exists(CarafeJarFilePath))
            {
                return false;
            }
            return true;
        }

        private void SetJavaExecutablePath()
        {
            var dirs = Directory.GetDirectories(JavaDir);
            Assume.IsTrue(dirs.Length.Equals(1), @"Java directory has more than one java SDKs");
            var javaSdkDir = dirs[0];
            JavaExecutablePath = Path.Combine(javaSdkDir, BIN, JAVA_EXECUTABLE);
        }

        private void ExecuteCarafe(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Executing carafe")
                .ChangePercentComplete(0));

            var cmd = new StringBuilder();

            // add activate python virtual env command
            cmd.Append(PythonVirtualEnvironmentActivateScriptPath);
            cmd.Append(SPACE);
            cmd.Append(CONDITIONAL_CMD_PROCEEDING_SYMBOL);
            cmd.Append(SPACE);

            // add java carafe command
            cmd.Append(JavaExecutablePath);
            cmd.Append(SPACE);

            // add carafe args
            foreach (var arg in CarafeArguments)
            {
                cmd.Append(arg.Key);
                cmd.Append(SPACE);
                if (arg.Value.IsNullOrEmpty()) { continue; }
                cmd.Append(arg.Value);
                cmd.Append(SPACE);
            }

            // execute command
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(CMD_EXECUTABLE)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };
            try
            {
                pr.Run(psi, cmd.ToString(), progress, ref progressStatus, ProcessPriorityClass.BelowNormal, true);
            }
            catch (Exception ex)
            {
                // TODO(xgwang): update this exception to an Carafe specific one
                throw new Exception(@"Failed to build library by executing carafe", ex);
            }

            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }

        private void ImportSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Importing spectral library")
                .ChangePercentComplete(0));

            var source = CarafeOutputLibraryFilePath;
            var dest = LibrarySpec.FilePath;
            try
            {
                File.Copy(source, dest, true);
            }
            catch (Exception ex)
            {
                // TODO(xgwang): update this exception to an Carafe specific one
                throw new Exception(
                    @$"Failed to copy carafe output library file from {source} to {dest}", ex);
            }
            
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }
    }
}

