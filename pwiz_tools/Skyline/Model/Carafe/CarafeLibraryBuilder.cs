/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ionic.Zip;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model.AlphaPeptDeep;
using pwiz.Skyline.Model.DocSettings;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Diagnostics;

[assembly: InternalsVisibleTo("TestFunctional")]

namespace pwiz.Skyline.Model.Carafe
{
    public class CarafeLibraryBuilder : IiRTCapableLibraryBuilder
    {
        internal const string ECHO = @"echo";
        private const string BIN = @"bin";
        private const string INPUT = @"input"; 
        private const string TRAIN = @"train";
        private const string CARAFE = @"carafe";
        private const string CARAFE_VERSION = @"0.0.1";
        private const string CARAFE_DEV = @"-dev"; 
        private const string CARAFE_DEV_VERSION = CARAFE_DEV + @"-20250228T195109Z-001";
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
        private const string TAB = "\t";

        internal TextWriter Writer { get; }

        public ISkylineProcessRunnerWrapper SkylineProcessRunner { get; set; }

        private string PythonVirtualEnvironmentScriptsDir { get; }
        private LibraryHelper LibraryHelper { get; set; }
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
        private SrmDocument TrainingDocument { get; }
        public string ProteinDatabaseFilePath { get; private set; }
        internal string ExperimentDataFilePath { get; set;  }
        internal string ExperimentDataTuningFilePath { get; set; }

        private bool BuildLibraryForCurrentSkylineDocument => ProteinDatabaseFilePath.IsNullOrEmpty();
        private string PythonVirtualEnvironmentActivateScriptPath =>
            PythonInstallerUtil.GetPythonVirtualEnvironmentActivationScriptPath(PythonVersion,
                PythonVirtualEnvironmentName);
        private string RootDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CARAFE, LibraryHelper.TimeStamp);
        private string JavaDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), JAVA);
        private string CarafeDir => Path.Combine(RootDir, CARAFE); 
        private string CarafeJavaDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CARAFE);
        private string UserDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private DirectoryInfo JavaDirInfo => new DirectoryInfo(JavaDir);
        private DirectoryInfo CarafeJavaDirInfo => new DirectoryInfo(CarafeJavaDir);
        private DirectoryInfo CarafeDirInfo => new DirectoryInfo(CarafeDir);
        private string JavaSdkDownloadFileName => @"jdk-21_windows-x64_bin.zip";
        private Uri JavaSdkUri => new Uri(JAVA_SDK_DOWNLOAD_URL + JavaSdkDownloadFileName);
        private string JavaSdkDownloadPath => Path.Combine(JavaDir, JavaSdkDownloadFileName);
        private string JavaExecutablePath { get; set; }
        private string CarafeFileBaseName => CARAFE + HYPHEN + CARAFE_VERSION;
        private string CarafeJarZipFileName => CarafeFileBaseName + CARAFE_DEV_VERSION + DOT_ZIP;
        private string CarafeJarFileName => CarafeFileBaseName + DOT_JAR;
        private Uri CarafeJarZipDownloadUrl =
            new Uri(@$"https://skyline.ms/_webdav/home/support/file%20sharing/%40files/{CARAFE}-{CARAFE_VERSION}{CARAFE_DEV_VERSION}{DOT_ZIP}"); 

        //Uri(@$"https://github.com/Noble-Lab/Carafe/releases/download/v{CARAFE_VERSION}-dev/{CARAFE}-{CARAFE_VERSION}{DOT_ZIP}");
        private Uri CarafeJarZipUri => new Uri(CarafeJarZipDownloadUrl + CarafeJarZipFileName);
        private string CarafeJarZipLocalPath => Path.Combine(UserDir, DOWNLOADS, CarafeJarZipFileName);
        private Uri CarafeJarZipLocalUri => new Uri(@$"file:///{CarafeJarZipLocalPath}");
        private string CarafeJarZipDownloadPath => Path.Combine(CarafeJavaDir, CarafeJarZipFileName);
        private string CarafeOutputLibraryDir => Path.Combine(CarafeDir, OUTPUT_LIBRARY);
        private string CarafeOutputLibraryFilePath => Path.Combine(CarafeOutputLibraryDir, OUTPUT_LIBRARY_FILE_NAME);

        public string BuilderLibraryPath => CarafeOutputLibraryFilePath;

        private string CarafeJarFileDir => Path.Combine(CarafeJavaDir, CarafeFileBaseName + CARAFE_DEV);
        private string CarafeJarFilePath => Path.Combine(CarafeJarFileDir, CarafeJarFileName);
        private string InputFileName => INPUT + TextUtil.UNDERSCORE + TextUtil.EXT_TSV; //Convert.ToBase64String(Encoding.ASCII.GetBytes(Document.DocumentHash)) + TextUtil.EXT_TSV;
        private string TrainingFileName => TRAIN + TextUtil.UNDERSCORE + TextUtil.EXT_TSV;
        private string InputFilePath => Path.Combine(RootDir, InputFileName);

        private string TrainingFilePath => Path.Combine(RootDir, TrainingFileName);


        private IList<ArgumentAndValue> CommandArguments =>
            new []
            {
                new ArgumentAndValue(@"jar", CarafeJarFilePath, TextUtil.HYPHEN),
                new ArgumentAndValue(@"db", ProteinDatabaseFilePath , TextUtil.HYPHEN),
                new ArgumentAndValue(@"i", InputFilePath, TextUtil.HYPHEN),
                new ArgumentAndValue(@"ms", ExperimentDataFilePath, TextUtil.HYPHEN),
                new ArgumentAndValue(@"o", CarafeOutputLibraryDir, TextUtil.HYPHEN),
                new ArgumentAndValue(@"c_ion_min", @"2", TextUtil.HYPHEN),
                new ArgumentAndValue(@"cor", @"0.8", TextUtil.HYPHEN),
                new ArgumentAndValue(@"device", @"gpu", TextUtil.HYPHEN),
                new ArgumentAndValue(@"enzyme", @"2", TextUtil.HYPHEN),
                new ArgumentAndValue(@"ez", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"fast", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"fixMod", @"1", TextUtil.HYPHEN),
                new ArgumentAndValue(@"itol", @"20", TextUtil.HYPHEN),
                new ArgumentAndValue(@"itolu", @"ppm", TextUtil.HYPHEN),
                new ArgumentAndValue(@"lf_frag_n_min", @"2", TextUtil.HYPHEN),
                new ArgumentAndValue(@"lf_top_n_frag", @"20", TextUtil.HYPHEN),
                new ArgumentAndValue(@"lf_type", @"skyline", TextUtil.HYPHEN),
                new ArgumentAndValue(@"max_pep_mz", @"1000", TextUtil.HYPHEN),
                new ArgumentAndValue(@"maxLength", @"35", TextUtil.HYPHEN),
                new ArgumentAndValue(@"maxVar", @"1", TextUtil.HYPHEN),
                new ArgumentAndValue(@"min_mz", @"200", TextUtil.HYPHEN),
                new ArgumentAndValue(@"min_pep_mz", @"400", TextUtil.HYPHEN),
                new ArgumentAndValue(@"minLength", @"7", TextUtil.HYPHEN),
                new ArgumentAndValue(@"miss_c", @"1", TextUtil.HYPHEN),
                new ArgumentAndValue(@"mode", @"general", TextUtil.HYPHEN),
                new ArgumentAndValue(@"n_ion_min", @"2", TextUtil.HYPHEN),
                new ArgumentAndValue(@"na", @"0", TextUtil.HYPHEN),
                new ArgumentAndValue(@"nf", @"4", TextUtil.HYPHEN),
                new ArgumentAndValue(@"nm", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"rf_rt_win", @"1", TextUtil.HYPHEN),
                new ArgumentAndValue(@"rf", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"seed", @"2000", TextUtil.HYPHEN),
                new ArgumentAndValue(@"se", @"skyline", TextUtil.HYPHEN),
                new ArgumentAndValue(@"skyline", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"tf", @"all", TextUtil.HYPHEN),
                new ArgumentAndValue(@"valid", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"varMod", @"0", TextUtil.HYPHEN)
            };


        /// <summary>
        /// List of UniMod Modifications available
        /// </summary>
        internal static readonly IList<ModificationType> AlphapeptdeepModificationName = populateUniModList();
        private static IList<ModificationType> populateUniModList()
        {
            IList<ModificationType> modList = new List<ModificationType>();
            for (int m = 0; m < UniModData.UNI_MOD_DATA.Length; m++)
            {
                if (!UniModData.UNI_MOD_DATA[m].ID.HasValue)
                    continue;
                var accession = UniModData.UNI_MOD_DATA[m].ID.Value + @":" + UniModData.UNI_MOD_DATA[m].Name;
                var name = UniModData.UNI_MOD_DATA[m].Name;
                var formula = UniModData.UNI_MOD_DATA[m].Formula;
                modList.Add(new ModificationType(accession, name, formula));
            }
            return modList;
        }

        public string ProductLibraryPath()
        {
            return CarafeOutputLibraryFilePath;
        }

        public CarafeLibraryBuilder(
            string libName,
            string libOutPath,
            string pythonVersion,
            string pythonVirtualEnvironmentName,
            string pythonVirtualEnvironmentScriptsDir,
            string experimentDataFilePath,
            string proteinDatabaseFilePath,
            SrmDocument document,
            TextWriter textWriter)
        {
            Document = document;
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            LibraryHelper = new LibraryHelper(InputFilePath, TrainingFilePath, experimentDataFilePath);
            Writer = textWriter;
            ProteinDatabaseFilePath = proteinDatabaseFilePath;
            PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
            ExperimentDataFilePath = experimentDataFilePath;
            Document = document;
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(JavaDir);
            //Directory.CreateDirectory(CarafeDir);
            Directory.CreateDirectory(CarafeJavaDir);
            //ExperimentDataTuningFilePath = experimentDataTuningFilePath;            
        }

        public CarafeLibraryBuilder(
            string libName,
            string libOutPath,
            string pythonVersion,
            string pythonVirtualEnvironmentName,
            string pythonVirtualEnvironmentScriptsDir,
            string proteinDatabaseFilePath,
            string experimentDataFilePath,
            string experimentDataTuningFilePath,
            SrmDocument document)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
            ProteinDatabaseFilePath = proteinDatabaseFilePath;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataTuningFilePath = experimentDataTuningFilePath;
            Document = document;
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(JavaDir);
            //Directory.CreateDirectory(CarafeDir);
            Directory.CreateDirectory(CarafeJavaDir);

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
            //progressStatus = progressStatus.ChangeSegments(0, 3);
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(@"Executing carafe")
                .ChangePercentComplete(0));

            SetupJavaEnvironment(progress, ref progressStatus);
            //progressStatus = progressStatus.NextSegment();
            //if (BuildLibraryForCurrentSkylineDocument)
            //{
               LibraryHelper.PreparePrecursorInputFile(Document, progress, ref progressStatus, @"carafe");

               if (TrainingDocument != null)
                   LibraryHelper.PrepareTrainingInputFile(Document, progress, ref progressStatus, @"carafe");

            // progressStatus = progressStatus.NextSegment();

            //}
            ExecuteCarafe(progress, ref progressStatus);
            //progressStatus = progressStatus.NextSegment();

            ImportSpectralLibrary(progress, ref progressStatus);
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
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
                
                CarafeJavaDirInfo.Delete(true);
                Directory.CreateDirectory(CarafeJavaDir);

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
                CarafeJavaDirInfo.Delete(true);
                Directory.CreateDirectory(RootDir);
                Directory.CreateDirectory(CarafeJavaDir);


                // download carafe jar package
                using var webClient = new MultiFileAsynchronousDownloadClient(progress, 1);
                if (!webClient.DownloadFileAsync(CarafeJarZipDownloadUrl, CarafeJarZipDownloadPath, out var exception))
                    throw new Exception(
                        @"Failed to download carafe jar package", exception);

                // unzip carafe jar package
                using var carafeJarZip = ZipFile.Read(CarafeJarZipDownloadPath);
                carafeJarZip.ExtractAll(CarafeJavaDir);
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
                .ChangePercentComplete(-1));

         
            // compose carafe cmd command arguments to build library
            var args = new StringBuilder();

            var cmdBuilder = new StringBuilder();
            CancellationToken cancelToken = CancellationToken.None;

            // add activate python virtual env command
            args.Append(PythonVirtualEnvironmentActivateScriptPath);
            args.Append(SPACE);
            args.Append(CONDITIONAL_CMD_PROCEEDING_SYMBOL);
            args.Append(SPACE);

            // add java carafe command
            args.Append(JavaExecutablePath);
            args.Append(SPACE);

            // add carafe args
            foreach (var arg in CommandArguments)
            {
                args.Append(arg).Append(SPACE);
            }

            cmdBuilder.Append(args).Append(SPACE);

            var cmd = string.Format(ToolsResources.PythonInstaller__0__Running_command____1____2__, ECHO,
                cmdBuilder, TextUtil.AMPERSAND);
           
            cmd += string.Format(
                ToolsResources
                    .PythonInstaller_PipInstall__0__This_sometimes_could_take_3_5_minutes__Please_be_patient___1__,
                ECHO, TextUtil.AMPERSAND);
            
            cmd += cmdBuilder;

            string batPath = Path.Combine(RootDir, "runCarafe.bat");
            File.WriteAllText(batPath, cmd);

            // execute command
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo() 
            {
                FileName = batPath,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            try
            {
                pr.EnableImmediateLog = true;
                pr.Run(psi, string.Empty, progress, ref progressStatus, Writer, ProcessPriorityClass.BelowNormal, true);
            }
            catch (Exception ex)
            {
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

