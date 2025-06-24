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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Ionic.Zip;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using File = System.IO.File;

[assembly: InternalsVisibleTo("TestPerf")]

namespace pwiz.Skyline.Model.Lib.Carafe
{
    public class CarafeLibraryBuilder : AbstractDeepLibraryBuilder, IiRTCapableLibraryBuilder
    {
        public const string CARAFE = @"Carafe";

        internal const string ECHO = @"echo";
        private const string BIN = @"bin";
        private const string INPUT = @"input";
        private const string TRAIN = @"train";
        private const string CARAFE_VERSION = @"1.0.0";
        private const string CARAFE_URI_NAME = @"carafe-";
        private const string CARAFE_DEV = @"-dev";
        private const string CARAFE_DEV_VERSION = ""; //@"-beta"; //CARAFE_DEV + @"-20250304T224833Z-001";
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
        private const string OUTPUT_SPECTRAL_LIB_FILE_NAME = @"test_res_fine_tuned.csv";
        private const string SPACE = TextUtil.SPACE;
        private const string TAB = @"\t";

        public ISkylineProcessRunnerWrapper SkylineProcessRunner { get; set; }

        private string PythonVirtualEnvironmentScriptsDir { get; }

        public LibraryHelper LibraryHelper { get; private set; }

        

        public LibrarySpec LibrarySpec { get; private set; }

//        public static string PythonVersion { get; private set; }
        public static string PythonVersion => Settings.Default.PythonEmbeddableVersion;
        private string PythonVirtualEnvironmentName { get; }

        private string OutputSpectralLibsDir => Path.Combine(RootDir, OUTPUT_LIBRARY);
        private string OutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, OUTPUT_SPECTRAL_LIB_FILE_NAME);
        public new SrmDocument Document { get; }
        private SrmDocument TrainingDocument { get; }
        public string DbInputFilePath { get; private set; }
        internal string ExperimentDataFilePath { get; set; }
        internal string ExperimentDataTuningFilePath { get; set; }

        //internal string ProteinDatabaseFilePath;

        //private bool BuildLibraryForCurrentSkylineDocument => ProteinDatabaseFilePath.IsNullOrEmpty();
        private string PythonVirtualEnvironmentActivateScriptPath =>
            PythonInstallerUtil.GetPythonVirtualEnvironmentActivationScriptPath(PythonVersion,
                PythonVirtualEnvironmentName);


        private string _rootDir;
        private string RootDir
        {
            get => _rootDir;
            set => _rootDir = value;
        }


        private string JavaDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), JAVA);

        private string CarafeDir
        {
            get
            {
                InitializeLibraryHelper(RootDir);
                return Path.Combine(RootDir, CARAFE);
            }
        }

        private string CarafeJavaDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CARAFE);
        private string UserDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private DirectoryInfo JavaDirInfo => new DirectoryInfo(JavaDir);
        private DirectoryInfo CarafeJavaDirInfo => new DirectoryInfo(CarafeJavaDir);
        private DirectoryInfo CarafeDirInfo => new DirectoryInfo(CarafeDir);
        private string JavaSdkDownloadFileName => @"jdk-21_windows-x64_bin.zip";
        private Uri JavaSdkUri => new Uri(JAVA_SDK_DOWNLOAD_URL + JavaSdkDownloadFileName);
        private string JavaSdkDownloadPath => Path.Combine(JavaDir, JavaSdkDownloadFileName);
        private string JavaExecutablePath { get; set; }
        private string CarafeFileBaseName => @"carafe" + HYPHEN + CARAFE_VERSION;
        private string CarafeJarZipFileName => CarafeFileBaseName + CARAFE_DEV_VERSION + DOT_ZIP;
        private string CarafeJarFileName => CarafeFileBaseName + DOT_JAR;

        private static string AlphapeptdeepDiaRepo = @"https://codeload.github.com/wenbostar/alphapeptdeep_dia/zip/refs/tags/v1.0";
        protected override string ToolName => CARAFE;

        public static PythonInstaller CreatePythonInstaller(TextWriter writer)
        {
            var packages = new[]
            {
                new PythonPackage  { Name = AlphapeptdeepDiaRepo, Version = null},
                //  { Name = PEPTDEEP, Version = AlphapeptdeepDiaRepo },
                new PythonPackage { Name = @"alphabase", Version = @"1.2.1" },
                new PythonPackage { Name = @"numpy", Version = @"1.26.4" },
                new PythonPackage { Name = @"transformers", Version = @"4.36.1" },
                new PythonPackage { Name = @"torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu118 --upgrade", Version = null },
                new PythonPackage { Name = @"wheel", Version = null },
                new PythonPackage { Name = @"huggingface-hub", Version = null}
                
            };

            return new PythonInstaller(packages, writer, CARAFE);
        }

        private static string CARAFE_JAR_URI => @"https://github.com/Noble-Lab/Carafe/releases/download/v1.0.0/";
        private Uri CarafeJarZipDownloadUrl()
        {
            //return new Uri(@$"https://skyline.ms/_webdav/home/support/file%20sharing/%40files/carafe-{CARAFE_VERSION}{CARAFE_DEV_VERSION}{DOT_ZIP}");
            return new Uri(@$"{CARAFE_JAR_URI}{CARAFE_URI_NAME}{CARAFE_VERSION}{CARAFE_DEV_VERSION}{DOT_ZIP}");
        }

        //Uri(@$"https://github.com/Noble-Lab/Carafe/releases/download/v{CARAFE_VERSION}-dev/{CARAFE}-{CARAFE_VERSION}{DOT_ZIP}");
        private Uri CarafeJarZipUri()
        {
            return new Uri(CarafeJarZipDownloadUrl() + CarafeJarZipFileName);
        }

        private string CarafeJarZipLocalPath => Path.Combine(UserDir, DOWNLOADS, CarafeJarZipFileName);
        private Uri CarafeJarZipLocalUri => new Uri(@$"file:///{CarafeJarZipLocalPath}");
        private string CarafeJarZipDownloadPath => Path.Combine(CarafeJavaDir, CarafeJarZipFileName);

        private string CarafeOutputLibraryDir()
        {
            return Path.Combine(CarafeDir, OUTPUT_LIBRARY);
        }

        private string CarafeOutputLibraryFilePath(bool test = false)
        {
            if (!test)
                return Path.Combine(CarafeOutputLibraryDir(), OUTPUT_LIBRARY_FILE_NAME);
            return Path.Combine(CarafeOutputLibraryDir(), OUTPUT_SPECTRAL_LIB_FILE_NAME);
        }

        //        public string BuilderLibraryPath;

        private string CarafeJarFileDir => Path.Combine(CarafeJavaDir, CarafeFileBaseName);
        private string CarafeJarFilePath => Path.Combine(CarafeJarFileDir, CarafeJarFileName);

        private string InputFileName =>
            INPUT + TextUtil.UNDERSCORE +
            TextUtil.EXT_TSV; //Convert.ToBase64String(Encoding.ASCII.GetBytes(Document.DocumentHash)) + TextUtil.EXT_TSV;

        private string TrainingFileName => TRAIN + TextUtil.UNDERSCORE + TextUtil.EXT_TSV;

        public override string InputFilePath
        {
            get { return Path.Combine(RootDir, InputFileName); }
        }

        public string TuningFilePath { get; private set; }

        private bool _diann_training;

        public string BuilderLibraryPath { get; private set; }
        public string TestLibraryPath { get; private set; }

        public sealed override string TrainingFilePath => Path.Combine(RootDir, TrainingFileName);

        public static IDictionary<string, AbstractDdaSearchEngine.Setting> DataParameters { get; private set; }
        public static IDictionary<string, AbstractDdaSearchEngine.Setting> ModelParameters { get; private set; }
        public static IDictionary<string, AbstractDdaSearchEngine.Setting> LibraryParameters { get; private set; }


        public new void PrepareTrainingInputFile(IList<ModificationType> modificationNames, IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.LibraryHelper_PrepareTrainingInputFile_Preparing_training_input_file));

            var trainingTable = GetPrecursorTable(true);
            File.WriteAllLines(TrainingFilePath, trainingTable);
        }
        private IList<ArgumentAndValue> CommandArguments =>
            new List<ArgumentAndValue>
            {
                new ArgumentAndValue(@"jar", TextUtil.Quote(CarafeJarFilePath), TextUtil.HYPHEN),
                new ArgumentAndValue(@"ms", TextUtil.Quote(ExperimentDataFilePath), TextUtil.HYPHEN),
                new ArgumentAndValue(@"o", CarafeOutputLibraryDir(), TextUtil.HYPHEN),
                new ArgumentAndValue(@"c_ion_min", @"2", TextUtil.HYPHEN),
                new ArgumentAndValue(@"ez", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"fast", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"lf_frag_n_min", @"2", TextUtil.HYPHEN),
                new ArgumentAndValue(@"n_ion_min", @"2", TextUtil.HYPHEN),
                new ArgumentAndValue(@"na", @"0", TextUtil.HYPHEN),
                new ArgumentAndValue(@"nf", @"4", TextUtil.HYPHEN),
                new ArgumentAndValue(@"nm", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"seed", @"2000", TextUtil.HYPHEN),
                new ArgumentAndValue(@"skyline", string.Empty, TextUtil.HYPHEN),
                new ArgumentAndValue(@"tf", @"all", TextUtil.HYPHEN),
                new ArgumentAndValue(@"valid", string.Empty, TextUtil.HYPHEN)
            };

        public enum ToleranceUnits
        {
            ppm,
            da
        };

        public enum ModelTypes
        {
            general,
            phosphorylation
        };

        public enum DeviceTypes
        {
            gpu,
            cpu
        };

        public enum LibraryFormats
        {
            [Description("Skyline")] skyline,
            [Description("DIA-NN")] diann,
            [Description("EncyclopeDIA")] encyclopedia
        };

        //Carafe enzymes
        // 0:Non enzyme, 1:Trypsin (default), 2:Trypsin (no P rule), 3:Arg-C, 4:Arg-C (no P rule), 5:Arg-N, 6:Glu-C, 7:Lys-C.
        public enum SupportedEnzymeTypes
        {
            [Description("0:No enzyme")] NoEnzyme = 0,
            [Description("1:Trypsin (default)")] TrypsinDefault = 1,
            [Description("2:Trypsin(no P rule)")] TrypsinNoPRule = 2,
            [Description("3:Arg-C")] ArgC = 3,
            [Description("4:Arg-C(no P rule)")] ArgCNoPRule = 4,
            [Description("5:Arg-N")] ArgN = 5,
            [Description("6:Glu-C")] GluC = 6,
            [Description("7:Lys-C")] LysC = 7
        };
        public static string GetDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .FirstOrDefault() as DescriptionAttribute;
            return attribute?.Description ?? value.ToString();
        }

        // ReSharper disable LocalizableElement
        public static readonly ImmutableDictionary<string, AbstractDdaSearchEngine.Setting> DefaultDataParameters =
            new ImmutableDictionary<string, AbstractDdaSearchEngine.Setting>(
                new Dictionary<string, AbstractDdaSearchEngine.Setting>
                {
                    { ModelResources.CarafeTraining_fdr_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_fdr_long, 0.01, 0, 1) },
                    { ModelResources.CarafeTraining_ptm_site_prob_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_ptm_site_prob_long, 0.75, 0, 1) },
                    { ModelResources.CarafeTraining_ptm_site_qvalue_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_ptm_site_qvalue_long, 0.01, 0, 1) },
                    { ModelResources.CarafeTraining_fragment_ion_mass_tolerance_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_fragment_ion_mass_tolerance_long, 20, 0) },
                    { 
                        ModelResources.CarafeTraining_fragment_ion_mass_tolerance_units_short,
                        new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_fragment_ion_mass_tolerance_units_long, "ppm", Enum.GetNames(typeof(ToleranceUnits)))
                    },
                    { ModelResources.CarafeTraining_refine_peak_detection_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_refine_peak_detection_long, true) },
                    { ModelResources.CarafeTraining_RT_window_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_RT_window_long, 3, 0) },
                    { ModelResources.CarafeTraining_XIC_correlation_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_XIC_correlation_long, 0.80, 0, 1) }, 
                    { ModelResources.CarafeTraining_min_fragment_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_min_fragment_mz_long, 200.0, 0.0) }
                });

        public static readonly ImmutableDictionary<string, AbstractDdaSearchEngine.Setting> DefaultModelParameters =
            new ImmutableDictionary<string, AbstractDdaSearchEngine.Setting>(
                new Dictionary<string, AbstractDdaSearchEngine.Setting>
                {
                    { ModelResources.CarafeModel_model_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeModel_model_long, "general", Enum.GetNames(typeof(ModelTypes))) },
                    { ModelResources.CarafeModel_nce_short,  new AbstractDdaSearchEngine.Setting(ModelResources.CarafeModel_nce_long) },
                    { ModelResources.CarafeModel_instrument_short,  new AbstractDdaSearchEngine.Setting(ModelResources.CarafeModel_instrument_long) },
                    { ModelResources.CarafeModel_device_short,  new AbstractDdaSearchEngine.Setting(ModelResources.CarafeModel_device_long, "gpu", Enum.GetNames(typeof(DeviceTypes))) }
                });

        public static AbstractDdaSearchEngine.Setting FixedModSetting =
            new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_fixed_modification_long, @"1");

        public static AbstractDdaSearchEngine.Setting VarModSetting =
            new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_variable_modification_long, @"0");

        public static readonly ImmutableDictionary<string, AbstractDdaSearchEngine.Setting> DefaultLibraryParameters =
            new ImmutableDictionary<string, AbstractDdaSearchEngine.Setting>(
                new Dictionary<string, AbstractDdaSearchEngine.Setting>
                {
                    { ModelResources.CarafeLibrary_enzyme_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_enzyme_long, GetDescription( SupportedEnzymeTypes.TrypsinDefault ), Enum.GetValues(typeof(SupportedEnzymeTypes)).
                        Cast<SupportedEnzymeTypes>().Select(e => GetDescription(e))) },
                    { ModelResources.CarafeLibrary_missed_cleavage_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_missed_cleavage_long, 1, 0) },
    
                    { ModelResources.CarafeLibrary_fixed_modification_types_short, 
                        new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_fixed_modification_types_long, GetDescription( CarafeSupportedModifications.MODID_1 ), 
                            Enum.GetValues(typeof(CarafeSupportedModifications)).
                                Cast<CarafeSupportedModifications>().Select(e => GetDescription(e)), ModelResources.CarafeLibrary_fixed_modification_long, (s1, s2) =>
                            {
                                var input = "";

                                s2 = s2.Split(':')[0].Trim();

                                if (s1 == "0" || s2 == "0")
                                    return s2;
                                      
                                input = $"{s1},{s2}";

                                if (string.IsNullOrWhiteSpace(input))
                                {
                                    return s1;
                                }
                                return string.Join(",",
                                    input.Split(',')
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrEmpty(s) && int.TryParse(s, out _))
                                        .Select(s => int.Parse(s)) // Safe to parse after TryParse check
                                        .Distinct() // Removes duplicates, preserves order
                                        .Select(n => n.ToString()));
                            } )  },
                
                    { ModelResources.CarafeLibrary_fixed_modification_short, FixedModSetting },
                    { ModelResources.CarafeLibrary_variable_modification_types_short, 
                        new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_variable_modification_types_long, GetDescription( CarafeSupportedModifications.NONE ), 
                            Enum.GetValues(typeof(CarafeSupportedModifications)).
                                Cast<CarafeSupportedModifications>().Select(e => GetDescription(e)), ModelResources.CarafeLibrary_variable_modification_long, (s1,s2) =>
                            {
                                var input = "";

                                s2 = s2.Split(':')[0].Trim();

                                if (s1 == "0" || s2 == "0")
                                    return s2;

                                input = $"{s1},{s2}";

                                if (string.IsNullOrWhiteSpace(input))
                                {
                                    return s1;
                                }
                                return string.Join(",",
                                    input.Split(',')
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrEmpty(s) && int.TryParse(s, out _))
                                        .Select(s => int.Parse(s)) // Safe to parse after TryParse check
                                        .Distinct() // Removes duplicates, preserves order
                                        .Select(n => n.ToString()));
                            } )  },

                    { ModelResources.CarafeLibrary_variable_modification_short, VarModSetting },
                    { ModelResources.CarafeLibrary_max_variable_modification_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_variable_modification_long, 1, 0) },
                    { ModelResources.CarafeLibrary_clip_nterm_methionine_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_clip_nterm_methionine_long, false ) },
                    { ModelResources.CarafeLibrary_min_peptide_length_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_min_peptide_length_long, 7, 0 ) },
                    { ModelResources.CarafeLibrary_max_peptide_length_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_peptide_length_long, 35, 0 ) },
                    { ModelResources.CarafeLibrary_min_peptide_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_min_peptide_mz_long, 400.0, 0.0 ) },
                    { ModelResources.CarafeLibrary_max_peptide_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_peptide_mz_long, 1000.0, 0.0 ) },
                    { ModelResources.CarafeLibrary_min_peptide_charge_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_min_peptide_charge_long, 2, 1 ) },
                    { ModelResources.CarafeLibrary_max_peptide_charge_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_peptide_charge_long, 4, 1 ) },
                    { ModelResources.CarafeLibrary_min_fragment_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_min_fragment_mz_long, 200.0, 0.0 ) },
                    { ModelResources.CarafeLibrary_max_fragment_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_fragment_mz_long, 1800.0, 0.0 ) },
                    { ModelResources.CarafeLibrary_topN_fragments_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_topN_fragments_long, 20, 1 ) },
                    { ModelResources.CarafeLibrary_library_format_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_library_format_long, GetDescription( LibraryFormats.skyline), Enum.GetValues(typeof(LibraryFormats)).
                        Cast<LibraryFormats>().Select(e => GetDescription(e))) }

                });



        // ReSharper restore LocalizableElement


        /// <summary>
        /// List of UniMod Modifications available
        /// </summary>
        internal static readonly IList<ModificationType> UnimodModificationNames = populateUniModList();
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
            return CarafeOutputLibraryFilePath();
        }

        protected override IEnumerable<string> GetHeaderColumnNames(bool training)
        {
            throw new NotImplementedException();
        }

        protected override string GetTableRow(PeptideDocNode peptide, ModifiedSequence modifiedSequence, int charge, bool training,
            string modsBuilder, string modSitesBuilder)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// List of UniMod Modifications available
        /// </summary>
        public static readonly IList<ModificationType> MODIFICATION_NAMES = PopulateUniModList(null);

        protected override IList<ModificationType> ModificationTypes => MODIFICATION_NAMES;

        public static void CarafeDefaultSettings()
        {
            DataParameters = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            foreach (var kvp in DefaultDataParameters)
                DataParameters[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);

            ModelParameters = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            foreach (var kvp in DefaultModelParameters)
                ModelParameters[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);

            LibraryParameters = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            foreach (var kvp in DefaultLibraryParameters)
                LibraryParameters[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);
        } 

        public CarafeLibraryBuilder(
            string libName,
            string libOutPath,
            string pythonVirtualEnvironmentName,
            string pythonVirtualEnvironmentScriptsDir,
            string experimentDataFilePath,
            string experimentDataTuningFilePath,
            string dbInputFilePath,
            SrmDocument document,
            SrmDocument trainingDocument,
            bool diann_training, 
            IrtStandard irtStandard, out string testLibraryOutputPath, out string builderLibraryOutputPath) : base(document, irtStandard)
        {
            Document = document;
            TrainingDocument = trainingDocument;
            DbInputFilePath = dbInputFilePath;
            //PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataTuningFilePath = experimentDataTuningFilePath;
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);

            if (RootDir == null)
            {
                RootDir = Path.GetDirectoryName(libOutPath);
            }

            if (RootDir != null)
            {
                RootDir = Path.Combine(RootDir, libName);
                //Directory.CreateDirectory(RootDir);
                InitializeLibraryHelper(RootDir);
            }

            if (Document.DocumentHash != null || DbInputFilePath != null) InitializeLibraryHelper(RootDir);


            Directory.CreateDirectory(JavaDir);
            Directory.CreateDirectory(CarafeDir);

            _diann_training = diann_training;
            if (_diann_training)
                TuningFilePath = experimentDataTuningFilePath;
            else
                TuningFilePath = TrainingFilePath;

                Document = document;
            BuilderLibraryPath = CarafeOutputLibraryFilePath();
            TestLibraryPath = CarafeOutputLibraryFilePath(true);
            testLibraryOutputPath = TestLibraryPath;
            builderLibraryOutputPath = BuilderLibraryPath;

            if (CarafeLibraryBuilder.DataParameters == null || 
                CarafeLibraryBuilder.ModelParameters == null || 
                CarafeLibraryBuilder.LibraryParameters == null)
                CarafeLibraryBuilder.CarafeDefaultSettings();
        }

        public CarafeLibraryBuilder(
            string libName,
            string libOutPath,
            //string pythonVersion,
            string pythonVirtualEnvironmentName,
            string pythonVirtualEnvironmentScriptsDir,
            string proteinDatabaseFilePath,
            string experimentDataFilePath,
            string experimentDataTuningFilePath,
            SrmDocument document, 
            SrmDocument trainingDocument, 
            IDictionary<string, AbstractDdaSearchEngine.Setting> libraryParameters,
            IrtStandard irtStandard) : base(document, irtStandard)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            //PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
            //ProteinDatabaseFilePath = proteinDatabaseFilePath;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataTuningFilePath = experimentDataTuningFilePath;
            Document = document;
            TrainingDocument = trainingDocument;
            LibraryParameters = libraryParameters;
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(JavaDir);
            Directory.CreateDirectory(CarafeJavaDir);

        }

        private void InitializeLibraryHelper(string rootDir)
        {
            if (LibraryHelper == null)
            {
                LibraryHelper = new LibraryHelper(rootDir, CARAFE);
                RootDir = LibraryHelper.GetRootDir(rootDir, CARAFE);
                LibraryHelper.InitializeLibraryHelper(InputFilePath, TuningFilePath, ExperimentDataFilePath);
            }
        }
        public bool BuildLibrary(IProgressMonitor progress)
        {
            IProgressStatus progressStatus = new ProgressStatus();
            try
            {
                InitializeLibraryHelper(RootDir);
                
                Directory.CreateDirectory(RootDir);
                Directory.CreateDirectory(JavaDir);
                Directory.CreateDirectory(CarafeJavaDir);

                RunCarafe(progress, ref progressStatus);
                progress.UpdateProgress(progressStatus = progressStatus.Complete());
                LibraryHelper = null;
                return true;
            }
            catch (Exception exception)
            {
                progress.UpdateProgress(progressStatus.ChangeErrorException(exception));
                LibraryHelper = null;
                return false;
            }
        }

        private List<ArgumentAndValue> getUserSettingArgumentAndValues(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {

            var readyArgs = new List<ArgumentAndValue>();
            foreach (var arg in CommandArguments)
            {
                readyArgs.Add(new ArgumentAndValue(arg.Name, arg.Value, TextUtil.HYPHEN));
            }

            foreach (var dataParam in DataParameters)
            {
                if (dataParam.Key == ModelResources.CarafeTraining_fdr_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"fdr", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeTraining_ptm_site_prob_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"ptm_site_prob", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeTraining_ptm_site_qvalue_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"ptm_site_qvalue", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeTraining_fragment_ion_mass_tolerance_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"itol", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeTraining_fragment_ion_mass_tolerance_units_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"itolu", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeTraining_refine_peak_detection_short)
                {
                    if (!dataParam.Value.Value.ToString().IsNullOrEmpty() && (bool)dataParam.Value.Value)
                        readyArgs.Add(new ArgumentAndValue(@"rf", @"", TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeTraining_RT_window_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"rf_rt_win", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeTraining_XIC_correlation_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"cor", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeTraining_min_fragment_mz_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"min_mz", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
            }

            foreach (var dataParam in ModelParameters)
            {
                if (dataParam.Key == ModelResources.CarafeModel_model_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"mode", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeModel_nce_short)
                {
                    if (!dataParam.Value.Value.ToString().IsNullOrEmpty())
                        readyArgs.Add(new ArgumentAndValue(@"nce", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeModel_instrument_short)
                {
                    if (!dataParam.Value.Value.ToString().IsNullOrEmpty())
                        readyArgs.Add(new ArgumentAndValue(@"ms_instrument", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeModel_device_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"device", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
            }

            foreach (var dataParam in LibraryParameters)
            {
                if (dataParam.Key == ModelResources.CarafeLibrary_enzyme_short)
                {
                    if (!dataParam.Value.Value.ToString().IsNullOrEmpty())
                    {
                        string[] enz_name = dataParam.Value.Value.ToString().Split(':');

                        readyArgs.Add(new ArgumentAndValue(@"enzyme", enz_name[0], TextUtil.HYPHEN));
                    }

                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_missed_cleavage_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"miss_c", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_fixed_modification_short)
                {
                    if (!dataParam.Value.Value.ToString().IsNullOrEmpty())
                        readyArgs.Add(new ArgumentAndValue(@"fixMod", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_variable_modification_short)
                {
                    if (!dataParam.Value.Value.ToString().IsNullOrEmpty())
                        readyArgs.Add(new ArgumentAndValue(@"varMod", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                    else
                        readyArgs.Add(new ArgumentAndValue(@"varMod", @"0", TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_max_variable_modification_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"maxVar", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_clip_nterm_methionine_short)
                {
                    if (!dataParam.Value.Value.ToString().IsNullOrEmpty() && (bool)dataParam.Value.Value)
                        readyArgs.Add(new ArgumentAndValue(@"clip_n_m", @"", TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_min_peptide_length_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"minLength", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_max_peptide_length_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"maxLength", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_min_peptide_mz_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"min_pep_mz", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_max_peptide_mz_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"max_pep_mz", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_min_peptide_charge_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"min_pep_charge", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_max_peptide_charge_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"max_pep_charge", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_min_fragment_mz_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"lf_frag_mz_min", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_max_fragment_mz_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"lf_frag_mz_max", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_max_fragment_mz_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"lf_frag_mz_max", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_max_fragment_mz_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"lf_frag_mz_max", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }

                else if (dataParam.Key == ModelResources.CarafeLibrary_topN_fragments_short)
                {
                    readyArgs.Add(new ArgumentAndValue(@"lf_top_n_frag", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                }
                else if (dataParam.Key == ModelResources.CarafeLibrary_library_format_short)
                {
                    var format = GetDescription(LibraryFormats.skyline);
                    if (format != dataParam.Value.Value.ToString())
                        format += @$",{dataParam.Value.Value.ToString()}";
                    readyArgs.Add(new ArgumentAndValue(@"lf_type", format, TextUtil.HYPHEN));
                }
            }

            if (TrainingDocument != null)
            {
                if (!DbInputFilePath.IsNullOrEmpty())
                {
                    readyArgs.Add(new ArgumentAndValue(@"db", TextUtil.Quote(DbInputFilePath), TextUtil.HYPHEN));
                }
                else
                {
                    readyArgs.Add(new ArgumentAndValue(@"db", TextUtil.Quote(InputFilePath), TextUtil.HYPHEN));
                }

                readyArgs.Add(new ArgumentAndValue(@"i", TextUtil.Quote(TuningFilePath), TextUtil.HYPHEN));
                readyArgs.Add(new ArgumentAndValue(@"se", @"skyline", TextUtil.HYPHEN));

                LibraryHelper.PreparePrecursorInputFile(Document, progress, ref progressStatus, CARAFE, IrtStandard);
                LibraryHelper.PrepareTrainingInputFile(TrainingDocument, progress, ref progressStatus, CARAFE);
            }
            else if (_diann_training)
            {
                if (!DbInputFilePath.IsNullOrEmpty())
                {
                    readyArgs.Add(new ArgumentAndValue(@"db", TextUtil.Quote(DbInputFilePath), TextUtil.HYPHEN));
                    readyArgs.Add(new ArgumentAndValue(@"i", TextUtil.Quote(TuningFilePath), TextUtil.HYPHEN));
                    readyArgs.Add(new ArgumentAndValue(@"se", @"DIA-NN", TextUtil.HYPHEN));
                    LibraryHelper.PreparePrecursorInputFile(Document, progress, ref progressStatus, CARAFE, IrtStandard);
                }
                else
                {
                    readyArgs.Add(new ArgumentAndValue(@"db", TextUtil.Quote(InputFilePath), TextUtil.HYPHEN));
                    readyArgs.Add(new ArgumentAndValue(@"i", TextUtil.Quote(TuningFilePath), TextUtil.HYPHEN));
                    readyArgs.Add(new ArgumentAndValue(@"se", @"DIA-NN", TextUtil.HYPHEN));
                    LibraryHelper.PreparePrecursorInputFile(Document, progress, ref progressStatus, CARAFE, IrtStandard);
                }
            }
            else
            {
                readyArgs.Add(new ArgumentAndValue(@"db", TextUtil.Quote(DbInputFilePath), TextUtil.HYPHEN));
                readyArgs.Add(new ArgumentAndValue(@"i", TextUtil.Quote(TuningFilePath), TextUtil.HYPHEN));
                readyArgs.Add(new ArgumentAndValue(@"se", @"skyline", TextUtil.HYPHEN));
                LibraryHelper.PrepareTrainingInputFile(Document, progress, ref progressStatus, CARAFE);
            }

            return readyArgs;
        }

        private void RunCarafe(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            //progressStatus = progressStatus.ChangeSegments(0, 3);
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.CarafeLibraryBuilder_Running_Carafe));

            SetupJavaEnvironment(progress, ref progressStatus);

            var args = new StringBuilder();
            var segmentEndPercentages = new[] { 95, 99 };
            progressStatus = progressStatus.ChangeSegments(0, ImmutableList<int>.ValueOf(segmentEndPercentages));
            ExecuteCarafe(progress, ref progressStatus, getUserSettingArgumentAndValues(progress, ref progressStatus));
            progressStatus = progressStatus.NextSegment();
            ImportSpectralLibrary(progress, ref progressStatus);
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangePercentComplete(100));
        }

        private void SetupJavaEnvironment(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.CarafeLibraryBuilder_Setting_up_Java_environment)
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
                    throw new Exception(ModelResources.CarafeLibraryBuilder_Failed_to_download_Java_Software_Development_Kit_package, exception);

                // unzip java sdk package
                using var javaSdkZip = ZipFile.Read(JavaSdkDownloadPath);
                javaSdkZip.ExtractAll(JavaDir);
                SetJavaExecutablePath();
            }

            if (!isCarafeValid)
            {
                // clean carafe dir
                //CarafeJavaDirInfo.Delete(true);
                Directory.CreateDirectory(RootDir);
                Directory.CreateDirectory(CarafeJavaDir);


                // download carafe jar package
                using var webClient = new MultiFileAsynchronousDownloadClient(progress, 1);
                if (!webClient.DownloadFileAsync(CarafeJarZipDownloadUrl(), CarafeJarZipDownloadPath, out var exception))
                    throw new Exception(ModelResources.CarafeLibraryBuilder_Failed_to_download_Carafe_jar_package, exception);

                // unzip carafe jar package
                using var carafeJarZip = ZipFile.Read(CarafeJarZipDownloadPath);
                carafeJarZip.ExtractAll(CarafeJavaDir);
                PythonInstallerUtil.SignFile(CarafeJarFilePath);
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
            if (JavaExecutablePath.Any(char.IsWhiteSpace))
                JavaExecutablePath = TextUtil.Quote(JavaExecutablePath);

            var signatureValid = PythonInstallerUtil.IsSignatureValid(JavaExecutablePath, PythonInstallerUtil.GetFileHash(JavaExecutablePath));
            if (signatureValid != true)
            {
                return false;
            }

            return true;
        }

        private bool ValidateCarafe()
        {
            if (!File.Exists(CarafeJarFilePath))
            {
                return false;
            }
            var signatureValid = PythonInstallerUtil.IsSignatureValid(CarafeJarFilePath, PythonInstallerUtil.GetFileHash(CarafeJarFilePath));
            if (signatureValid != true)
                return false;
            return true;
        }

        private void SetJavaExecutablePath()
        {
            var dirs = Directory.GetDirectories(JavaDir);
            Assume.IsTrue(dirs.Length.Equals(1), string.Format(ModelResources.CarafeLibraryBuilder_Java_directory__0__has_more_than_one_Java_Software_Development_Kit, JavaDir));
            var javaSdkDir = dirs[0];
            JavaExecutablePath = Path.Combine(javaSdkDir, BIN, JAVA_EXECUTABLE);
            PythonInstallerUtil.SignFile(JavaExecutablePath);
        }

        private void ExecuteCarafe(IProgressMonitor progress, ref IProgressStatus progressStatus,
            IList<ArgumentAndValue> commandArgs)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.CarafeLibraryBuilder_Running_Carafe));

            progressStatus.ChangePercentComplete(0);

            // compose carafe cmd command arguments to build library
            var args = TextUtil.SpaceSeparate(
                TextUtil.Quote(PythonVirtualEnvironmentActivateScriptPath)
            );

            args += TextUtil.SpaceSeparate(CONDITIONAL_CMD_PROCEEDING_SYMBOL) + SPACE;

            args += TextUtil.SpaceSeparate(
                TextUtil.Quote(JavaExecutablePath)
            );

            args += SPACE + TextUtil.SpaceSeparate(
                commandArgs.Select(arg =>
                    arg.ToString())
            );

            var cmdBuilder = new StringBuilder();
            CancellationToken cancelToken = CancellationToken.None;

            cmdBuilder.Append(args).Append(SPACE);

            string batPath = Path.Combine(RootDir, @"runCarafe.bat");
            File.WriteAllText(batPath, cmdBuilder.ToString());

            // execute command
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo()
            {
                FileName = batPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };

            try
            {
                pr.SilenceStatusMessageUpdates =
                    true; // Use SimpleUserMessageWriter to write process output instead of ProgressStatus.ChangeMessage()
                pr.ExpectedOutputLinesCount = 1000;

                pr.EnableImmediateLog = true;
                pr.EnableRunningTimeMessage = true;
                pr.Run(psi, string.Empty, progress, ref progressStatus,
                    new SimpleUserMessageWriter(), ProcessPriorityClass.BelowNormal, true);
            }
            catch (Exception ex)
            {
                throw new Exception(ModelResources.Carafe_failed_to_complete, ex);
            }
        }

        private void ImportSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.CarafeLibraryBuilder_Importing_spectral_library));

            var source = CarafeOutputLibraryFilePath();
            var dest = LibrarySpec.FilePath;
 
            File.Copy(source, dest);
        }
    }
}

