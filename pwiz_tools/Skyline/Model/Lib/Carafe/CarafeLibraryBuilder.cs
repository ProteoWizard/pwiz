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
using System.Text.RegularExpressions;
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
using Enum = System.Enum;
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

        // Processing file names
        private const string OUTPUT_LIBRARY_FILE_NAME_BLIB = "SkylineAI_spectral_library.blib";
        private const string OUTPUT_LIBRARY_FILE_NAME_CSV = @"test_res_fine_tuned.csv";

        private const string SPACE = TextUtil.SPACE;
        private const string TAB = @"\t";

        //Processing folders
        private const string PREFIX_WORKDIR = "Carafe";
        private const string OUTPUT_SPECTRAL_LIBS = @"output_libs";

        // Column names for Carafe
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

        private static readonly IEnumerable<string> PrecursorTableColumnNames = 
            new[]
            {
                PRECURSOR, PEPTIDE, PRECURSOR_CHARGE, ISOTOPE_LABEL_TYPE, PRECURSOR_MZ, MODIFIED_SEQUENCE, PRECURSOR_EXPLICIT_COLLISION_ENERGY, 
                PRECURSOR_NOTE, LIBRARY_NAME, LIBRARY_TYPE, LIBRARY_PROBABILITY_SCORE, PEPTIDE_MODIFIED_SEQUENCE_UNIMOD_IDS
            };


        private static readonly IEnumerable<string> TrainingTableColumnNames =
            new[]
            {
                PRECURSOR, PEPTIDE, PRECURSOR_CHARGE, ISOTOPE_LABEL_TYPE, PRECURSOR_MZ, MODIFIED_SEQUENCE, PRECURSOR_EXPLICIT_COLLISION_ENERGY,
                PRECURSOR_NOTE, LIBRARY_NAME, LIBRARY_TYPE, LIBRARY_PROBABILITY_SCORE, PEPTIDE_MODIFIED_SEQUENCE_UNIMOD_IDS, 
                BEST_RT, MIN_RT, MAX_RT, IONMOB_MS1, APEX_SPECTRUM_ID, FILE_NAME, Q_VALUE
            };
        public ISkylineProcessRunnerWrapper SkylineProcessRunner { get; set; }

        private string PythonVirtualEnvironmentScriptsDir { get; }

        internal static List<ModificationType> MODEL_SUPPORTED_UNIMODS =
            new List<ModificationType>

            {
                {
                    new ModificationType(
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 4).ID.Value,
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 4).Name,
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 4).Formula,
                    PredictionSupport.FRAG_RT_ONLY)
                },
                {
                    new ModificationType(
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 21).ID.Value,
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 21).Name,
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 21).Formula,
                    PredictionSupport.FRAGMENTATION)
                },
                {
                    new ModificationType(
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 35).ID.Value,
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 35).Name,
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 35).Formula,
                    PredictionSupport.FRAG_RT_ONLY)
                },
                {
                    new ModificationType(
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 121).ID.Value,
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 121).Name,
                        UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == 121).Formula,
                    PredictionSupport.FRAGMENTATION)
                }

            };

        public LibrarySpec LibrarySpec { get; private set; }

        public static string PythonVersion => Settings.Default.PythonEmbeddableVersion;
        private string PythonVirtualEnvironmentName { get; }
        public string DbInputFilePath { get; private set; }
        internal string ExperimentDataFilePath { get; set; }
        internal string ExperimentDataTuningFilePath { get; set; }

        //internal string ProteinDatabaseFilePath;

        //private bool BuildLibraryForCurrentSkylineDocument => ProteinDatabaseFilePath.IsNullOrEmpty();
        private string PythonVirtualEnvironmentActivateScriptPath =>
            PythonInstallerUtil.GetPythonVirtualEnvironmentActivationScriptPath(PythonVersion,
                PythonVirtualEnvironmentName);



        private string JavaDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), JAVA);


        private static string JAVA_TMPDIR_PATH => Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, @"Temp");
        private string CarafeJavaDir => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CARAFE);
        private string UserDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private DirectoryInfo JavaDirInfo => new DirectoryInfo(JavaDir);
        private DirectoryInfo CarafeJavaDirInfo => new DirectoryInfo(CarafeJavaDir);
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

        private string CarafeOutputLibraryDir => Path.Combine(WorkDir, OUTPUT_LIBRARY);

        internal string CarafeOutputLibraryFilePath => Path.Combine(CarafeOutputLibraryDir, OUTPUT_LIBRARY_FILE_NAME_BLIB);

        internal string CarafeOutputLibraryCsvFilePath => Path.Combine(CarafeOutputLibraryDir, OUTPUT_LIBRARY_FILE_NAME_CSV);
        //        public string BuilderLibraryPath;

        private string CarafeJarFileDir => Path.Combine(CarafeJavaDir, CarafeFileBaseName);
        private string CarafeJarFilePath => Path.Combine(CarafeJarFileDir, CarafeJarFileName);

        private string InputFileName =>
            INPUT + TextUtil.UNDERSCORE +
            TextUtil.EXT_TSV; //Convert.ToBase64String(Encoding.ASCII.GetBytes(Document.DocumentHash)) + TextUtil.EXT_TSV;

        private string TrainingFileName => TRAIN + TextUtil.UNDERSCORE + TextUtil.EXT_TSV;

        public override string InputFilePath => Path.Combine(WorkDir, InputFileName);
  

        public string TuningFilePath { get; private set; }

        private bool _diann_training;

        public string BuilderLibraryPath { get; private set; }
        public string TestLibraryPath { get; private set; }

        public sealed override string TrainingFilePath => Path.Combine(WorkDir, TrainingFileName);

        public static IDictionary<string, AbstractDdaSearchEngine.Setting> DataParameters { get; private set; }
        public static IDictionary<string, AbstractDdaSearchEngine.Setting> ModelParameters { get; private set; }
        public static IDictionary<string, AbstractDdaSearchEngine.Setting> LibraryParameters { get; private set; }

        protected override LibraryBuilderModificationSupport libraryBuilderModificationSupport { get; }

        private IList<ArgumentAndValue> CommandArguments =>
            new List<ArgumentAndValue>
            {
                new ArgumentAndValue(@"jar", TextUtil.Quote(CarafeJarFilePath), TextUtil.HYPHEN),
                new ArgumentAndValue(@"ms", TextUtil.Quote(ExperimentDataFilePath), TextUtil.HYPHEN),
                new ArgumentAndValue(@"o", TextUtil.Quote(CarafeOutputLibraryDir), TextUtil.HYPHEN),
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
                    { ModelResources.CarafeTraining_fdr_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_fdr_short, 0.01, 0, 1, ModelResources.CarafeTraining_fdr_long) },
                    { ModelResources.CarafeTraining_ptm_site_prob_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_ptm_site_prob_short, 0.75, 0, 1, ModelResources.CarafeTraining_ptm_site_prob_long) },
                    { ModelResources.CarafeTraining_ptm_site_qvalue_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_ptm_site_qvalue_short, 0.01, 0, 1, ModelResources.CarafeTraining_ptm_site_qvalue_long) },
                    { ModelResources.CarafeTraining_fragment_ion_mass_tolerance_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_fragment_ion_mass_tolerance_short, 20, 0, int.MaxValue,ModelResources.CarafeTraining_fragment_ion_mass_tolerance_long) },
                    { 
                        ModelResources.CarafeTraining_fragment_ion_mass_tolerance_units_short,
                        new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_fragment_ion_mass_tolerance_units_short, "ppm", Enum.GetNames(typeof(ToleranceUnits)), ModelResources.CarafeTraining_fragment_ion_mass_tolerance_units_long)
                    },
                    { ModelResources.CarafeTraining_refine_peak_detection_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_refine_peak_detection_short, true, ModelResources.CarafeTraining_refine_peak_detection_long) },
                    { ModelResources.CarafeTraining_RT_window_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_RT_window_short, 3, 0, int.MaxValue, ModelResources.CarafeTraining_RT_window_long) },
                    { ModelResources.CarafeTraining_XIC_correlation_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_XIC_correlation_short, 0.80, 0, 1, ModelResources.CarafeTraining_XIC_correlation_long) }, 
                    { ModelResources.CarafeTraining_min_fragment_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeTraining_min_fragment_mz_short, 200.0, 0.0, double.MaxValue,ModelResources.CarafeTraining_min_fragment_mz_long) }
                });

        public static readonly ImmutableDictionary<string, AbstractDdaSearchEngine.Setting> DefaultModelParameters =
            new ImmutableDictionary<string, AbstractDdaSearchEngine.Setting>(
                new Dictionary<string, AbstractDdaSearchEngine.Setting>
                {
                    { ModelResources.CarafeModel_model_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeModel_model_short, "general", Enum.GetNames(typeof(ModelTypes)), ModelResources.CarafeModel_model_long) },
                    { ModelResources.CarafeModel_nce_short,  new AbstractDdaSearchEngine.Setting(ModelResources.CarafeModel_nce_short,ModelResources.CarafeModel_nce_long) },
                    { ModelResources.CarafeModel_instrument_short,  new AbstractDdaSearchEngine.Setting(ModelResources.CarafeModel_instrument_short, ModelResources.CarafeModel_instrument_long) },
                    { ModelResources.CarafeModel_device_short,  new AbstractDdaSearchEngine.Setting(ModelResources.CarafeModel_device_short, DeviceTypes.gpu.ToString(), Enum.GetNames(typeof(DeviceTypes)),ModelResources.CarafeModel_device_long) }
                });

        private static Func<string, string, string> BuildSelectedModString = (s1, s2) =>
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
        };

        private static Func<string, bool> ValidateSelectedMods = (value) =>
        {

            if (!Regex.IsMatch(value, @"^\d+\s*(,\s*\d+\s*)*$"))
                return false;
            foreach (var num in value.Split(',').Select(s => s.Trim()).ToArray())
            {
                if (!int.TryParse(num, out int number) ||
                    number > Enum.GetValues(typeof(CarafeSupportedModifications)).Length - 1 ||
                    number < 0)
                    return false;
            }

            return true;
        }; 
    
        public static readonly ImmutableDictionary<string, AbstractDdaSearchEngine.Setting> DefaultLibraryParameters =
            new ImmutableDictionary<string, AbstractDdaSearchEngine.Setting>(
                new Dictionary<string, AbstractDdaSearchEngine.Setting>
                {
                    { ModelResources.CarafeLibrary_enzyme_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_enzyme_short, GetDescription( SupportedEnzymeTypes.TrypsinDefault ), Enum.GetValues(typeof(SupportedEnzymeTypes)).
                        Cast<SupportedEnzymeTypes>().Select(e => GetDescription(e)), ModelResources.CarafeLibrary_enzyme_long) },
                    { ModelResources.CarafeLibrary_missed_cleavage_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_missed_cleavage_short, 1, 0, int.MaxValue, ModelResources.CarafeLibrary_missed_cleavage_long) },
                    { ModelResources.CarafeLibrary_fixed_modification_types_short, 
                        new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_fixed_modification_types_short, GetDescription( CarafeSupportedModifications.MODID_1 ), 
                            Enum.GetValues(typeof(CarafeSupportedModifications)).
                                Cast<CarafeSupportedModifications>().Select(e => GetDescription(e)), ModelResources.CarafeLibrary_fixed_modification_short, BuildSelectedModString, ModelResources.CarafeLibrary_fixed_modification_types_long)  },
                    { ModelResources.CarafeLibrary_fixed_modification_short,  new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_fixed_modification_short, @"1", ValidateSelectedMods, ModelResources.CarafeLibrary_fixed_modification_long)
                    },
                    { ModelResources.CarafeLibrary_variable_modification_types_short, 
                        new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_variable_modification_types_short, GetDescription( CarafeSupportedModifications.NONE ), 
                            Enum.GetValues(typeof(CarafeSupportedModifications)).
                                Cast<CarafeSupportedModifications>().Select(e => GetDescription(e)), ModelResources.CarafeLibrary_variable_modification_short, BuildSelectedModString, ModelResources.CarafeLibrary_variable_modification_types_long )  },
                    { ModelResources.CarafeLibrary_variable_modification_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_variable_modification_short, @"0", ValidateSelectedMods,ModelResources.CarafeLibrary_variable_modification_long) },
                    { ModelResources.CarafeLibrary_max_variable_modification_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_variable_modification_short, 1, 0, int.MaxValue,ModelResources.CarafeLibrary_max_variable_modification_long) },
                    { ModelResources.CarafeLibrary_clip_nterm_methionine_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_clip_nterm_methionine_short, false, ModelResources.CarafeLibrary_clip_nterm_methionine_long) },
                    { ModelResources.CarafeLibrary_min_peptide_length_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_min_peptide_length_short, 7, 0, int.MaxValue, ModelResources.CarafeLibrary_min_peptide_length_long ) },
                    { ModelResources.CarafeLibrary_max_peptide_length_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_peptide_length_short, 35, 0, int.MaxValue, ModelResources.CarafeLibrary_max_peptide_length_long ) },
                    { ModelResources.CarafeLibrary_min_peptide_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_min_peptide_mz_short, 400.0, 0.0, double.MaxValue, ModelResources.CarafeLibrary_max_peptide_mz_long ) },
                    { ModelResources.CarafeLibrary_max_peptide_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_peptide_mz_short, 1000.0, 0.0, double.MaxValue, ModelResources.CarafeLibrary_max_peptide_mz_long ) },
                    { ModelResources.CarafeLibrary_min_peptide_charge_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_min_peptide_charge_short, 2, 1, int.MaxValue, ModelResources.CarafeLibrary_min_peptide_charge_long ) },
                    { ModelResources.CarafeLibrary_max_peptide_charge_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_peptide_charge_short, 4, 1, int.MaxValue, ModelResources.CarafeLibrary_max_peptide_charge_long ) },
                    { ModelResources.CarafeLibrary_min_fragment_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_min_fragment_mz_short, 200.0, 0.0, double.MaxValue, ModelResources.CarafeLibrary_min_fragment_mz_long ) },
                    { ModelResources.CarafeLibrary_max_fragment_mz_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_max_fragment_mz_short, 1800.0, 0.0, double.MaxValue, ModelResources.CarafeLibrary_max_fragment_mz_long ) },
                    { ModelResources.CarafeLibrary_topN_fragments_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_topN_fragments_short, 20, 1, int.MaxValue, ModelResources.CarafeLibrary_topN_fragments_long ) },
                    { ModelResources.CarafeLibrary_library_format_short, new AbstractDdaSearchEngine.Setting(ModelResources.CarafeLibrary_library_format_short, GetDescription( LibraryFormats.skyline), Enum.GetValues(typeof(LibraryFormats)).
                        Cast<LibraryFormats>().Select(e => GetDescription(e)), ModelResources.CarafeLibrary_library_format_long) }

                });



        // ReSharper restore LocalizableElement

        public string ProductLibraryPath()
        {
            return CarafeOutputLibraryFilePath;
        }

        public string GetDataFileName()
        {
            if (ExperimentDataFilePath.IsNullOrEmpty())
                return string.Empty;
            return Path.GetFileName(ExperimentDataFilePath);
        }

        public override string GetWarning()
        {
            var (noMs2SupportWarningMods, noRtSupportWarningMods, noCcsSupportWarningMods) = GetWarningMods();
            if (noMs2SupportWarningMods.Count == 0 && noRtSupportWarningMods.Count == 0 && noCcsSupportWarningMods.Count == 0)
                return null;

            string warningModificationString;
            if (noMs2SupportWarningMods.Count > 0)
            {
                warningModificationString = string.Join(Environment.NewLine, noMs2SupportWarningMods.Select(w => w.Indent(1)));
                return string.Format(ModelResources.Alphapeptdeep_Warn_unknown_modification,
                    warningModificationString);
            }
            if (noRtSupportWarningMods.Count > 0)
            {
                warningModificationString = string.Join(Environment.NewLine, noRtSupportWarningMods.Select(w => w.Indent(1)));
                return string.Format(ModelResources.Carafe_Warn_limited_modification,
                    warningModificationString);
            }
            return String.Empty;
        }

        protected override IEnumerable<string> GetHeaderColumnNames(bool training)
        {
            if (!training)
                return PrecursorTableColumnNames;
            return TrainingTableColumnNames;
        }
        private static string LabelMz(TransitionGroup tranGroup, double precursorMz)
        {
            int? massShift = tranGroup.DecoyMassShift;
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            return string.Format(@"{0:F04}{1}", precursorMz - shift,
                Transition.GetDecoyText(massShift));
        }
        private static string LabelPrecursor(TransitionGroup tranGroup, double precursorMz,
            string resultsText)
        {
            return string.Format(@"{0}{1}{2}{3}", LabelMz(tranGroup, precursorMz),
                Transition.GetChargeIndicator(tranGroup.PrecursorAdduct),
                tranGroup.LabelTypeText, resultsText);
        }

        protected override string GetTableRow(PeptideDocNode peptide, ModifiedSequence modifiedSequence, int charge, bool training,
            string modsBuilder, string modSitesBuilder)
        {
            var docNode = peptide.TransitionGroups.FirstOrDefault(group => group.PrecursorCharge == charge);
            if (docNode == null)
            {
                return "";
            }
            var precursor = LabelPrecursor(docNode.TransitionGroup, docNode.PrecursorMz, string.Empty);
            var collisionEnergy = docNode.ExplicitValues.CollisionEnergy != null ? docNode.ExplicitValues.CollisionEnergy.ToString() : @"#N/A";
            var note = docNode.Annotations.Note != null ? docNode.Annotations.Note : @"#N/A";
            var libraryName = docNode.LibInfo != null && docNode.LibInfo.LibraryName != null ? docNode.LibInfo.LibraryName : @"#N/A";
            var libraryType = docNode.LibInfo != null && docNode.LibInfo.LibraryTypeName != null ? docNode.LibInfo.LibraryTypeName : @"#N/A";
            var libraryScore = docNode.LibInfo != null && docNode.LibInfo.Score != null ? docNode.LibInfo.Score.ToString() : @"#N/A";
            var unimodSequence = modifiedSequence.ToString();
            var best_rt = docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.RetentionTime.ToString();
            var min_rt = docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.StartRetentionTime.ToString();
            var max_rt = docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.EndRetentionTime.ToString();
            string ionmob_ms1 = docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.IonMobilityInfo?.IonMobilityMS1.HasValue == true ?
                docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.IonMobilityInfo.IonMobilityMS1.ToString() : @"#N/A";
            var apex_psm = @"unknown"; //docNode.GetSafeChromInfo(peptide.BestResult).FirstOrDefault()?.Identified
            var filename = GetDataFileName();

            if (!training)
                return new [] { 
                    precursor, modifiedSequence.GetUnmodifiedSequence(), charge.ToString(), docNode.LabelType.ToString(),
                    docNode.PrecursorMz.ToString(), unimodSequence, collisionEnergy, note, libraryName, libraryType, libraryScore, modifiedSequence.UnimodIds
                }.ToDsvLine(TextUtil.SEPARATOR_TSV);

            return new []
            {
                precursor, modifiedSequence.GetUnmodifiedSequence(), charge.ToString(), docNode.LabelType.ToString(),
                docNode.PrecursorMz.ToString(), unimodSequence, collisionEnergy,
                note, libraryName, libraryType, libraryScore, modifiedSequence.UnimodIds,
                best_rt, min_rt, max_rt, ionmob_ms1, apex_psm, filename, libraryScore
            }.ToDsvLine(TextUtil.SEPARATOR_TSV);
        }

        public static void CarafeDefaultDataSettings()
        {
            DataParameters = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            foreach (var kvp in DefaultDataParameters)
                DataParameters[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);
        }
        public static void CarafeDefaultModelSettings()
        {
            ModelParameters = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            foreach (var kvp in DefaultModelParameters)
                ModelParameters[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);
        }
        public static void CarafeDefaultLibrarySettings()
        {
            LibraryParameters = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
            foreach (var kvp in DefaultLibraryParameters)
                LibraryParameters[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);
        }
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
            IrtStandard irtStandard, out string testLibraryOutputPath, out string builderLibraryOutputPath) : base(document, trainingDocument, irtStandard)
        {
            string rootProcessingDir = Path.GetDirectoryName(libOutPath);
            if (string.IsNullOrEmpty(rootProcessingDir))
                throw new ArgumentException($@"CarafeLibraryBuilder libOutputPath {libOutPath} must be a full path.");

            rootProcessingDir = Path.Combine(rootProcessingDir, Path.GetFileNameWithoutExtension(libOutPath));

            EnsureWorkDir(rootProcessingDir, PREFIX_WORKDIR);

            DbInputFilePath = dbInputFilePath;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataTuningFilePath = experimentDataTuningFilePath;
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);

            Directory.CreateDirectory(JavaDir);
            Directory.CreateDirectory(CarafeJavaDir);

            _diann_training = diann_training;
            if (_diann_training)
                TuningFilePath = experimentDataTuningFilePath;
            else
                TuningFilePath = TrainingFilePath;

            BuilderLibraryPath = CarafeOutputLibraryFilePath;
            TestLibraryPath = CarafeOutputLibraryCsvFilePath;
            testLibraryOutputPath = TestLibraryPath;
            builderLibraryOutputPath = BuilderLibraryPath;

            if (CarafeLibraryBuilder.DataParameters == null || 
                CarafeLibraryBuilder.ModelParameters == null || 
                CarafeLibraryBuilder.LibraryParameters == null)
                CarafeLibraryBuilder.CarafeDefaultSettings();

            libraryBuilderModificationSupport = new LibraryBuilderModificationSupport(MODEL_SUPPORTED_UNIMODS);
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
            IrtStandard irtStandard) : base(document, trainingDocument, irtStandard)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            //PythonVersion = pythonVersion;
            PythonVirtualEnvironmentName = pythonVirtualEnvironmentName;
            PythonVirtualEnvironmentScriptsDir = pythonVirtualEnvironmentScriptsDir;
            //ProteinDatabaseFilePath = proteinDatabaseFilePath;
            ExperimentDataFilePath = experimentDataFilePath;
            ExperimentDataTuningFilePath = experimentDataTuningFilePath;
            LibraryParameters = libraryParameters;
            EnsureWorkDir(Path.GetDirectoryName(libOutPath), PREFIX_WORKDIR);
            Directory.CreateDirectory(JavaDir);
            Directory.CreateDirectory(CarafeJavaDir);
            libraryBuilderModificationSupport = new LibraryBuilderModificationSupport(MODEL_SUPPORTED_UNIMODS);
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            IProgressStatus progressStatus = new ProgressStatus();
            try
            {

                Directory.CreateDirectory(JavaDir);
                Directory.CreateDirectory(CarafeJavaDir);

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
                    if (PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONE)
                        readyArgs.Add(new ArgumentAndValue(@"device", dataParam.Value.Value.ToString(), TextUtil.HYPHEN));
                    else
                        readyArgs.Add(new ArgumentAndValue(@"device", DefaultTestDevice.ToString(), TextUtil.HYPHEN));
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

                PreparePrecursorInputFile(progress, ref progressStatus);
                PrepareTrainingInputFile(progress, ref progressStatus);
            }
            else if (_diann_training)
            {
                if (!DbInputFilePath.IsNullOrEmpty())
                {
                    readyArgs.Add(new ArgumentAndValue(@"db", TextUtil.Quote(DbInputFilePath), TextUtil.HYPHEN));
                    readyArgs.Add(new ArgumentAndValue(@"i", TextUtil.Quote(TuningFilePath), TextUtil.HYPHEN));
                    readyArgs.Add(new ArgumentAndValue(@"se", @"DIA-NN", TextUtil.HYPHEN));
                    PreparePrecursorInputFile(progress, ref progressStatus);
                }
                else
                {
                    readyArgs.Add(new ArgumentAndValue(@"db", TextUtil.Quote(InputFilePath), TextUtil.HYPHEN));
                    readyArgs.Add(new ArgumentAndValue(@"i", TextUtil.Quote(TuningFilePath), TextUtil.HYPHEN));
                    readyArgs.Add(new ArgumentAndValue(@"se", @"DIA-NN", TextUtil.HYPHEN));
                    PreparePrecursorInputFile(progress, ref progressStatus);
                }
            }
            else
            {
                readyArgs.Add(new ArgumentAndValue(@"db", TextUtil.Quote(DbInputFilePath), TextUtil.HYPHEN));
                readyArgs.Add(new ArgumentAndValue(@"i", TextUtil.Quote(TuningFilePath), TextUtil.HYPHEN));
                readyArgs.Add(new ArgumentAndValue(@"se", @"skyline", TextUtil.HYPHEN));
                PrepareTrainingInputFile(progress, ref progressStatus);
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
            //var args = @$"cd {WorkDir} && echo %PATH% > myPath && " + TextUtil.SpaceSeparate(
            //    TextUtil.Quote(PythonVirtualEnvironmentActivateScriptPath)
            //);

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

            string batPath = Path.Combine(WorkDir, @"runCarafe.bat");
            File.WriteAllText(batPath, cmdBuilder.ToString());

            // execute command
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo( batPath, "")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };
            psi.EnvironmentVariables[@"TMP"] = JAVA_TMPDIR_PATH;
            try
            {
                var filterStrings = new[]
                {
                    @"No ions matched!",
                    @"s/^x: [0-9]*$//",
                    @"s/^index_(start|end|apex): [0-9]*$//",
                    @"s/^[0-9\.]*$//" //replace strange number sequences
                };
                pr.SilenceStatusMessageUpdates =
                    true; // Use SimpleUserMessageWriter to write process output instead of ProgressStatus.ChangeMessage()
                pr.ExpectedOutputLinesCount = 550;

                pr.EnableRunningTimeMessage = true;
                pr.Run(psi, string.Empty, progress, ref progressStatus,
                    new FilteredUserMessageWriter(filterStrings), ProcessPriorityClass.BelowNormal, true);
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

            var source = CarafeOutputLibraryFilePath;
            var dest = LibrarySpec.FilePath;
 
            File.Copy(source, dest);
        }
    }
}

