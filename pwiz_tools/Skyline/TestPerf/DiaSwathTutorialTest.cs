/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    public class DiaSwathTestInfo
    {
        public class InstrumentSpecificValues
        {
            public string InstrumentTypeName;
            public string ZipFileName; // if set will override name calculated from InstrumentTypeName
            public string DiaFilesExtension;
            public string[] DiaFiles;
            public string[] SearchFiles;
            public bool HasRedundantLibrary = true; // only DIA-NN directly creates a non-redundant library
            public IrtStandard IrtStandard;
            public int IrtStandardCount;
            public bool HasAmbiguousMatches;
            public string IsolationSchemeName;
            public string IsolationSchemeFile;
            public char IsolationSchemeFileSeparator;
            public string ExamplePeptide;

            // Frozen progress values for consistent AllChromatogramsGraph screenshots
            public ImportProgressFreezeValues FrozenImportValues;
        }

        public class ImportProgressFreezeValues
        {
            public int TotalProgress;
            public string ElapsedTime;
            public float? GraphTime;
            public float? GraphIntensityMax;
            public Dictionary<string, int> FileProgress;
        }

        public class AnalysisValues
        {
            public bool IsWholeProteome;
            public bool KeepPrecursors;

            public string IrtFilterText;
            public string ScoreType;
            public double ScoreThreshold;
            public int? MinPeptidesPerProtein;
            public bool RemoveDuplicates;
            //public int[] TargetCounts;
            public PointF ChromatogramClickPoint;
            public double? FoldChangeProteinsMax;
            public double? FoldChangeProteinsMin;

            public string FastaPath =>
                IsWholeProteome
                    ? "DDA_search\\napedro_3mixed_human_yeast_ecoli_20140403_iRT_reverse.fasta"
                    : "DIA\\target_protein_sequences.fasta";
        }
        public class ExpectedValues
        {
            public int LibraryPeptideCount;
            public double IrtSlope;
            public double IrtIntercept;
            public int[] FinalTargetCounts;
            public double[][] MassErrorStats;
            public int[] DiffPeptideCounts;
            public int UnpolishedProteins;
            public int PolishedProteins;
            public double?[] ScoringModelCoefficients;
        }

        public InstrumentSpecificValues _instrumentValues;
        public AnalysisValues _analysisValues;
        public ExpectedValues _expectedValues;
        public string ExpectedValuesFilePath { get; private set; }
        public string[] DiaFiles => _instrumentValues.DiaFiles;
        public string InstrumentTypeName => _instrumentValues.InstrumentTypeName;
        public string RootName { get; set; }
        public string ZipFileName => _instrumentValues.ZipFileName ?? RootName;

        public string[] TestFilesZipPaths { get; set; }
        public bool[] TestFilesZipExtractHere { get; set; }
        public string[] TestFilesPersistent { get; set; }
        public string LinkPdf { get; set; }

        public void TestTtofData(bool fullSet)
        {
            ReadExpectedValues(nameof(TestTtofData), fullSet);
            SetInstrumentType(new InstrumentSpecificValues
            {
                InstrumentTypeName = "TTOF",
                DiaFilesExtension = DataSourceUtil.EXT_MZML,
                DiaFiles = new[]
                {
                    "collinsb_I180316_001_SW-A.mzML",
                    "collinsb_I180316_002_SW-B.mzML",
                    "collinsb_I180316_003_SW-A.mzML",
                    "collinsb_I180316_004_SW-B.mzML",
                    "collinsb_I180316_005_SW-A.mzML",
                    "collinsb_I180316_006_SW-B.mzML",
                },
                SearchFiles = new[]
                {
                    "DDA_search\\interact.pep.xml"
                },
                IrtStandard = IrtStandard.BIOGNOSYS_11,
                IrtStandardCount = 11,
                HasAmbiguousMatches = true,
                IsolationSchemeName = "ETH TTOF (64 variable)",
                IsolationSchemeFile = "64_variable_windows.csv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_CSV,
                ExamplePeptide = "LPQVEGTGGDVQPSQDLVR",
                FrozenImportValues = new ImportProgressFreezeValues
                {
                    TotalProgress = 20,
                    ElapsedTime = "00:00:22",
                    GraphTime = 39f,
                    GraphIntensityMax = 1.00e4f,
                    FileProgress = new Dictionary<string, int>
                    {
                        { "collinsb_I180316_001", 41 },
                        { "collinsb_I180316_002", 41 },
                        { "collinsb_I180316_003", 41 }
                    }
                }
            });

            if (fullSet)
            {
                _analysisValues = new AnalysisValues
                {
                    KeepPrecursors = false,
                    IsWholeProteome = true,
                    IrtFilterText = "iRT",
                    MinPeptidesPerProtein = 2,
                    RemoveDuplicates = true,
                    ChromatogramClickPoint = new PointF(34.18F, 108.0F),
                    //TargetCounts = new[] { 4700, 36628, 37960, 227760 },
                };
            }
            else
            {
                _analysisValues = new AnalysisValues
                {
                    KeepPrecursors = false,
                    IrtFilterText = "standard",
                    ChromatogramClickPoint = new PointF(34.18F, 108.0F),
                    //TargetCounts = new[] { 14, 279, 299, 1793 },
                };
            }

            _analysisValues.ScoreType = pwiz.BiblioSpec.Properties.Resources.BiblioSpecScoreType_DisplayName_PeptideProphet_confidence;
            _analysisValues.ScoreThreshold = 0.95;
        }

        public bool IsTtof => Equals("TTOF", _instrumentValues.InstrumentTypeName);

        public void TestQeData(bool fullSet)
        {
            ReadExpectedValues(nameof(TestQeData), fullSet);
            SetInstrumentType(new InstrumentSpecificValues
            {
                InstrumentTypeName = "QE",
                DiaFilesExtension = DataSourceUtil.EXT_MZML,
                DiaFiles = new[]
                {
                    "collinsb_X1803_171-A.mzML",
                    "collinsb_X1803_172-B.mzML",
                    "collinsb_X1803_173-A.mzML",
                    "collinsb_X1803_174-B.mzML",
                    "collinsb_X1803_175-A.mzML",
                    "collinsb_X1803_176-B.mzML",
                },
                SearchFiles = new[]
                {
                    "DDA_search\\interact.pep.xml"
                },
                IrtStandard = IrtStandard.BIOGNOSYS_11,
                IrtStandardCount = 11,
                HasAmbiguousMatches = false,
                IsolationSchemeName = "ETH QE (18 variable)",
                IsolationSchemeFile = "QE_DIA_18var.tsv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_TSV,
                ExamplePeptide = "LPQVEGTGGDVQPSQDLVR",
                FrozenImportValues = new ImportProgressFreezeValues
                {
                    TotalProgress = 21,
                    ElapsedTime = "00:00:12",
                    GraphTime = 35f,
                    GraphIntensityMax = 3e7f,
                    FileProgress = new Dictionary<string, int>
                    {
                        { "collinsb_X1803_171-A", 42 },
                        { "collinsb_X1803_172-B", 43 },
                        { "collinsb_X1803_173-A", 43 }
                    }
                }
            });

            if (fullSet)
            {
                _analysisValues = new AnalysisValues
                {
                    KeepPrecursors = false,
                    IsWholeProteome = true,
                    IrtFilterText = "iRT",
                    MinPeptidesPerProtein = 2,
                    RemoveDuplicates = true,
                    ChromatogramClickPoint = new PointF(32.05F, 268334.7F),
                    //TargetCounts = new[] { 3991, 30916, 33841, 203044 },
                };
            }
            else
            {
                _analysisValues = new AnalysisValues
                {
                    KeepPrecursors = false,
                    IrtFilterText = "standard",
                    ChromatogramClickPoint = new PointF(31.98F, 285741.3F),
                    //TargetCounts = new[] { 14, 271, 331, 1985 },
                    FoldChangeProteinsMax = 2,
                };
            }

            _analysisValues.ScoreType = pwiz.BiblioSpec.Properties.Resources.BiblioSpecScoreType_DisplayName_PeptideProphet_confidence;
            _analysisValues.ScoreThreshold = 0.95;
        }

        public void TestQeDataDiaNN(bool fullSet)
        {
            ReadExpectedValues(nameof(TestQeDataDiaNN), fullSet);
            SetInstrumentType(new InstrumentSpecificValues
            {
                InstrumentTypeName = "QE",
                DiaFilesExtension = DataSourceUtil.EXT_MZML,
                DiaFiles = new[]
                {
                    "collinsb_X1803_171-A.mzML",
                    "collinsb_X1803_172-B.mzML",
                    "collinsb_X1803_173-A.mzML",
                    "collinsb_X1803_174-B.mzML",
                    "collinsb_X1803_175-A.mzML",
                    "collinsb_X1803_176-B.mzML",
                },
                SearchFiles = new[]
                {
                    "DIA\\DIANN\\report-lib.parquet.skyline.speclib"
                },
                HasRedundantLibrary = false,
                IrtStandard = IrtStandard.AUTO,
                IrtStandardCount = 11,
                HasAmbiguousMatches = false,
                IsolationSchemeName = "ETH QE (18 variable)",
                IsolationSchemeFile = "QE_DIA_18var.tsv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_TSV,
                ExamplePeptide = "LPQVEGTGGDVQPSQDLVR"
            });

            // extract the DIA-*-DIANN zipfile inside the DIA-* directory so the DIA mzML files are picked up automatically
            TestFilesZipExtractHere = new bool[TestFilesZipPaths.Length].Append(true).ToArray();
            TestFilesZipPaths = TestFilesZipPaths
                .Append(string.Format(@"http://skyline.ms/tutorials/{0}-DIANN.zip", ZipFileName))
                .ToArray();

            if (fullSet)
            {
                _analysisValues = new AnalysisValues
                {
                    KeepPrecursors = false,
                    IsWholeProteome = true,
                    IrtFilterText = "iRT",
                    MinPeptidesPerProtein = 2,
                    RemoveDuplicates = true,
                    ChromatogramClickPoint = new PointF(32.05F, 268334.7F),
                    //TargetCounts = new[] { 3991, 30916, 33841, 203044 },
                };
            }
            else
            {
                _analysisValues = new AnalysisValues
                {
                    KeepPrecursors = false,
                    IrtFilterText = "iRT",
                    ChromatogramClickPoint = new PointF(31.98F, 285741.3F),
                    //TargetCounts = new[] { 14, 271, 331, 1985 },
                    FoldChangeProteinsMax = 2,
                };
            }

            _analysisValues.ScoreType = pwiz.BiblioSpec.Properties.Resources.BiblioSpecScoreType_DisplayName_q_value;
            _analysisValues.ScoreThreshold = 0.01;
        }

        public void TestPasefData(bool fullSet)
        {
            ReadExpectedValues(nameof(TestPasefData), fullSet);

            // Freeze values are the same for both full and small PASEF data sets
            var frozenImportValues = new ImportProgressFreezeValues
            {
                TotalProgress = 20,
                ElapsedTime = "00:00:11",
                GraphTime = 11.5f,
                GraphIntensityMax =1.8e5f,
                FileProgress = new Dictionary<string, int>
                {
                    { "A210331_bcc_1180", 44 },
                    { "A210331_bcc_1181", 44 },
                    { "A210331_bcc_1182", 42 }
                }
            };

            if (fullSet)
            {
                _analysisValues = new AnalysisValues
                {
                    KeepPrecursors = false,
                    IsWholeProteome = true,
                    IrtFilterText = Resources.IrtDb_MakeDocumentXml_iRT_standards,
                    MinPeptidesPerProtein = 2,
                    RemoveDuplicates = true,
                    ChromatogramClickPoint = new PointF(10.79F, 3800.0F),
                    //TargetCounts = new[] { 4937, 37152, 38716, 232296 },
                };

                SetInstrumentType(new InstrumentSpecificValues
                {
                    InstrumentTypeName = "PASEF",
                    ZipFileName = "DIA-PASEF-full",
                    DiaFilesExtension = DataSourceUtil.EXT_AGILENT_BRUKER_RAW,
                    DiaFiles = new[]
                    {
                        "A210331_bcc_1180_lfqbA_17min_dia_200ng.d",
                        "A210331_bcc_1181_lfqbB_17min_dia_200ng.d",
                        "A210331_bcc_1182_lfqbA_17min_dia_200ng.d",
                        "A210331_bcc_1183_lfqbB_17min_dia_200ng.d",
                        "A210331_bcc_1184_lfqbA_17min_dia_200ng.d",
                        "A210331_bcc_1185_lfqbB_17min_dia_200ng.d",
                    },
                    SearchFiles = new[]
                    {
                        "DDA_search\\out\\interact.pep.xml",
                    },
                    IrtStandard = IrtStandard.AUTO,
                    IrtStandardCount = 15,
                    HasAmbiguousMatches = false,
                    IsolationSchemeName = "diaPASEF (24 fixed)",
                    IsolationSchemeFile = "diaPASEF_24fix.csv",
                    IsolationSchemeFileSeparator = TextUtil.SEPARATOR_TSV,
                    ExamplePeptide = "LPQVEGTGGDVQPSQDLVR",
                    FrozenImportValues = frozenImportValues
                });
            }
            else
            {
                _analysisValues = new AnalysisValues
                {
                    KeepPrecursors = false,
                    IrtFilterText = Resources.IrtDb_MakeDocumentXml_iRT_standards,
                    ChromatogramClickPoint = new PointF(10.79F, 3800.0F),
                    //TargetCounts = new[] { 14, 75, 83, 498 },
                };

                SetInstrumentType(new InstrumentSpecificValues
                {
                    InstrumentTypeName = "PASEF",
                    ZipFileName = "DIA-PASEF-small",
                    DiaFilesExtension = DataSourceUtil.EXT_AGILENT_BRUKER_RAW,
                    DiaFiles = new[]
                    {
                        "A210331_bcc_1180_lfqbA_17min_dia_200ng.d",
                        "A210331_bcc_1181_lfqbB_17min_dia_200ng.d",
                        "A210331_bcc_1182_lfqbA_17min_dia_200ng.d",
                        "A210331_bcc_1183_lfqbB_17min_dia_200ng.d",
                        "A210331_bcc_1184_lfqbA_17min_dia_200ng.d",
                        "A210331_bcc_1185_lfqbB_17min_dia_200ng.d",
                    },
                    SearchFiles = new[]
                    {
                        "DDA_search\\out\\interact.pep.xml",
                        //"c:\\test\\issues\\skyline-cli-import\\DIA-PASEF-small\\yufe_fragpipe_dda\\interact-A210331_bcc_lfqbA_17min_dda_200ng_1171.pep.xml",
                        //"C:\\test\\issues\\skyline-cli-import\\DIA-PASEF-small\\yufe_fragpipe_dda\\interact-A210331_bcc_lfqbB_17min_dda_200ng_1172.pep.xml"
                    },
                    IrtStandard = IrtStandard.AUTO,
                    IrtStandardCount = 15,
                    HasAmbiguousMatches = false,
                    IsolationSchemeName = "diaPASEF (24 fixed)",
                    IsolationSchemeFile = "diaPASEF_24fix.csv",
                    IsolationSchemeFileSeparator = TextUtil.SEPARATOR_TSV,
                    ExamplePeptide = "LPQVEGTGGDVQPSQDLVR",
                    FrozenImportValues = frozenImportValues
                });
            }

            _analysisValues.ScoreType = pwiz.BiblioSpec.Properties.Resources.BiblioSpecScoreType_DisplayName_PeptideProphet_confidence;
            _analysisValues.ScoreThreshold = 0.95;
        }
        private void ReadExpectedValues(string variant, bool fullSet)
        {
            ExpectedValuesFilePath = Path.Combine(ExtensionTestContext.GetProjectDirectory(
                    @"TestPerf\DiaSwathTutorialTest.data"),
                variant + (fullSet ? "_full" : "") + ".json");
            Assert.IsNull(_expectedValues);
            if (File.Exists(ExpectedValuesFilePath))
            {
                using var streamReader = File.OpenText(ExpectedValuesFilePath);
                using var jsonReader = new JsonTextReader(streamReader);
                _expectedValues = JsonSerializer.Create().Deserialize<ExpectedValues>(jsonReader);
            }
            else
            {
                _expectedValues = new ExpectedValues();
            }
        }

        public void SaveExpectedValues()
        {
            Assert.IsNotNull(_expectedValues);
            Assert.IsNotNull(ExpectedValuesFilePath);
            using var streamWriter = new StreamWriter(ExpectedValuesFilePath);
            using var jsonTextWriter = new JsonTextWriter(streamWriter);
            JsonSerializer.Create(new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            }).Serialize(jsonTextWriter, _expectedValues);
        }

        public bool IsPasef => Equals("PASEF", _instrumentValues.InstrumentTypeName);
        public bool IsDiaNN => !_instrumentValues.HasRedundantLibrary;

        public void SetInstrumentType(InstrumentSpecificValues instrumentValues)
        {
            _instrumentValues = instrumentValues;

            RootName = "DIA-" + InstrumentTypeName;

            const string pdfFormat = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/{0}-22_2.pdf";
            // const string pdfFormat = "file:///C:/proj/branches/work/pwiz_tools/Skyline/Documentation/Tutorials/{0}-22_2.pdf";
            LinkPdf = string.Format(pdfFormat, RootName);

            TestFilesZipPaths = new[]
            {
                string.Format(@"http://skyline.ms/tutorials/{0}.zip", ZipFileName),
                string.Format(@"TestPerf\DiaSwath{0}Views.zip", InstrumentTypeName)
            };

            TestFilesPersistent = new[] { Path.Combine(ZipFileName, "DDA_search"), Path.Combine(ZipFileName, "DIA") };
        }

        public void ValidateTargets(bool recordMode, ref int[] expected, int proteinCount, int peptideCount, int precursorCount, int transitionCount)
        {
            var targetCountsActual = new[] { proteinCount, peptideCount, precursorCount, transitionCount };
            if (recordMode)
            {
                expected = targetCountsActual;
                return;
            }
            AssertEx.AreEqual(string.Join(",", expected), string.Join(",", targetCountsActual));
            }
        }

    /// <summary>
    /// Verify DIA/SWATH tutorial operation
    /// </summary>
    [TestClass]
    public class DiaSwathTutorialTest : AbstractFunctionalTestEx
    {
        private DiaSwathTestInfo _testInfo = new DiaSwathTestInfo();
        private string[] DiaFiles => _testInfo._instrumentValues.DiaFiles;
        private string InstrumentTypeName => _testInfo._instrumentValues.InstrumentTypeName;
        private string RootName => _testInfo.RootName;
        private string ZipFileName => _testInfo._instrumentValues.ZipFileName ?? RootName;
        private bool IsTtof => _testInfo.IsTtof;
        private bool IsPasef => _testInfo.IsPasef;
        private bool IsDiaNN => _testInfo.IsDiaNN;
        private DiaSwathTestInfo.AnalysisValues _analysisValues => _testInfo._analysisValues;
        private DiaSwathTestInfo.InstrumentSpecificValues _instrumentValues => _testInfo._instrumentValues;
        private DiaSwathTestInfo.ExpectedValues _expectedValues => _testInfo._expectedValues;

        [TestMethod]
        public void TestDiaTtofTutorial()
        {
            _testInfo.TestTtofData(false);
            RunTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] // Times out on slower worker VMs
        public void TestDiaTtofFullSearchTutorialExtra()
        {
            _testInfo.TestTtofData(true);
            if (!IsCoverShotMode)
                RunTest();
        }


        [TestMethod]
        public void TestDiaQeTutorial()
        {
            _testInfo.TestQeData(false);
            RunTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] // Times out on slower VMs
        public void TestDiaQeFullSearchTutorialExtra()
        {
            _testInfo.TestQeData(true);
            if (!IsCoverShotMode)
                RunTest();
        }


        [TestMethod]
        public void TestDiaQeDiaNnTutorialDraft()
        {
            _testInfo.TestQeDataDiaNN(false);
            RunTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] // Times out on slower VMs
        public void TestDiaQeDiaNnFullSearchTutorialExtra()
        {
            _testInfo.TestQeDataDiaNN(true);
            if (!IsCoverShotMode)
                RunTest();
        }


        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)] // Bruker wants exclusive read access to raw data
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaPasefTutorial()
        {
            // Not yet translated
            if (IsTranslationRequired)
                return;

            _testInfo.TestPasefData(false);
            RunTest();
        }

        [TestMethod,
         NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING), // Bruker wants exclusive read access to raw data
         NoNightlyTesting(TestExclusionReason.EXCESSIVE_TIME)] // Skip during Nightly
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaPasefFullDatasetExtra()
        {
            _testInfo.TestPasefData(true);
            if (!IsCoverShotMode)
                RunTest();
        }

        private void RunTest()
        {
            TestFilesZipPaths = _testInfo.TestFilesZipPaths;
            TestFilesZipExtractHere = _testInfo.TestFilesZipExtractHere;
            TestFilesPersistent = _testInfo.TestFilesPersistent;
            LinkPdf = _testInfo.LinkPdf;

//            IsPauseForScreenShots = true;
//            RunPerfTests = true;
//            IsCoverShotMode = true;
            CoverShotName = IsTtof ? "DIA-TTOF" : RootName;

            RunFunctionalTest();
        }

        private string DataPath { get { return TestFilesDirs.Last().PersistentFilesDir; } }

        private PropertyPath _resultProperty = PropertyPath.Root.Property("FoldChangeResult");
        private PropertyPath _proteinProperty = PropertyPath.Root.Property("Protein");

        private string GetTestPath(string path)
        {
            foreach (var dir in TestFilesDirs)
            {
                string possiblePath = dir.GetTestPath(Path.Combine(ZipFileName, path));
                if (File.Exists(possiblePath) || Directory.Exists(possiblePath))
                    return possiblePath;
                possiblePath = dir.GetTestPath(path);
                if (File.Exists(possiblePath) || Directory.Exists(possiblePath))
                    return possiblePath;
            }

            throw new ArgumentException($"{path} does not exist in any of the TestFilesDirs");
        }

        /// <summary>
        /// Change to true to write coefficient arrays.
        /// </summary>
        protected override bool IsRecordMode => false;

        protected override void DoTest()
        {
            Assert.IsTrue(IsRecordMode || File.Exists(_testInfo.ExpectedValuesFilePath),
                "Expected values file {0} does not exist.", _testInfo.ExpectedValuesFilePath);
            // Clean-up before running the test
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                d => d.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SetIntegrateAll(true));
            Settings.Default.CompactFormatOption = CompactFormatOption.NEVER.Name;

            SrmDocument doc = SkylineWindow.Document;

            string documentBaseName = "DIA-" + InstrumentTypeName + "-tutorial";
            string documentFile = TestFilesDirs[0].GetTestPath(documentBaseName + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentFile));

            //var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            //RunUI(() => peptideSettingsDlg.Prediction.MeasuredRTWindow = 3);
            //OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library empty page");

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            string[] searchFiles = _instrumentValues.SearchFiles.Select(o => GetTestPath(o)).ToArray();
            foreach (var searchFile in searchFiles)
                Assert.IsTrue(File.Exists(searchFile), string.Format("File {0} does not exist.", searchFile));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = _instrumentValues.IrtStandard;
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
            });
            WaitForConditionUI(() =>
                Equals(_analysisValues.ScoreType, importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Files.FirstOrDefault()?.ScoreType?.ToString()));
            RunUI(() =>
            {
                // Check default settings shown in the tutorial
                Assert.AreEqual(_analysisValues.ScoreThreshold, importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Files.First().ScoreThreshold);
                Assert.IsFalse(importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUIForScreenShot(() =>
            {
                var cols = importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Columns;
                cols[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                cols[0].Width = 175;    // just "interact.pep.xml"
                cols[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; // To show the full PeptideProphet confidence
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library populated page");

            AddIrtPeptidesDlg addIrtPeptidesDlg;
            AddIrtStandardsDlg addIrtStandardsDlg = null;
            if (_instrumentValues.IrtStandard.IsAuto ||
                _instrumentValues.IrtStandard.Equals(IrtStandard.CIRT_SHORT) ||
                _instrumentValues.IrtStandard.Equals(IrtStandard.CIRT))
            {
                addIrtStandardsDlg = ShowDialog<AddIrtStandardsDlg>(() => importPeptideSearchDlg.ClickNextButton());
                RunUI(() => addIrtStandardsDlg.StandardCount = _instrumentValues.IrtStandardCount);
                PauseForScreenShot<AddIrtStandardsDlg>("Add Standard Peptides - Select number of CiRT peptides");
                addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(addIrtStandardsDlg.OkDialog);
            }
            else
                addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(() => importPeptideSearchDlg.ClickNextButton());

            RunUI(() =>
            {
                // Check values shown in the tutorial
                Assert.AreEqual(1, addIrtPeptidesDlg.RunsConvertedCount);
                var row = addIrtPeptidesDlg.GetRow(0);
                var regressionRefined = addIrtPeptidesDlg.GetRegressionRefined(0);
                Assert.IsNotNull(regressionRefined);
                var slope = Math.Round(regressionRefined.Slope, 3);
                var intercept = Math.Round(regressionRefined.Intercept, 3);
                Assert.AreEqual(_instrumentValues.IrtStandardCount, row.Cells[1].Value);
                if (IsRecordMode)
                {
                    _expectedValues.LibraryPeptideCount = addIrtPeptidesDlg.PeptidesCount;
                    _expectedValues.IrtSlope = slope;
                    _expectedValues.IrtIntercept = intercept;
                }
                else
                {
                    Assert.AreEqual(_expectedValues.LibraryPeptideCount, addIrtPeptidesDlg.PeptidesCount);
                    Assert.AreEqual(_expectedValues.IrtSlope, slope);
                    Assert.AreEqual(_expectedValues.IrtIntercept, intercept);
                }

                Assert.AreEqual(Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_Success, row.Cells[4].Value);
                addIrtPeptidesDlg.Width = 650;
            });
            PauseForScreenShot<AddIrtPeptidesDlg>("Add iRT peptides form");

            var irtGraph = ShowDialog<GraphRegression>(() => addIrtPeptidesDlg.ShowRegression(0));
            PauseForScreenShot<GraphRegression>("iRT regression graph");

            OkDialog(irtGraph, irtGraph.CloseDialog);
            var recalibrateMessage = ShowDialog<MultiButtonMsgDlg>(addIrtPeptidesDlg.OkDialog);
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(Resources.LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_,
                Resources.LibraryGridViewDriver_AddToLibrary_This_can_improve_retention_time_alignment_under_stable_chromatographic_conditions_), recalibrateMessage.Message));
            if (!_instrumentValues.HasAmbiguousMatches)
            {
                OkDialog(recalibrateMessage, recalibrateMessage.ClickNo);
            }
            else
            {
                var ambiguousDlg = ShowDialog<MessageDlg>(recalibrateMessage.ClickNo);
                RunUI(() => AssertEx.Contains(ambiguousDlg.Message,
                    Resources.BiblioSpecLiteBuilder_AmbiguousMatches_The_library_built_successfully__Spectra_matching_the_following_peptides_had_multiple_ambiguous_peptide_matches_and_were_excluded_));
                OkDialog(ambiguousDlg, ambiguousDlg.OkDialog);
            }
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);

            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(documentFile);
            Assert.IsTrue(File.Exists(docLibPath));
            if (_instrumentValues.HasRedundantLibrary)
            {
                string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
                Assert.IsTrue(File.Exists(redundantDocLibPath));
            }
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.HasDocumentLibrary);

            var importResults = importPeptideSearchDlg.ImportResultsControl;
            string diaDir = GetTestPath("DIA");
            if (IsPauseForScreenShots)
            {
                // delete -diaumpire files so they don't show up for screenshots
                foreach (var file in Directory.GetFiles(diaDir, "*-diaumpire.*"))
                    FileEx.SafeDelete(file);
            }

            if (!IsDiaNN)
            {
                // Get rid of the DIANN directory for the screenshot if it is there
                if (IsPauseForScreenShots && Directory.Exists(Path.Combine(diaDir, "DIANN")))
                    Directory.Delete(Path.Combine(diaDir, "DIANN"), true);

                var importResultsDia = importResults as ImportResultsDIAControl;
                Assert.IsNotNull(importResults);
                var openDataFiles = ShowDialog<OpenDataSourceDialog>(() => importResultsDia.Browse(diaDir));
                RunUI(() =>
                {
                    openDataFiles.SelectAllFileType(_instrumentValues.DiaFilesExtension);
                    foreach (var selectedFile in openDataFiles.SelectedFiles)
                        Assert.IsTrue(DiaFiles.Contains(selectedFile));
                });
                PauseForScreenShot<OpenDataSourceDialog>("Results files form");
                OkDialog(openDataFiles, openDataFiles.Open);
            }

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() =>
            {
                foreach (var resultFileName in importResults.FoundResultsFiles)
                    Assert.IsTrue(DiaFiles.Contains(Path.GetFileName(resultFileName.Path)));
            });
            PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>("Extract chromatograms page with files");

            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.match_modifications_page);

            // "Add Modifications" page
            RunUI(() =>
            {
                const string modOxidation = "Oxidation (M)";
                // Define expected matched/unmatched modifications
                var expectedMatched = !IsPasef && !IsDiaNN ? new[] { modOxidation } : Array.Empty<string>();
                // Verify matched/unmatched modifications
                AssertEx.AreEqualDeep(expectedMatched, importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.ToArray());
                Assert.IsFalse(importPeptideSearchDlg.MatchModificationsControl.UnmatchedModifications.Any());
            });
            // TODO: Include this screenshot in the tutorial
            // PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Modifications page - currently no screenshot", screenshotPage);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            RunUI(() =>
            {
                importPeptideSearchDlg.TransitionSettingsControl.ExclusionUseDIAWindow = true;
                importPeptideSearchDlg.TransitionSettingsControl.PeptidePrecursorCharges = new[]
                {
                    Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED, Adduct.QUADRUPLY_PROTONATED
                };
                // Default is to have precursors
                if (_analysisValues.KeepPrecursors)
                {
                    AssertEx.AreEqualDeep(new[] {IonType.y, IonType.b, IonType.precursor},
                        importPeptideSearchDlg.TransitionSettingsControl.PeptideIonTypes);
                }
                else
                {
                    importPeptideSearchDlg.TransitionSettingsControl.PeptideIonTypes = new[]
                    {
                        IonType.y, IonType.b    // Removes precursor
                    };
                }
                // Verify other values shown in the tutorial
                Assert.AreEqual(50, importPeptideSearchDlg.TransitionSettingsControl.MinIonMz);
                Assert.AreEqual(2000, importPeptideSearchDlg.TransitionSettingsControl.MaxIonMz);
                Assert.AreEqual(TransitionFilter.StartFragmentFinder.ION_3.Label, importPeptideSearchDlg.TransitionSettingsControl.IonRangeFrom);
                Assert.AreEqual(TransitionFilter.EndFragmentFinder.LAST_ION.Label, importPeptideSearchDlg.TransitionSettingsControl.IonRangeTo);
                Assert.AreEqual(6, importPeptideSearchDlg.TransitionSettingsControl.IonCount);
                Assert.AreEqual(6, importPeptideSearchDlg.TransitionSettingsControl.MinIonCount);
                Assert.AreEqual(0.05, importPeptideSearchDlg.TransitionSettingsControl.IonMatchMzTolerance.Value);
                Assert.AreEqual(MzTolerance.Units.mz, importPeptideSearchDlg.TransitionSettingsControl.IonMatchMzTolerance.Unit);
                // CONSIDER: Not that easy to validate 1, 2 in ion charges.
            });
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("Transition settings");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Configure Full-Scan Settings" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                if (_analysisValues.KeepPrecursors)
                    importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 20;
                importPeptideSearchDlg.FullScanSettingsControl.ProductRes = 20;

                Assert.AreEqual(importPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent, FullScanPrecursorIsotopes.None);
                Assert.AreEqual(FullScanMassAnalyzerType.centroided, importPeptideSearchDlg.FullScanSettingsControl.ProductMassAnalyzer);
                Assert.AreEqual(RetentionTimeFilterType.scheduling_windows, importPeptideSearchDlg.FullScanSettingsControl.RetentionTimeFilterType);
                Assert.AreEqual(5, importPeptideSearchDlg.FullScanSettingsControl.TimeAroundPrediction);
            });

            var isolationScheme =
                ShowDialog<EditIsolationSchemeDlg>(importPeptideSearchDlg.FullScanSettingsControl.AddIsolationScheme);
            RunUI(() =>
            {
                isolationScheme.IsolationSchemeName = _instrumentValues.IsolationSchemeName;
                isolationScheme.UseResults = false;
            });
            RunDlg<OpenDataSourceDialog>(isolationScheme.ImportRanges, importRangesDlg =>
            {
                importRangesDlg.CurrentDirectory = new MsDataFilePath(diaDir);
                importRangesDlg.SelectFile(DiaFiles[0]);
                importRangesDlg.Open();
            });
            string schemePath = Path.Combine("DIA", _instrumentValues.IsolationSchemeFile);
            var schemeLines = File.ReadAllLines(GetTestPath(schemePath));
            string[][] windowFields = schemeLines.Select(l =>
                TextUtil.ParseDsvFields(l, _instrumentValues.IsolationSchemeFileSeparator)).ToArray();
            WaitForConditionUI(() => isolationScheme.GetIsolationWindows().Count == schemeLines.Length);

            RunUI(() =>
            {
                bool hasMargin = windowFields[0].Length == 3;
                if (hasMargin)
                    Assert.IsTrue(isolationScheme.SpecifyMargin);
                else
                    Assert.IsFalse(isolationScheme.SpecifyMargin);
                int schemeRow = 0;
                foreach (var isolationWindow in isolationScheme.GetIsolationWindows())
                {
                    var fields = windowFields[schemeRow++];
                    Assert.AreEqual(double.Parse(fields[0], CultureInfo.InvariantCulture), isolationWindow.MethodStart, 0.01);
                    Assert.AreEqual(double.Parse(fields[1], CultureInfo.InvariantCulture), isolationWindow.MethodEnd, 0.01);
                    if (hasMargin)
                        Assert.AreEqual(double.Parse(fields[2], CultureInfo.InvariantCulture), isolationWindow.StartMargin ?? 0, 0.01);
                }
            });
            PauseForScreenShot<EditIsolationSchemeDlg>("Isolation scheme");

            var isolationGraph = ShowDialog<DiaIsolationWindowsGraphForm>(isolationScheme.OpenGraph);
            PauseForScreenShot<DiaIsolationWindowsGraphForm>("Isolation scheme graph");

            OkDialog(isolationGraph, isolationGraph.CloseButton);
            OkDialog(isolationScheme, isolationScheme.OkDialog);

            if (IsPasef)
                PauseForScreenShot<ImportPeptideSearchDlg.ImsFullScanPage>("Import Peptide Search - Configure Full-Scan Settings page");
            else
                PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>("Import Peptide Search - Configure Full-Scan Settings page");

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            string fastPath = GetTestPath(_analysisValues.FastaPath);
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastPath);
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod =
                    Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = !IsDiaNN; // use peak boundaries from DIA-NN
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.ContainsFastaContent);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page");

            var peptidesPerProteinDlg = ShowDialog<AssociateProteinsDlg>(() => importPeptideSearchDlg.ClickNextButton());
            WaitForCondition(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                //int proteinCount, peptideCount, precursorCount, transitionCount;
                //peptidesPerProteinDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                //ValidateTargets(_analysisValues.TargetCounts, proteinCount, peptideCount, precursorCount, transitionCount, @"TargetCounts");
                if (_analysisValues.RemoveDuplicates)
                    peptidesPerProteinDlg.RemoveDuplicatePeptides = true;
                if (_analysisValues.MinPeptidesPerProtein.HasValue)
                    peptidesPerProteinDlg.MinPeptides = _analysisValues.MinPeptidesPerProtein.Value;
            });
            WaitForConditionUI(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                peptidesPerProteinDlg.NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                _testInfo.ValidateTargets(IsRecordMode, ref _expectedValues.FinalTargetCounts, proteinCount,
                    peptideCount, precursorCount, transitionCount);
            });
            PauseForScreenShot<AssociateProteinsDlg>("Import FASTA summary form");

            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);

            var fv = _instrumentValues.FrozenImportValues;
            if (fv != null && !PauseForAllChromatogramsGraphScreenShot("Importing Results form",
                    fv.TotalProgress, fv.ElapsedTime, fv.GraphTime, fv.GraphIntensityMax, fv.FileProgress))
            {
                return;
            }
            WaitForDocumentChangeLoaded(doc, 40 * 60 * 1000); // 40 minutes

            if (importPeptideSearchDlg.ImportFastaControl.AutoTrain)
            {
                var peakScoringModelDlg = WaitForOpenForm<EditPeakScoringModelDlg>();
                PauseForScreenShot<EditPeakScoringModelDlg>("mProphet model form");
                ValidateCoefficients(peakScoringModelDlg, ref _expectedValues.ScoringModelCoefficients);

                OkDialog(peakScoringModelDlg, peakScoringModelDlg.OkDialog);
            }

            var docLibrary = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries[0];
            RunUI(() =>
            {
                Assert.AreEqual(_instrumentValues.IrtStandardCount, SkylineWindow.Document.PeptideGroups.First().PeptideCount);
                if (!IsRecordMode)
                {
                    Assert.AreEqual(_expectedValues.LibraryPeptideCount + _instrumentValues.IrtStandardCount, docLibrary.LibraryDetails.UniquePeptideCount);
                }
                else
                {
                    _expectedValues.LibraryPeptideCount = docLibrary.LibraryDetails.UniquePeptideCount - _instrumentValues.IrtStandardCount;
                }
            });

            // Setup annotations
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);

            AddReplicateAnnotation(documentSettingsDlg, "Condition", AnnotationDef.AnnotationType.value_list,
                new[] { "A", "B" }, true);

            AddReplicateAnnotation(documentSettingsDlg, "BioReplicate");

            RunUI(() =>
            {
                documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(0, true);
                documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(1, true);
            });

            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);

            // Annotate replicates in Document Grid: Replicates
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() =>
            {
                documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates);
                FormEx.GetParentForm(documentGrid).Size = new Size(591, 283);
            });
            WaitForConditionUI(() => documentGrid.IsComplete); // Let it initialize

            RunUI(() =>
            {
                var pathCondition = PropertyPath.Root.Property(AnnotationDef.GetColumnName("Condition"));
                var columnSubjectId = documentGrid.FindColumn(pathCondition);
                var gridView = documentGrid.DataGridView;
                gridView.CurrentCell = gridView.Rows[0].Cells[columnSubjectId.Index];
            });

            var replicateAnnotations = new[]
            {
                new[] {"A", "1"},
                new[] {"B", "1"},
                new[] {"A", "2"},
                new[] {"B", "2"},
                new[] {"A", "3"},
                new[] {"B", "3"}
            };
            SetClipboardText(TextUtil.LineSeparate(replicateAnnotations.Select(TextUtil.ToEscapedTSV)));

            RunUI(() => documentGrid.DataGridView.SendPaste());
            PauseForScreenShot<DocumentGridForm>("Document Grid - filled");

            RunUI(() => SkylineWindow.ShowDocumentGrid(false));

            // Arrange windows for manual inspection
            var arrangeGraphsDlg = ShowDialog<ArrangeGraphsGroupedDlg>(SkylineWindow.ArrangeGraphsGrouped);
            RunUI(() =>
            {
                arrangeGraphsDlg.Groups = 2;
                arrangeGraphsDlg.GroupType = GroupGraphsType.distributed;
                arrangeGraphsDlg.GroupOrder = GroupGraphsOrder.Document;
                arrangeGraphsDlg.DisplayType = DisplayGraphsType.Row;
            });

            OkDialog(arrangeGraphsDlg, arrangeGraphsDlg.OkDialog);

            RunUI(() => SkylineWindow.SaveDocument());

            if (IsPauseForScreenShots)
            {
                RestoreViewOnScreenNoSelChange(17);
                SelectNode(SrmDocument.Level.MoleculeGroups, 0);
                Rectangle rectFrame = Rectangle.Empty;
                RunUI(() =>
                {
                    SkylineWindow.Size = new Size(900, 900);
                    SkylineWindow.ForceOnScreen();  // Avoid this shifting the window under the floating window later
                });
                Thread.Sleep(200);  // Give layout time to adjust
                RunUI(() =>
                {
                    var chromPane1 = SkylineWindow.GetGraphChrom(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[0].Name);
                    var chromPane2 = SkylineWindow.GetGraphChrom(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[1].Name);
                    var rtGraphFrame = FindFloatingWindow(SkylineWindow.GraphRetentionTime);
                    var chromTopLeft = chromPane1.PointToScreen(new Point(0, 0));
                    var chromTopRight = chromPane2.PointToScreen(new Point(chromPane2.Width, 0));
                    rtGraphFrame.Location = chromPane1.PointToScreen(new Point(((chromTopRight.X - chromTopLeft.X) - rtGraphFrame.Width) / 2, 50));
                    rectFrame = rtGraphFrame.Bounds;
                    rtGraphFrame.Activate();    // TODO: Want the graph activated but a screenshot of screen
                });
                PauseForScreenShot<ScreenForm>("Docking floating image with cursor", null, bmp =>
                    DrawLArrowCursorOnBitmap(ClipDockingRect(bmp, rectFrame), 0.5, 0.155));
                BeginDragDisplay(SkylineWindow.GraphRetentionTime, 0.62, 0.11);
                PauseForScreenShot<ScreenForm>("Docking image cursor on upper dock indicator", null, bmp =>
                    ClipDockingRect(bmp, rectFrame));
                EndDragDisplay();
            }

            const string proteinNameToSelect = "sp|P63284|CLPB_ECOLI";
            if (Equals(proteinNameToSelect, SkylineWindow.Document.MoleculeGroups.Skip(1).First().Name))
                SelectNode(SrmDocument.Level.MoleculeGroups, 1);
            else
                FindNode(proteinNameToSelect);

            RunUI(() =>
            {
                Assert.AreEqual(proteinNameToSelect, SkylineWindow.SelectedNode.Text);

                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.Size = new Size(1226, 900);
            });
            RestoreViewOnScreenNoSelChange(18);
            WaitForGraphs();

            PauseForScreenShot("Manual review window layout with protein selected");

            FindNode(_instrumentValues.ExamplePeptide);
            WaitForGraphs();
            PauseForScreenShot("Manual review window layout with peptide selected");

            FindNode("_HUMAN");
            WaitForGraphs();
            FindNode(_instrumentValues.ExamplePeptide);
            RunUI(SkylineWindow.AutoZoomBestPeak);
            WaitForGraphs();

            var firstReplicateName = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.First().Name;
            var clickPoint = new PointF(_analysisValues.ChromatogramClickPoint.X, _analysisValues.ChromatogramClickPoint.Y);
            if (IsPauseForScreenShots)
            {
                MouseOverChromatogram(firstReplicateName, clickPoint.X, clickPoint.Y);
            }

            var graphChrom = SkylineWindow.GetGraphChrom(firstReplicateName);
            PauseForScreenShot(graphChrom, "Snip just one chromatogram pane", null, bmp => DrawHandCursorOnChromBitmap(bmp,
                graphChrom, true, clickPoint.X, clickPoint.Y));

            try
            {
                ClickChromatogram(firstReplicateName, clickPoint.X, clickPoint.Y);
            }
            catch (AssertFailedException e)
            {
                PauseForRecordModeInstruction(
                    $"Clicking the left-side chromatogram at ({_analysisValues.ChromatogramClickPoint.X}, {_analysisValues.ChromatogramClickPoint.Y}) failed.\r\n" +
                    "Click on and record a new ChromatogramClickPoint at the peak of that chromatogram.", e);
            }

            PauseForScreenShot<GraphFullScan>("Full-Scan graph window - zoomed");

            if (IsPasef)
            {
                RunUI(() => SkylineWindow.GraphFullScan.ShowMobility(true));
                WaitForGraphs();
                PauseForScreenShot<GraphFullScan>("Full-Scan graph window - mobility zoomed");
            }

            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(false));
            WaitForGraphs();
            PauseForScreenShot<GraphFullScan>("Full-Scan graph window - unzoomed");

            RunUI(SkylineWindow.GraphFullScan.Close);
            RunUI(SkylineWindow.ShowMassErrorHistogramGraph);
            if (IsPasef)
            {
                // diaPASEF has an outlier in a poor scoring peak
                RunUI(() => SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.targets_1FDR));
            }
            WaitForGraphs();
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out MassErrorHistogramGraphPane massErrorPane));
            int massErrorStatsIndex = 0;
            ValidateMassErrors(massErrorPane, massErrorStatsIndex++);

            PauseForScreenShot(SkylineWindow.GraphMassError,"Mass errors histogram graph window");

            // Review single replicates
            RunUI(SkylineWindow.ShowSingleReplicate);
            foreach (var chromatogramSet in SkylineWindow.Document.MeasuredResults.Chromatograms)
            {
                RunUI(() => SkylineWindow.ActivateReplicate(chromatogramSet.Name));
                WaitForGraphs();
                ValidateMassErrors(massErrorPane, massErrorStatsIndex++);
            }
            RunUI(() =>
            {
                SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.decoys);
                SkylineWindow.ShowAverageReplicates();
            });
            WaitForGraphs();
            RunUI(() => SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.targets));    // CONSIDER: 1% FDR
            RunUI(() => SkylineWindow.ShowGraphMassError(false));

            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);
            RestoreViewOnScreenNoSelChange(24);
            WaitForRegression();
            PauseForScreenShot(SkylineWindow.GraphRetentionTime, "Retention time regression graph window - regression");

            RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
            WaitForGraphs();
            PauseForScreenShot(SkylineWindow.GraphRetentionTime, "Retention time regression graph window - residuals");
            RunUI(() => SkylineWindow.ShowGraphRetentionTime(false, GraphTypeSummary.score_to_run_regression));

            var editGroupComparisonDlg = ShowDialog<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison);
            const string groupComparisonName = @"By Condition";
            RunUI(() =>
            {
                editGroupComparisonDlg.TextBoxName.Text = groupComparisonName;
                editGroupComparisonDlg.ControlAnnotation = "Condition";
            });
            WaitForConditionUI(() => editGroupComparisonDlg.ControlValueOptions.Any());
            RunUI(() =>
            {
                editGroupComparisonDlg.ControlValue= "A";
                editGroupComparisonDlg.CaseValue = "B";
                editGroupComparisonDlg.IdentityAnnotation = "BioReplicate";   // Irrelevant
                editGroupComparisonDlg.ShowAdvanced(true);
                editGroupComparisonDlg.TextBoxQValueCutoff.Text = (0.01).ToString(CultureInfo.CurrentCulture);
            });
            PauseForScreenShot<EditGroupComparisonDlg>("Group comparison");

            OkDialog(editGroupComparisonDlg, editGroupComparisonDlg.OkDialog);
            RunUI(() => SkylineWindow.ShowGroupComparisonWindow(groupComparisonName));
            {
                var fcGrid = WaitForOpenForm<FoldChangeGrid>();
                var fcGridControl = fcGrid.DataboundGridControl;
                WaitForConditionUI(() => fcGridControl.IsComplete && fcGridControl.FindColumn(_resultProperty) != null && fcGridControl.RowCount > 11);
                RunUI(() =>
                {
                    var foldChangeResultColumn = fcGridControl.FindColumn(_resultProperty);
                    fcGridControl.DataGridView.AutoResizeColumn(foldChangeResultColumn.Index);
                    var proteinNameColumn = fcGridControl.FindColumn(_proteinProperty);
                    fcGridControl.DataGridView.AutoResizeColumn(proteinNameColumn.Index);
                    fcGridControl.DataGridView.FirstDisplayedScrollingRowIndex = 11;  // Scroll past iRT peptides
                });
                WaitForConditionUI(() => 0 != fcGridControl.RowCount, "0 != foldChangeGrid.DataboundGridControl.RowCount");
                WaitForConditionUI(() => fcGridControl.IsComplete, "foldChangeGrid.DataboundGridControl.IsComplete");
                PauseForScreenShot<FoldChangeGrid>("By Condition grid");

                var volcanoPlot = ShowDialog<FoldChangeVolcanoPlot>(fcGrid.ShowVolcanoPlot);
                RestoreViewOnScreenNoSelChange(27);
                fcGrid = WaitForOpenForm<FoldChangeGrid>();
                WaitForConditionUI(() => fcGrid.DataboundGridControl.IsComplete && fcGrid.DataboundGridControl.RowCount > 11);
                RunUI(() => fcGrid.DataboundGridControl.DataGridView.FirstDisplayedScrollingRowIndex = 11); // Re-apply scrolling
                PauseForScreenShot<FoldChangeVolcanoPlot>("By Condition:Volcano Plot - unformatted");
                volcanoPlot = WaitForOpenForm<FoldChangeVolcanoPlot>();    // May have changed with RestoreViewsOnScreen
                WaitForConditionUI(() => volcanoPlot.CurveList.Count == 5);
                WaitForConditionUI(() => volcanoPlot.CurveList[4].Points.Count > SkylineWindow.Document.MoleculeCount/4);
                RunUI(() =>
                {
                    int actualPoints = volcanoPlot.CurveList[4].Points.Count;
                    if (IsRecordMode)
                    {
                        _expectedValues.DiffPeptideCounts = new[] { actualPoints };
                    }
                    else
                    {
                        Assert.AreEqual(_expectedValues.DiffPeptideCounts[0], actualPoints);
                    }
                });
                var formattingDlg = ShowDialog<VolcanoPlotFormattingDlg>(volcanoPlot.ShowFormattingDialog);
                ApplyFormatting(formattingDlg, "ECOLI", "128, 0, 255");
                var createExprDlg = ShowDialog<CreateMatchExpressionDlg>(() =>
                    formattingDlg.ClickCreateExpression(formattingDlg.ResultList.Count - 1));
                PauseForScreenShot<CreateMatchExpressionDlg>("Create Expression form");
                OkDialog(createExprDlg, createExprDlg.OkDialog);

                ApplyFormatting(formattingDlg, "YEAS", "255, 128, 0");
                ApplyFormatting(formattingDlg, "HUMAN", "0, 128, 0");
                PauseForScreenShot<VolcanoPlotFormattingDlg>("Volcano plot formatting form");
                OkDialog(formattingDlg, formattingDlg.OkDialog);
                //PauseTest();
                WaitForConditionUI(() => volcanoPlot.CurveList.Count == 8 &&
                                         volcanoPlot.CurveList[7].Points.Count >= _instrumentValues.IrtStandardCount); // iRTs
                for (int i = 1; i < 4; i++)
                {
                    RunUI(() =>
                    {
                        int actualPoints = volcanoPlot.CurveList[7 - i].Points.Count;
                        if (IsRecordMode)
                        {
                            _expectedValues.DiffPeptideCounts =
                                _expectedValues.DiffPeptideCounts.Append(actualPoints).ToArray();
                        }
                        else
                            Assert.AreEqual(_expectedValues.DiffPeptideCounts[i], actualPoints);
                    });
                }
                PauseForGraphScreenShot("By Condition:Volcano Plot - fully formatted", volcanoPlot);
            }

            {
                var fcGrid = WaitForOpenForm<FoldChangeGrid>(); // May have changed with RestoreViewsOnScreen
                RunUI(fcGrid.ShowGraph);
                RestoreViewOnScreenNoSelChange(30);
            }

            {
                var fcGrid = WaitForOpenForm<FoldChangeGrid>(); // May have changed with RestoreViewsOnScreen
                var fcGridControl = fcGrid.DataboundGridControl;
                FilterIrtProtein(fcGridControl);

                var volcanoPlot = WaitForOpenForm<FoldChangeVolcanoPlot>();    // May have changed with RestoreViewsOnScreen
                WaitForConditionUI(() => volcanoPlot.CurveList.Count == 8 && 
                                         volcanoPlot.CurveList[7].Points.Count == 0); // No iRTs
                if (!IsRecordMode)
                {
                    for (int i = 1; i < 4; i++)
                        RunUI(() => Assert.AreEqual(_expectedValues.DiffPeptideCounts[i], volcanoPlot.CurveList[7 - i].Points.Count));
                }
                var barGraph = WaitForOpenForm<FoldChangeBarGraph>();
                
                if (!IsRecordMode)
                {
                    int volcanoBarDelta = _instrumentValues.IrtStandardCount - 1; // iRTs - selected peptide
                    WaitForBarGraphPoints(barGraph, _expectedValues.DiffPeptideCounts[0] - volcanoBarDelta, _expectedValues.DiffPeptideCounts[0] - volcanoBarDelta * 2);
                }

                SortByFoldChange(fcGridControl, _resultProperty);
                PauseForScreenShot<FoldChangeBarGraph>("By Condition:Bar Graph - peptides");

                var changeGroupComparisonSettings = ShowDialog<EditGroupComparisonDlg>(fcGrid.ShowChangeSettings);
                RunUI(() => changeGroupComparisonSettings.RadioScopePerProtein.Checked = true);

                int targetProteinCount = SkylineWindow.Document.MoleculeGroupCount - 2; // minus iRTs and decoys
                if (!IsRecordMode)
                    WaitForBarGraphPoints(barGraph, _expectedValues.UnpolishedProteins);
                else
                {
                    WaitForBarGraphPoints(barGraph, targetProteinCount, 1);
                    _expectedValues.UnpolishedProteins = GetBarCount(barGraph);
                }

                RunUI(() => changeGroupComparisonSettings.ComboSummaryMethod.SelectedItem =
                    SummarizationMethod.MEDIANPOLISH);

                if (IsDiaNN && _analysisValues.IsWholeProteome)
                {
                    if (IsRecordMode)
                    {
                        _testInfo.SaveExpectedValues();
                    }
                    return; // fold change bar graphs don't behave the same way for DIA-NN results as for iProphet, so exit early
                }

                WaitForConditionUI(() => barGraph.IsComplete);
                if (IsRecordMode)
                {
                    _expectedValues.PolishedProteins = GetBarCount(barGraph);
                }
                else
                {
                    Assert.AreEqual(_expectedValues.PolishedProteins, GetBarCount(barGraph));
                }

                fcGrid = WaitForOpenForm<FoldChangeGrid>();
                var fcGridControlFinal = fcGrid.DataboundGridControl;
                SortByFoldChange(fcGridControlFinal, _resultProperty);  // Re-apply the sort, in case it was lost in restoring views

                RestoreViewOnScreen(31);
                barGraph = WaitForOpenForm<FoldChangeBarGraph>();
                WaitForConditionUI(() => barGraph.IsComplete);
                RunUIForScreenShot(() =>
                {
                    var yScale = barGraph.ZedGraphControl.GraphPane.YAxis.Scale;
                    yScale.MinAuto = yScale.MaxAuto = false;
                    yScale.Max = _analysisValues.FoldChangeProteinsMax ?? 2.2;
                    yScale.Min = _analysisValues.FoldChangeProteinsMin ?? -2.4;
                    yScale.MajorStep = 1;
                    yScale.MinorStep = 0.2;
                    yScale.Format = "0.#";
                });
                PauseForGraphScreenShot("By Condition:Bar Graph - proteins", barGraph);

                RunQValueSummaryTest();

                if (IsCoverShotMode)
                {
                    RunUI(() =>
                    {
                        Settings.Default.ChromatogramFontSize = 14;
                        Settings.Default.AreaFontSize = 14;
                        SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    });

                    RestoreCoverViewOnScreen();
                    fcGrid = WaitForOpenForm<FoldChangeGrid>();
                    fcGridControlFinal = fcGrid.DataboundGridControl;
                    FilterIrtProtein(fcGridControlFinal);
                    changeGroupComparisonSettings = ShowDialog<EditGroupComparisonDlg>(fcGrid.ShowChangeSettings);
                    RunUI(() => changeGroupComparisonSettings.RadioScopePerPeptide.Checked = true);
                    OkDialog(changeGroupComparisonSettings, changeGroupComparisonSettings.Close);
                    WaitForConditionUI(() => fcGrid.IsComplete);
                    WaitForGraphs();
                    volcanoPlot = WaitForOpenForm<FoldChangeVolcanoPlot>();    // May have changed with RestoreCoverViewOnScreen
                    WaitForConditionUI(() => !volcanoPlot.UpdatePending);
                    RunUI(() =>
                    {
                        var pane = volcanoPlot.GraphControl.GraphPane;
                        var xScale = pane.XAxis.Scale;
                        xScale.MaxAuto = xScale.MinAuto = false;
                        xScale.Min = -4;
                        xScale.Max = 4;
                        pane.AxisChange();
                        volcanoPlot.GraphControl.Invalidate();
                    });

                    RunUI(() =>
                    {
                        var fcFloatingWindow = fcGrid.Parent.Parent;
                        fcFloatingWindow.Left = SkylineWindow.Left + 8;
                        fcFloatingWindow.Top = SkylineWindow.Bottom - fcFloatingWindow.Height - 8;
                    });

                    if (!IsPasef)
                    {
                        FocusDocument();
                        TakeCoverShot();
                    }
                    else
                    {
                        ClickChromatogram(SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name,
                            1.2642E+01, 1.0521E+04);
                        RunUI(() => SkylineWindow.ShowChromatogramLegends(false));
                        RunUI(() => SkylineWindow.GraphFullScan.SetZoom(true));
                        WaitForGraphs();
                        TakeCoverShot(FindOpenForm<GraphFullScan>());
                    }
                }
            }
            if (IsRecordMode)
            {
                _testInfo.SaveExpectedValues();
            }
        }

        private static Bitmap ClipDockingRect(Bitmap bmp, Rectangle rectFrame)
        {
            const int hBorder = 20;
            const int topBorder = 100;
            const int bottomBorder = 10;
            var rectClip = new Rectangle(rectFrame.X - hBorder, rectFrame.Y - topBorder, rectFrame.Width + hBorder * 2,
                rectFrame.Height + topBorder + bottomBorder);
            bmp = ClipBitmap(bmp, rectClip);
            return bmp;
        }

        private void RunQValueSummaryTest()
        {
            if (!IsTtof || _analysisValues.IsWholeProteome)
                return;

            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg = ShowDialog<ManageViewsForm>(exportReportDlg.EditList);
            var viewEditor = ShowDialog<ViewEditor>(editReportListDlg.AddView);
            string qValuesReportName = "Q-ValuesTest";
            string reportFileName = TestFilesDirs[0].GetTestPath(qValuesReportName + ".csv");
            RunUI(() =>
            {
                viewEditor.ViewName = qValuesReportName;
                var columnsToAdd = new[]
                {
                    // Not L10N
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.NeutralMass"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.DetectionQValue"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.ResultSummary.DetectionQValue.Min"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.ResultSummary.DetectionQValue.Max"),
                    PropertyPath.Parse(
                        "Proteins!*.Peptides!*.Precursors!*.ResultSummary.DetectionQValue.Median")
                };
                foreach (var id in columnsToAdd)
                {
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(id), "Unable to select {0}", id);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                }

                viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>().First().SetPivotReplicate(true);
            });

            OkDialog(viewEditor, () => viewEditor.OkDialog());
            OkDialog(editReportListDlg, () => editReportListDlg.OkDialog());

            RunUI(() =>
            {
                exportReportDlg.ReportName = qValuesReportName;
                exportReportDlg.OkDialog(reportFileName, TextUtil.SEPARATOR_CSV); // Not L10N
            });

            foreach (var line in File.ReadLines(reportFileName).Skip(1))
            {
                var values = line.Split(',').Select((strValue) =>
                    double.TryParse(strValue, out var res) ? res : double.NaN
                ).ToArray();
                //skip the line if it has any N/As in it
                if (!values.Any(double.IsNaN))
                {
                    var stats = new Statistics(values.Skip(4));
                    //using numeric comparison due to rounding errors
                    Assert.IsTrue(Math.Abs(values[1] - stats.Min()) <= 0.0001);
                    Assert.IsTrue(Math.Abs(values[2] - stats.Max()) <= 0.0001);
                    Assert.IsTrue(Math.Abs(values[3] - stats.Median()) <= 0.0001);
                }
            }
        }

        private void FilterIrtProtein(DataboundGridControl fcGridControl)
        {
            WaitForConditionUI(() => fcGridControl.IsComplete && fcGridControl.FindColumn(_proteinProperty) != null);
            var quickFilterForm = ShowDialog<QuickFilterForm>(() =>
            {
                var proteinNameColumn = fcGridControl.FindColumn(_proteinProperty);
                fcGridControl.QuickFilter(proteinNameColumn);
            });
            RunUI(() =>
            {
                quickFilterForm.SetFilterOperation(0, FilterOperations.OP_NOT_CONTAINS);
                quickFilterForm.SetFilterOperand(0, _analysisValues.IrtFilterText);
            });
            OkDialog(quickFilterForm, quickFilterForm.OkDialog);
        }

        private void RestoreViewOnScreenNoSelChange(int pageName)
        {
            if (!Program.SkylineOffscreen)
            {
                RunUI(() =>
                {
                    var selectedPath = SkylineWindow.SelectedPath;
                    RestoreViewOnScreen(pageName);
                    SkylineWindow.SelectedPath = selectedPath;
                });
            }
        }

        private void ApplyFormatting(VolcanoPlotFormattingDlg formattingDlg, string matchText, string rgbText)
        {
            RunUI(() =>
            {
                var color = RgbHexColor.ParseRgb(rgbText).Value;
                formattingDlg.AddRow(new MatchRgbHexColor("ProteinName: " + matchText, 
                    false, color, PointSymbol.Circle, PointSize.normal));
            });
        }

        private void ValidateCoefficients(EditPeakScoringModelDlg editDlgFromSrm, ref double?[] expectedCoefficients)
        {
            var actualCoefficients = editDlgFromSrm.PeakCalculatorsGrid.Items
                .Select(item=> item.Weight.HasValue ? Math.Round(item.Weight.Value, 4) : (double?) null).ToArray();

            if (IsRecordMode)
            {
                expectedCoefficients = actualCoefficients;
            }
            else
            {
                AssertEx.AreEqual(string.Join("|", expectedCoefficients), string.Join("|", actualCoefficients));
            }
        }

        private void ValidateMassErrors(MassErrorHistogramGraphPane massErrorPane, int index)
        {
            double mean = massErrorPane.Mean, stdDev = massErrorPane.StdDev;
            if (IsRecordMode)
            {
                _expectedValues.MassErrorStats ??= Array.Empty<double[]>();
                while (_expectedValues.MassErrorStats.Length <= index)
                {
                    _expectedValues.MassErrorStats =
                        _expectedValues.MassErrorStats.Append(Array.Empty<double>()).ToArray();
                }
                _expectedValues.MassErrorStats[index] = new []{ Math.Round(mean, 2), Math.Round(stdDev, 2) };
            }
            else
            {
                Assert.AreEqual(_expectedValues.MassErrorStats[index][0], mean, 0.05);
                Assert.AreEqual(_expectedValues.MassErrorStats[index][1], stdDev, 0.05);
            }
        }

        private void WaitForBarGraphPoints(FoldChangeBarGraph barGraph, int barCount, int? lowerBoundCount = null)
        {
            WaitForConditionUI(() => barGraph.ZedGraphControl.GraphPane.CurveList.Count == 1);
            if (!lowerBoundCount.HasValue)
            {
                WaitForConditionUI(() => barCount == GetBarCount(barGraph),
                    () => string.Format("Expecting {0} bars, actual {1} bars", barCount, GetBarCount(barGraph)));
            }
            else
            {
                WaitForConditionUI(() => lowerBoundCount.Value <= GetBarCount(barGraph) && GetBarCount(barGraph) <= barCount,
                    () => string.Format("Expecting >= {0} and <= {1} bars, actual {2} bars", lowerBoundCount.Value, barCount, GetBarCount(barGraph)));
            }
        }

        private int GetBarCount(FoldChangeBarGraph barGraph)
        {
            return barGraph.ZedGraphControl.GraphPane.CurveList[0].Points.Count;
        }

        private static void SortByFoldChange(DataboundGridControl fcGridControl, PropertyPath fcResultProperty)
        {
            RunUI(() =>
            {
                var fcResultColumn = fcGridControl.FindColumn(fcResultProperty);
                fcGridControl.SetSortDirection(fcGridControl.GetPropertyDescriptor(fcResultColumn),
                    ListSortDirection.Ascending);
            });
        }
    }

    [TestClass]
    public class DiaSwathCommandLineTest : AbstractUnitTestEx
    {
        private DiaSwathTestInfo _testInfo = new DiaSwathTestInfo();
        private string InstrumentTypeName => _testInfo._instrumentValues.InstrumentTypeName;
        private string RootName => _testInfo.RootName;
        private string ZipFileName => _testInfo._instrumentValues.ZipFileName ?? RootName;
        private DiaSwathTestInfo.AnalysisValues _analysisValues => _testInfo._analysisValues;
        private DiaSwathTestInfo.InstrumentSpecificValues _instrumentValues => _testInfo._instrumentValues;

        [TestMethod]
        public void ConsoleTestDiaTtof()
        {
            _testInfo.TestTtofData(false);
            RunTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] // Times out on slower worker VMs
        public void ConsoleTestDiaTtofFullSearch()
        {
            _testInfo.TestTtofData(true);
            RunTest();
        }


        [TestMethod]
        public void ConsoleTestDiaQe()
        {
            _testInfo.TestQeData(false);
            RunTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] // Times out on slower VMs
        public void ConsoleTestDiaQeFullSearch()
        {
            _testInfo.TestQeData(true);
            RunTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)] // Bruker wants exclusive read access to raw data
        [Timeout(int.MaxValue)] // These can take a long time
        public void ConsoleTestDiaPasef()
        {
            _testInfo.TestPasefData(false);
            RunTest();
        }

        [TestMethod,
         NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING), // Bruker wants exclusive read access to raw data
         NoNightlyTesting(TestExclusionReason.EXCESSIVE_TIME)] // Skip during Nightly
        [Timeout(int.MaxValue)] // These can take a long time
        public void ConsoleTestDiaPasefFullDataset()
        {
            _testInfo.TestPasefData(true);
            RunTest();
        }

        private string GetTestPath(string path)
        {
            foreach(var dir in TestFilesDirs)
            {
                string possiblePath = dir.GetTestPath(Path.Combine(ZipFileName, path));
                if (File.Exists(possiblePath) || Directory.Exists(possiblePath))
                    return possiblePath;
                possiblePath = dir.GetTestPath(path);
                if (File.Exists(possiblePath) || Directory.Exists(possiblePath))
                    return possiblePath;
            }

            throw new ArgumentException($"{path} does not exist in any of the TestFilesDirs");
        }

        private void RunTest()
        {
            if (!RunPerfTests)
                return;

            TestFilesZipPaths = _testInfo.TestFilesZipPaths;
            TestFilesPersistent = _testInfo.TestFilesPersistent;

            // Unzip test files.
            if (TestFilesZipPaths != null)
            {
                TestFilesDirs = new TestFilesDir[TestFilesZipPaths.Length];
                for (int i = 0; i < TestFilesZipPaths.Length; i++)
                {
                    TestFilesDirs[i] = new TestFilesDir(TestContext, TestFilesZipPaths[i], TestDirectoryName,
                        TestFilesPersistent, IsExtractHere(i));
                }
            }

            string documentBaseName = "DIA-" + InstrumentTypeName + "-tutorial-cli";
            string documentFile = TestFilesDirs[0].GetTestPath(documentBaseName + SrmDocument.EXT);

            // arguments that would normally be quoted on the command-line shouldn't be quoted here
            var settings = new[]
            {
                "--new=" + documentFile,
                "--full-scan-precursor-isotopes=Count",
                "--full-scan-acquisition-method=DIA",
                "--full-scan-precursor-isotopes=None",
                "--full-scan-product-analyzer=centroided",
                "--full-scan-rt-filter=scheduling_windows",
                "--full-scan-rt-filter-tolerance=5",
                "--full-scan-isolation-scheme=" + Path.Combine(GetTestPath("DIA"), _instrumentValues.DiaFiles[0]),
                "--tran-precursor-ion-charges=2,3,4",
                "--tran-product-ion-charges=1,2",
                "--tran-product-start-ion=" + TransitionFilter.StartFragmentFinder.ION_3.Label,
                "--tran-product-end-ion=" + TransitionFilter.EndFragmentFinder.LAST_ION.Label,
                "--tran-product-clear-special-ions",
                "--tran-use-dia-window-exclusion",
                "--library-product-ions=6",
                "--library-min-product-ions=6",
                "--library-match-tolerance=" + 0.05 + "mz",
                "--library-pick-product-ions=filter",
                "--instrument-max-mz=2000",
                "--reintegrate-model-name=" + documentBaseName,
                "--reintegrate-create-model",
                "--reintegrate-model-type=mprophet",
                "--import-search-exclude-library-sources",
                "--import-search-irts=" + _instrumentValues.IrtStandard,
                "--import-fasta=" + GetTestPath(_analysisValues.FastaPath),
                "--decoys-add=shuffle"
            };
            settings = settings.Concat(_instrumentValues.SearchFiles.Select(o => "--import-search-file=" + GetTestPath(o))).ToArray();
            settings = settings.Concat(_instrumentValues.DiaFiles.Select(o => "--import-file=" + Path.Combine(GetTestPath("DIA"), o)).Take(1)).ToArray();

            if (_testInfo.IsPasef)
            {
                settings = settings.Append("--ims-library-res=30").ToArray();
                settings = settings.Append("--import-search-add-mods").ToArray();
            }

            //const string modOxidation = "Oxidation (M)";
            // Define expected matched/unmatched modifications
            //var expectedMatched = !IsPasef ? new[] { modOxidation } : Array.Empty<string>();
            // Verify matched/unmatched modifications
            //AssertEx.AreEqualDeep(expectedMatched, importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.ToArray());
            //Assert.IsFalse(importPeptideSearchDlg.MatchModificationsControl.UnmatchedModifications.Any());

            // Default is to have precursors
            if (_analysisValues.KeepPrecursors)
            {
                settings = settings.Append("--tran-product-ion-types=y,b,p").ToArray();
                settings = settings.Append("--full-scan-precursor-res=20").ToArray();
            }
            else
            {
                settings = settings.Append("--tran-product-ion-types=y,b").ToArray();
                settings = settings.Append("--full-scan-product-res=20").ToArray();
            }

            if (_instrumentValues.IrtStandard.IsAuto ||
                Equals(_instrumentValues.IrtStandard, IrtStandard.CIRT) ||
                Equals(_instrumentValues.IrtStandard, IrtStandard.CIRT_SHORT))
            {
                settings = settings.Append("--import-search-num-cirts=" + _instrumentValues.IrtStandardCount).ToArray();
            }

            // Verify other values shown in the tutorial
            // CONSIDER: Not that easy to validate 1, 2 in ion charges.

            /*string schemePath = Path.Combine("DIA", _instrumentValues.IsolationSchemeFile);
            var schemeLines = File.ReadAllLines(GetTestPath(schemePath));
            string[][] windowFields = schemeLines.Select(l =>
                TextUtil.ParseDsvFields(l, _instrumentValues.IsolationSchemeFileSeparator)).ToArray();
            WaitForConditionUI(() => isolationScheme.GetIsolationWindows().Count == schemeLines.Length);*/

            //Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
            //Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
            //importPeptideSearchDlg.ImportFastaControl.AutoTrain = true;

            if (_analysisValues.RemoveDuplicates)
                //settings = settings.Append("--refine-remove-duplicates").ToArray();
                settings = settings.Append("--associate-proteins-shared-peptides=Removed").ToArray();
            if (_analysisValues.MinPeptidesPerProtein.HasValue)
                //settings = settings.Append("--refine-min-peptides=" + _analysisValues.MinPeptidesPerProtein.Value).ToArray();
                settings = settings.Append("--associate-proteins-min-peptides=" + _analysisValues.MinPeptidesPerProtein.Value).ToArray();
            else
                settings = settings.Append("--associate-proteins-min-peptides=1").ToArray();

            string output = RunCommand(true, settings);

            try
            {

                var docInit = ResultsUtil.DeserializeDocument(documentFile);
                string docLibFile = BiblioSpecLiteSpec.GetLibraryFileName(documentFile);
                docInit = docInit.ChangeSettings(docInit.Settings.ConnectLibrarySpecs((library, spec) => spec, docLibFile));

                using (var docContainer = new ResultsTestDocumentContainer(null, documentFile))
                {
                    Assert.IsTrue(docContainer.SetDocument(docInit, null, true));
                    var doc = docContainer.Document;
                    var docLibrary = doc.Settings.PeptideSettings.Libraries.Libraries[0];
                    var irtGroup = doc.PeptideGroups.First();
                    Assert.AreEqual(_instrumentValues.IrtStandardCount, irtGroup.PeptideCount);
                    Assert.AreEqual(_testInfo._expectedValues.LibraryPeptideCount + _testInfo._instrumentValues.IrtStandardCount, docLibrary.LibraryDetails.UniquePeptideCount);

                    Assert.AreEqual(50, doc.Settings.TransitionSettings.Instrument.MinMz);
                    Assert.AreEqual(2000, doc.Settings.TransitionSettings.Instrument.MaxMz);
                    Assert.AreEqual(TransitionFilter.StartFragmentFinder.ION_3.Label, doc.Settings.TransitionSettings.Filter.StartFragmentFinderLabel.Label);
                    Assert.AreEqual(TransitionFilter.EndFragmentFinder.LAST_ION.Label, doc.Settings.TransitionSettings.Filter.EndFragmentFinderLabel.Label);
                    Assert.AreEqual(6, doc.Settings.TransitionSettings.Libraries.IonCount);
                    Assert.AreEqual(6, doc.Settings.TransitionSettings.Libraries.MinIonCount);
                    Assert.AreEqual(0.05, doc.Settings.TransitionSettings.Libraries.IonMatchMzTolerance.Value);
                    Assert.AreEqual(MzTolerance.Units.mz, doc.Settings.TransitionSettings.Libraries.IonMatchMzTolerance.Unit);

                    //Assert.AreEqual(0.95, importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Files.First().ScoreThreshold);

                    _testInfo.ValidateTargets(false, ref _testInfo._expectedValues.FinalTargetCounts,
                        doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                        doc.PeptideTransitionCount);
                    //ValidateCoefficients(peakScoringModelDlg, _analysisValues.ScoringModelCoefficients);
                    docContainer.AssertComplete();
                }
            }
            catch (Exception)
            {
                Console.Error.WriteLine(output);
                throw;
            }
        }
    }
}
