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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ionic.Zip;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.Carafe
{
    public class CarafeLibraryBuilder : AbstractDeepLibraryBuilder, IiRTCapableLibraryBuilder
    {
        public const string CARAFE_NAME = @"Carafe";
        
        private const string PRECURSOR = @"Precursor";
        private const string PEPTIDE = @"Peptide";
        private const string PRECURSOR_CHARGE = @"Precursor Charge";
        private const string ISOTOPE_LABEL_TYPE = @"Isotope Label Type";
        private const string PRECURSOR_MZ = @"Precursor Mz";
        private const string MODIFIED_SEQUENCE = @"Modified Sequence";
        private const string PRECURSOR_EXPLICIT_COLLISION_ENERGY = @"Precursor Explicit Collision Energy";
        private const string PRECURSOR_NOTE = @"Precursor Note";
        private const string LIBRARY_NAME = @"Library Name";
        private const string LIBRARY_TYPE = @"Library Type";
        private const string LIBRARY_PROBABILITY_SCORE = @"Library Probability Score";
        private const string PEPTIDE_MODIFIED_SEQUENCE_UNIMOD_IDS = @"Peptide Modified Sequence Unimod Ids";
        private const string BEST_RT = @"Best Retention Time";
        private const string MIN_RT = @"Min Start Time";
        private const string MAX_RT = @"Max End Time";
        private const string IONMOB_MS1 = @"Ion Mobility MS1";
        private const string APEX_SPECTRUM_ID = @"Apex Spectrum ID Fragment";
        private const string FILE_NAME = @"File Name";
        private const string Q_VALUE = @"Detection Q Value";

        private const string BIN = @"bin";
        private const string INPUT = @"input";
        private const string CARAFE = @"carafe";
        private const string CARAFE_VERSION = @"0.0.1";
        // private const string CMD_ARG_C = @"/C";
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
        private const string NA = @"#N/A";


        private static readonly IEnumerable<string> TrainingTableColumnNamesCarafe =
            new[]
            {
                PRECURSOR, PEPTIDE, PRECURSOR_CHARGE, ISOTOPE_LABEL_TYPE, PRECURSOR_MZ, MODIFIED_SEQUENCE, PRECURSOR_EXPLICIT_COLLISION_ENERGY,
                PRECURSOR_NOTE, LIBRARY_NAME, LIBRARY_TYPE, LIBRARY_PROBABILITY_SCORE, PEPTIDE_MODIFIED_SEQUENCE_UNIMOD_IDS, BEST_RT, MIN_RT,
                MAX_RT, IONMOB_MS1, APEX_SPECTRUM_ID, FILE_NAME, Q_VALUE
            };

        private static readonly IEnumerable<string> PrecursorTableColumnNamesCarafe =
            new[]
            {
                PRECURSOR, PEPTIDE, PRECURSOR_CHARGE, ISOTOPE_LABEL_TYPE, PRECURSOR_MZ, MODIFIED_SEQUENCE, PRECURSOR_EXPLICIT_COLLISION_ENERGY,
                PRECURSOR_NOTE, LIBRARY_NAME, LIBRARY_TYPE, LIBRARY_PROBABILITY_SCORE, PEPTIDE_MODIFIED_SEQUENCE_UNIMOD_IDS
            };

        public static string PythonVersionSetting => Settings.Default.PythonEmbeddableVersion;
        public static string ScriptsDir => PythonInstallerUtil.GetPythonVirtualEnvironmentScriptsDir(PythonVersionSetting, CARAFE_NAME);

        protected override IEnumerable<string> GetHeaderColumnNames(bool training)
        {
            return training ? TrainingTableColumnNamesCarafe : PrecursorTableColumnNamesCarafe;
        }
        protected override string GetModName(ModificationType mod, string unmodifiedSequence, int modIndexAA)
        {
            return mod.Name;
        }

        protected override string GetTableRow(PeptideDocNode peptide, ModifiedSequence modifiedSequence,
            int charge, bool training, string modsBuilder, string modSitesBuilder)
        {
            // CONSIDER: For better error checking, existence of the charge could be checked first and
            // throw if it is missing with a more informative message. The caller is expected to provide a valid charge.
            var docNode = peptide.TransitionGroups.First(group => group.PrecursorCharge == charge);

            var precursor = LabelPrecursor(docNode.TransitionGroup, docNode.PrecursorMz, string.Empty);
            var collisionEnergy = docNode.ExplicitValues.CollisionEnergy != null ? docNode.ExplicitValues.CollisionEnergy.ToString() : NA;
            var note = docNode.Annotations.Note != null ? TextUtil.Quote(docNode.Annotations.Note) : NA;
            var libraryName = docNode.LibInfo?.LibraryName ?? NA;
            var libraryType = docNode.LibInfo?.LibraryTypeName ?? NA;
            var libraryScore = docNode.LibInfo?.Score != null ? docNode.LibInfo.Score.ToString() : NA;
            var unimodSequence = modifiedSequence.ToString();
            var best_rt = docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.RetentionTime;
            var min_rt = docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.StartRetentionTime;
            var max_rt = docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.EndRetentionTime;
            string ionmob_ms1 = docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.IonMobilityInfo?.IonMobilityMS1.HasValue == true ?
                docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.IonMobilityInfo.IonMobilityMS1.ToString() : NA;
            var apex_psm = @"unknown"; //docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.Identified
            var filename = Path.GetFileNameWithoutExtension(ExperimentDataFilePath);
            if (training)
            {
                return string.Join(TextUtil.SEPARATOR_TSV_STR, precursor, modifiedSequence.GetUnmodifiedSequence(), charge,
                    docNode.LabelType, docNode.PrecursorMz, unimodSequence, collisionEnergy, note, libraryName, libraryType,
                    libraryScore, modifiedSequence.UnimodIds, best_rt, min_rt, max_rt, ionmob_ms1, apex_psm, filename, libraryScore);
            }
            else
            {
                return string.Join(TextUtil.SEPARATOR_TSV_STR, precursor, modifiedSequence.GetUnmodifiedSequence(), charge,
                    docNode.LabelType, docNode.PrecursorMz, unimodSequence, collisionEnergy, note, libraryName, libraryType,
                    libraryScore, modifiedSequence.UnimodIds);
            }
        }

        protected override string ToolName => CARAFE;
        protected override LibraryBuilderModificationSupport LibraryBuilderModificationSupport { get; }
        public LibrarySpec LibrarySpec { get; private set; }
        private string PythonVersion { get; }
        private string PythonVirtualEnvironmentName { get; }
        [CanBeNull] private string ProteinDatabaseFilePath { get;  }
        internal string ExperimentDataFilePath { get; set;  }
        internal string ExperimentDataTuningFilePath { get; set; }

        public override string InputFilePath => Path.Combine(RootDir, InputFileName);
        public override string TrainingFilePath => null;    // Not yet implemented
        
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
        private Uri CarafeJarZipDownloadUrl = new Uri(@$"https://github.com/Noble-Lab/Carafe/releases/download/v{CARAFE_VERSION}/{CARAFE}-{CARAFE_VERSION}{DOT_ZIP}");
        private Uri CarafeJarZipUri => new Uri(CarafeJarZipDownloadUrl + CarafeJarZipFileName);
        private string CarafeJarZipLocalPath => Path.Combine(UserDir, DOWNLOADS, CarafeJarZipFileName);
        private Uri CarafeJarZipLocalUri => new Uri(@$"file:///{CarafeJarZipLocalPath}");
        private string CarafeJarZipDownloadPath => Path.Combine(CarafeDir, CarafeJarZipFileName);
        private string CarafeOutputLibraryDir => Path.Combine(CarafeDir, OUTPUT_LIBRARY);
        private string CarafeOutputLibraryFilePath => Path.Combine(CarafeOutputLibraryDir, OUTPUT_LIBRARY_FILE_NAME);

        private string CarafeJarFileDir => Path.Combine(CarafeDir, CarafeFileBaseName);
        private string CarafeJarFilePath => Path.Combine(CarafeJarFileDir, CarafeJarFileName);
        private string InputFileName => INPUT + TextUtil.UNDERSCORE + Convert.ToBase64String(Encoding.ASCII.GetBytes(Document.DocumentHash)) + TextUtil.EXT_TSV;
        private IList<ArgumentAndValue> CommandArguments =>
            new []
            {
                new ArgumentAndValue(@"jar", CarafeJarFilePath, TextUtil.HYPHEN),
                new ArgumentAndValue(@"db", InputFilePath , TextUtil.HYPHEN),
                new ArgumentAndValue(@"i", ExperimentDataTuningFilePath, TextUtil.HYPHEN),
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
                new ArgumentAndValue(@"se", @"DIA-NN", TextUtil.HYPHEN),
                new ArgumentAndValue(@"skyline", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"tf", @"all", TextUtil.HYPHEN),
                new ArgumentAndValue(@"valid", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"varMod", @"0", TextUtil.HYPHEN)
            };

        public CarafeLibraryBuilder(
            string libName,
            string libOutPath,
            string pythonVersion,
            string pythonVirtualEnvironmentName,
            string experimentDataFilePath,
            string experimentDataTuningFilePath,
            SrmDocument document,
            IrtStandard irtStandard) : base(document, irtStandard)
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(JavaDir);
            Directory.CreateDirectory(CarafeDir);
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataTuningFilePath = experimentDataTuningFilePath;
            LibraryBuilderModificationSupport = new LibraryBuilderModificationSupport(null);
        }

        public CarafeLibraryBuilder(
            string libName,
            string libOutPath,
            string pythonVersion,
            string pythonVirtualEnvironmentName,
            string proteinDatabaseFilePath,
            string experimentDataFilePath,
            string experimentDataTuningFilePath,
            SrmDocument document,
            IrtStandard irtStandard) : base(document, irtStandard)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            ProteinDatabaseFilePath = proteinDatabaseFilePath;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataTuningFilePath = experimentDataTuningFilePath;
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(JavaDir);
            Directory.CreateDirectory(CarafeDir);
            LibraryBuilderModificationSupport = new LibraryBuilderModificationSupport(null);
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
            if (BuildLibraryForCurrentSkylineDocument)
            {
                //AbstractDeepLibraryBuilder.PrepareInputFile(Document, progress, ref progressStatus, @"carafe");
                
            }
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
                webClient.DownloadFileAsyncOrThrow(JavaSdkUri, JavaSdkDownloadPath);

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
                webClient.DownloadFileAsyncOrThrow(CarafeJarZipDownloadUrl, CarafeJarZipDownloadPath);

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

         
            // compose carafe cmd command arguments to build library
            // add activate python virtual env command
            var commandArgs = TextUtil.SpaceSeparate(CommandArguments.Select(a => a.ToString()));
            var args = TextUtil.SpaceSeparate(PythonVirtualEnvironmentActivateScriptPath,
                CONDITIONAL_CMD_PROCEEDING_SYMBOL, JavaExecutablePath, commandArgs);

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
                pr.Run(psi, args.ToString(), progress, ref progressStatus, ProcessPriorityClass.BelowNormal, true);
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

        private static string LabelPrecursor(TransitionGroup tranGroup, double precursorMz,
            string resultsText)
        {
            return string.Format(@"{0}{1}{2}{3}", LabelMz(tranGroup, precursorMz),
                Transition.GetChargeIndicator(tranGroup.PrecursorAdduct),
                tranGroup.LabelTypeText, resultsText);
        }

        private static string LabelMz(TransitionGroup tranGroup, double precursorMz)
        {
            int? massShift = tranGroup.DecoyMassShift;
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            return string.Format(CultureInfo.InvariantCulture, @"{0:F04}{1}", precursorMz - shift,
                Transition.GetDecoyText(massShift));
        }
    }
}

