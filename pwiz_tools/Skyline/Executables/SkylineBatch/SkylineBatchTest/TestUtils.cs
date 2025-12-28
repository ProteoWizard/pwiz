/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;
using SharedBatch;
using System.Linq;
using SharedBatch.Properties;
using System.Text.RegularExpressions;

namespace SkylineBatchTest
{
    public class TestUtils
    {
        /// <summary>
        /// Sets up mock R installations for testing without requiring actual R on the system.
        /// 
        /// OPTIONAL: Tests currently work with real R if installed on your machine.
        /// Use this to run tests on TeamCity clients or machines without R installed.
        /// 
        /// SkylineBatch tests validate R versions exist but DO NOT execute R code.
        /// This test seam bypasses registry/filesystem R detection.
        /// 
        /// Usage (in test class):
        ///   [TestInitialize]
        ///   public void TestInit()
        ///   {
        ///       TestUtils.SetupMockRInstallations();  // Provides 4.0.3, 4.5.1, 5.0.0
        ///   }
        ///   
        ///   [TestCleanup]
        ///   public void TestCleanup()
        ///   {
        ///       TestUtils.ClearMockRInstallations();
        ///   }
        /// </summary>
        /// <param name="mockVersions">Dictionary of version -> path (e.g., "4.5.1" -> "mock/Rscript.exe")</param>
        public static void SetupMockRInstallations(Dictionary<string, string> mockVersions = null)
        {
            // Default to common R versions if none specified
            mockVersions ??= new Dictionary<string, string>
            {
                { "4.0.3", @"mock\R-4.0.3\bin\Rscript.exe" },
                { "4.5.1", @"mock\R-4.5.1\bin\Rscript.exe" },
                { "5.0.0", @"mock\R-5.0.0\bin\Rscript.exe" }
            };
            
            RInstallations.TestRVersions = mockVersions;
        }

        /// <summary>
        /// Clears mock R installations, restoring normal R detection behavior.
        /// Call this in test cleanup if SetupMockRInstallations was used.
        /// </summary>
        public static void ClearMockRInstallations()
        {
            RInstallations.TestRVersions = null;
        }

        public static string GetTestFilePath(string fileName)
        {
            var currentPath = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(currentPath, "SkylineCmd.exe")))
                currentPath = Path.Combine(currentPath, "..", "..", "..", "Executables", "SkylineBatch", "SkylineBatchTest");
            else
            {
                currentPath = Path.GetDirectoryName(Path.GetDirectoryName(currentPath));
            }

            var batchTestPath = Path.Combine(currentPath ?? string.Empty, "Test");
            if (!Directory.Exists(batchTestPath))
                throw new DirectoryNotFoundException("Unable to find test data directory at: " + batchTestPath);
            return Path.Combine(batchTestPath, fileName);
        }

        public static SkylineBatchConfig GetChangedConfig(SkylineBatchConfig baseConfig, Dictionary<string, object> changedVariables)
        {
            var name = baseConfig.Name;
            var enabled = baseConfig.Enabled;
            var modified = baseConfig.Modified;
            var logTestFormat = baseConfig.LogTestFormat;
            foreach (var variable in changedVariables.Keys)
            {
                if ("Name".Equals(variable))
                    name = (string) changedVariables[variable];
                else if ("Enabled".Equals(variable))
                    enabled = (bool)changedVariables[variable];
                else if ("Modified".Equals(variable))
                    modified = (DateTime)changedVariables[variable];
                else if ("LogTestFormat".Equals(variable))
                    logTestFormat = (bool) changedVariables[variable];
            }
            
            return new SkylineBatchConfig(name, enabled, logTestFormat, modified, GetChangedMainSettings(baseConfig.MainSettings, changedVariables), 
                GetChangedFileSettings(baseConfig.FileSettings, changedVariables),
                GetChangedRefineSettings(baseConfig.RefineSettings, changedVariables), GetChangedReportSettings(baseConfig.ReportSettings, changedVariables),
                GetChangedSkylineSettings(baseConfig.SkylineSettings, changedVariables));
        }

        public static MainSettings GetChangedMainSettings(MainSettings baseSettings, Dictionary<string, object> changedVariables)
        {
            var template = baseSettings.Template;
            var analysisFolder = baseSettings.AnalysisFolderPath;
            var dataFolder = baseSettings.DataFolderPath;
            var annotationsFile = baseSettings.AnnotationsFilePath;
            var namingPattern = baseSettings.ReplicateNamingPattern;

            foreach (var variable in changedVariables.Keys)
            {
                if ("Template".Equals(variable))
                    template = (SkylineTemplate)changedVariables[variable];
                else if ("AnalysisFolderPath".Equals(variable))
                    analysisFolder = (string)changedVariables[variable];
                else if ("DataFolderPath".Equals(variable))
                    dataFolder = (string)changedVariables[variable];
                else if ("AnnotationsFilePath".Equals(variable))
                    annotationsFile = (string)changedVariables[variable];
                else if ("ReplicateNamingPattern".Equals(variable))
                    namingPattern = (string)changedVariables[variable];
            }
            return new MainSettings(template, analysisFolder, false, dataFolder, null, annotationsFile, null, namingPattern);
        }

        public static FileSettings GetChangedFileSettings(FileSettings baseSettings,
            Dictionary<string, object> changedVariables)
        {
            var msOneResolvingPower = baseSettings.MsOneResolvingPower;
            var msMsResolvingPower = baseSettings.MsMsResolvingPower;
            var retentionTime = baseSettings.RetentionTime;
            var addDecoys = baseSettings.AddDecoys;
            var shuffleDecoys = baseSettings.ShuffleDecoys;
            var trainMProphet = baseSettings.TrainMProphet;

            foreach (var variable in changedVariables.Keys)
            {
                if ("MsOneResolvingPower".Equals(variable))
                    msOneResolvingPower = (int?) changedVariables[variable];
                else if ("MsMsResolvingPower".Equals(variable))
                    msMsResolvingPower = (int?) changedVariables[variable];
                else if ("RetentionTime".Equals(variable))
                    retentionTime = (int?) changedVariables[variable];
                else if ("AddDecoys".Equals(variable))
                    addDecoys = (bool) changedVariables[variable];
                else if ("ShuffleDecoys".Equals(variable))
                    shuffleDecoys = (bool) changedVariables[variable];
                else if ("TrainMProphet".Equals(variable))
                    trainMProphet = (bool) changedVariables[variable];
            }
            return new FileSettings(msOneResolvingPower, msMsResolvingPower, retentionTime, addDecoys, shuffleDecoys,
                trainMProphet);
        }

        public static ReportSettings GetChangedReportSettings(ReportSettings baseSettings,
            Dictionary<string, object> changedVariables)
        {
            var reports = baseSettings.Reports.ToList();

            foreach (var variable in changedVariables.Keys)
            {
                if ("Reports".Equals(variable))
                    reports = (List<ReportInfo>)changedVariables[variable];
            }
            return new ReportSettings(reports);
        }

        public static RefineSettings GetChangedRefineSettings(RefineSettings baseSettings,
            Dictionary<string, object> changedVariables)
        {
            var commandValues = baseSettings.CommandValuesCopy;
            var removeDecoys = baseSettings.RemoveDecoys;
            var removeResults = baseSettings.RemoveResults;
            var outputFilePath = baseSettings.OutputFilePath;

            foreach (var variable in changedVariables.Keys)
            {
                if ("CommandValues".Equals(variable))
                    commandValues = (RefineInputObject)changedVariables[variable];
                else if ("RemoveDecoys".Equals(variable))
                    removeDecoys = (bool)changedVariables[variable];
                else if ("RemoveResults".Equals(variable))
                    removeResults = (bool)changedVariables[variable];
                else if ("OutputFilePath".Equals(variable))
                    outputFilePath = (string)changedVariables[variable];
            }
            return new RefineSettings(commandValues, removeDecoys, removeResults, outputFilePath);
        }

        public static SkylineSettings GetChangedSkylineSettings(SkylineSettings baseSettings,
            Dictionary<string, object> changedVariables)
        {
            var type = baseSettings.Type;
            var cmdPath = baseSettings.CmdPath;

            foreach (var variable in changedVariables.Keys)
            {
                if ("Type".Equals(variable))
                    type = (SkylineType)changedVariables[variable];
                else if ("CmdPath".Equals(variable))
                    cmdPath = (string)changedVariables[variable];
            }
            return new SkylineSettings(type, null, cmdPath);
        }

        public static MainSettings GetTestMainSettings()
        {
            return new MainSettings(SkylineTemplate.ExistingTemplate(GetTestFilePath("emptyTemplate.sky")), GetTestFilePath("analysis"), false, GetTestFilePath("emptyData"), null, string.Empty, null, string.Empty);
        }

        public static FileSettings GetTestFileSettings()
        {
            return new FileSettings(null, null, null, false, false, true);
        }

        public static RefineSettings GetTestRefineSettings()
        {
            return new RefineSettings(new RefineInputObject(), false, false, string.Empty);
        }

        public static ReportSettings GetTestReportSettings()
        {
            var reportList = new List<ReportInfo>{GetTestReportInfo() };
            return new ReportSettings(reportList);
        }

        public static ReportInfo GetTestReportInfo()
        {
            var rVersion = RInstallations.GetMostRecentInstalledRVersion();
            return new ReportInfo("UniqueReport", false, GetTestFilePath("UniqueReport.skyr"),
                new List<Tuple<string, string>> {new Tuple<string, string>(GetTestFilePath("testScript.r"), rVersion)}, new Dictionary<string, PanoramaFile>(), false);
        }

        /// <summary>
        /// Replaces R version references in a line with the current installed version.
        /// Handles both formats: &lt;r_script&gt; and &lt;script_path&gt;.
        /// </summary>
        public static string ReplaceRVersionWithCurrent(string line)
        {
            var rVersion = RInstallations.GetMostRecentInstalledRVersion();
            
            // Replace ONLY R script version references, in both formats:
            // 1. Newer: <r_script path="..." version="X.Y.Z" ...>
            // 2. Older: <script_path>(..., X.Y.Z)</script_path>
            // This avoids replacing xml_version, program version, etc.
            
            // Handle newer <r_script> format
            var updatedLine = Regex.Replace(line, 
                @"(<r_script [^>]*version="")(\d+\.\d+(\.\d+)?)("")", 
                match => match.Groups[1].Value + rVersion + match.Groups[4].Value);
            
            // Handle older <script_path> format
            updatedLine = Regex.Replace(updatedLine, 
                @"(<script_path>\([^,)]+,\s*)(\d+\.\d+(\.\d+)?)(\)</script_path>)", 
                match => match.Groups[1].Value + rVersion + match.Groups[4].Value);
            
            return updatedLine;
        }

        /// <summary>
        /// Creates a FileSaver for a temporary copy of a .bcfg file with all R version references
        /// replaced with the most recent installed R version.
        /// The caller should dispose the FileSaver when done (auto-cleanup on dispose).
        /// </summary>
        public static FileSaver CreateBcfgWithCurrentRVersion(string originalBcfgPath)
        {
            // Create temp file in the same folder as the original
            // In theory, this FileSaver would overwrite the original if it were ever committed,
            // but it will never be committed.
            var fileSaver = new FileSaver(originalBcfgPath);
            
            // Apply R version replacement using the general-purpose transform function
            CopyFileWithLineTransformAbsolute(originalBcfgPath, ReplaceRVersionWithCurrent, fileSaver.SafeName);
            
            return fileSaver;
        }

        public static SkylineSettings GetTestSkylineSettings()
        {
            return new SkylineSettings(SkylineType.Custom, null, GetSkylineDir());
        }

        public static SkylineBatchConfig GetTestConfig(string name = "name")
        {
            return new SkylineBatchConfig(name, true, false, DateTime.MinValue, GetTestMainSettings(), GetTestFileSettings(), 
                GetTestRefineSettings(), GetTestReportSettings(), GetTestSkylineSettings());
        }

        public static SkylineBatchConfig GetTestDependentConfig(string name, SkylineBatchConfig baseConfig)
        {
            if (string.IsNullOrEmpty(baseConfig.RefineSettings.OutputFilePath))
                throw new Exception("Config does not have refine output path and will not create dependency");
            var newMainSettings = GetTestMainSettings()
                .UpdateDependent(baseConfig.Name, baseConfig.RefineSettings.OutputFilePath);
            var populatedRefineSettings = new RefineSettings(new RefineInputObject(), true, true, GetTestFilePath("test.sky"));

            return new SkylineBatchConfig(name, true, false, DateTime.MinValue, newMainSettings, GetTestFileSettings(),
                populatedRefineSettings, GetTestReportSettings(), GetTestSkylineSettings());
        }

        public static SkylineBatchConfig GetFullyPopulatedConfig(string name = "TestConfig")
        {
            var main = new MainSettings(SkylineTemplate.ExistingTemplate(GetTestFilePath("emptyTemplate.sky")), GetTestFilePath("analysis"), false,
                GetTestFilePath("emptyData"), null, GetTestFilePath("fakeAnnotations.csv"), null, "testNamingPattern");
            var file = FileSettings.FromUi("5", "4", "3", true, true, true);
            var refine = new RefineSettings(new RefineInputObject() 
                {
                    cv_remove_above_cutoff = 20,
                    cv_global_normalize = RefineInputObject.CvGlobalNormalizeValues.equalize_medians,
                    qvalue_cutoff = 0.01,
                    cv_transitions_count = 2
                },  false, false, GetTestFilePath("RefineOutput.sky"));

            var reportList = new List<ReportInfo>();
            var rVersion = RInstallations.GetMostRecentInstalledRVersion();
            var script = new List<Tuple<string, string>>()
                {new Tuple<string, string>(GetTestFilePath("testScript.R"), rVersion)};
            reportList.Add(new ReportInfo("Unique Report", false, GetTestFilePath("uniqueReport.skyr"), script, new Dictionary<string, PanoramaFile>(), false));
            reportList.Add(new ReportInfo("Another Unique Report", true, GetTestFilePath("uniqueReport.skyr"), script, new Dictionary<string, PanoramaFile>(), true));
            var reports = new ReportSettings(reportList);
            var skyline = GetTestSkylineSettings();
            return new SkylineBatchConfig(name, true, false, DateTime.Now, main, file, refine, reports, skyline);
        }

        public static List<IConfig> ConfigListFromNames(List<string> names)
        {
            var configList = new List<IConfig>();
            foreach (var name in names)
            {
                configList.Add(GetTestConfig(name));
            }
            return configList;
        }

        public static string GetSkylineDir()
        {
            return GetProjectDirectory("bin\\x64\\Release");
        }

        public static string GetProjectDirectory(string relativePath)
        {
            for (String directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                directory != null && directory.Length > 10;
                directory = Path.GetDirectoryName(directory))
            {
                if (File.Exists(Path.Combine(directory, "Skyline.sln")))
                    return Path.Combine(directory, relativePath);
            }

            return null;
        }

        /// <summary>
        /// Gets the TestResults path for a specific test.
        /// Creates a test-specific folder similar to Skyline's ExtensionTestContext.GetTestResultsPath().
        /// </summary>
        public static string GetTestResultsPath(TestContext testContext, string relativePath = null)
        {
            // Build path similar to Skyline: <project_root>/TestResults/<TestName>/<relativePath>
            var testResultsDir = Path.Combine(GetProjectDirectory("TestResults"), testContext.TestName);
            if (!string.IsNullOrEmpty(relativePath))
                testResultsDir = Path.Combine(testResultsDir, relativePath);
            
            return Path.GetFullPath(testResultsDir);
        }

        public static Logger GetTestLogger(TestContext testContext, string logSubfolder = "")
        {
            // Use test-specific folder in TestResults
            var logFolder = string.IsNullOrEmpty(logSubfolder)
                ? GetTestResultsPath(testContext, "Logs")
                : GetTestResultsPath(testContext, logSubfolder);

            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);

            var logName = "TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log";
            return new Logger(Path.Combine(logFolder, logName), logName, true);
        }

        public static void InitializeRInstallation()
        {
            Assert.IsTrue(RInstallations.FindRDirectory());
        }

        public static void InitializeSettingsImportExport()
        {
            ConfigList.Importer = SkylineBatchConfig.ReadXml;
            ConfigList.XmlVersion = SkylineBatch.Properties.Settings.Default.XmlVersion;
        }

        public static List<string> GetAllLogFiles(TestContext testContext, string directory = null)
        {
            directory ??= GetTestResultsPath(testContext, "TestTinyLog");

            var files = Directory.GetFiles(directory);
            var logFiles = new List<string>();
            foreach (var fullName in files)
            {
                var file = Path.GetFileName(fullName);
                if (file.EndsWith(".log"))
                    logFiles.Add(fullName);
            }
            return logFiles;
        }

        public static void CompareFiles(string expectedFilePath, string actualFilePath, List<Regex> skipLines = null)
        {
            if (skipLines == null) skipLines = new List<Regex>();
            using (var expectedReader = new StreamReader(expectedFilePath))
            using (var actualReader = new StreamReader(actualFilePath))
            {
                int line = 1;
                while (line < 1000)
                {
                    if (expectedReader.EndOfStream != actualReader.EndOfStream)
                        Assert.Fail($"Line {line}: Expected end of stream value to be {expectedReader.EndOfStream} but instead was {actualReader.EndOfStream}.");
                    var expectedLine = expectedReader.ReadLine();
                    var actualLine = actualReader.ReadLine();
                    if (expectedLine == null || actualLine == null)
                    {
                        Assert.IsTrue(expectedLine == actualLine,
                            actualFilePath + Environment.NewLine +
                            $"Line {line}: Expected reached end of file to be {expectedLine == null} but instead was {actualLine == null}.");
                        return;
                    }
                    if (!expectedLine.Equals(actualLine))
                    {
                        var fail = true;
                        foreach (var pattern in skipLines)
                        {
                            //var a = pattern.Match(expectedLine).Groups;
                            //var b = pattern.Match(actualLine).Groups;
                            if (pattern.IsMatch(expectedLine) && pattern.IsMatch(actualLine) &&
                                pattern.Match(expectedLine).Groups[1].Value.Equals(pattern.Match(actualLine).Groups[1].Value))
                                fail = false;
                        }
                        if (fail)
                        {
                            Assert.IsTrue(expectedLine.Equals(actualLine),
                                                 actualFilePath + Environment.NewLine +
                                                 $"Line {line} does not match" + Environment.NewLine +
                                                "Expected:" + Environment.NewLine +
                                                expectedLine + Environment.NewLine +
                                                "Actual:" + Environment.NewLine +
                                                actualLine);
                        }
                    }
                    line++;
                }
                throw new Exception("Test Error: should never reach 1000 lines");
            }
        }

        /// <summary>
        /// Copies a file from the test directory, replacing all occurrences of a string.
        /// </summary>
        public static string CopyFileFindReplace(string fileName, string stringToBeReplaced, string replacementString, string newName)
        {
            // String.Replace() replaces all occurrences - no while loop needed
            return CopyFileWithLineTransform(fileName,
                line => line.Replace(stringToBeReplaced, replacementString),
                newName);
        }

        /// <summary>
        /// Copies a file from the test directory, applying a transformation function to each line.
        /// </summary>
        /// <param name="fileName">Source file name (relative to test directory)</param>
        /// <param name="lineTransform">Function to transform each line (input line -> output line)</param>
        /// <param name="newName">Destination file path (required)</param>
        /// <returns>Path to the created file</returns>
        public static string CopyFileWithLineTransform(string fileName, Func<string, string> lineTransform, string newName)
        {
            var originalFilePath = GetTestFilePath(fileName);
            return CopyFileWithLineTransformAbsolute(originalFilePath, lineTransform, newName);
        }

        /// <summary>
        /// Copies a file (absolute path), applying a transformation function to each line.
        /// </summary>
        private static string CopyFileWithLineTransformAbsolute(string sourceFilePath, Func<string, string> lineTransform, string destFilePath)
        {
            if (File.Exists(destFilePath))
                File.Delete(destFilePath);

            using (var fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read))
            using (var writeStream = File.OpenWrite(destFilePath))
            using (var streamReader = new StreamReader(fileStream))
            using (var streamWriter = new StreamWriter(writeStream))
            {
                while (!streamReader.EndOfStream)
                {
                    var line = streamReader.ReadLine();
                    if (line == null) continue;
                    var transformedLine = lineTransform(line);
                    streamWriter.WriteLine(transformedLine);
                }
            }
            return destFilePath;
        }
    }
}

