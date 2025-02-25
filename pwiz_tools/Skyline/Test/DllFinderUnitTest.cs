/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class DllFinderUnitTest : AbstractUnitTest
    {
        /// <summary>
        /// Uses registry and file configurations described in JSON to test finding
        /// and copying Thermo method export DLLs.
        /// </summary>
        [TestMethod]
        public void ThermoDllFinderTest()
        {
            var clazz = typeof(DllFinderUnitTest);
            using var stream = clazz.Assembly.GetManifestResourceStream(clazz, "DllFinderUnitTest.json");
            Assert.IsNotNull(stream);
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            var testCases = JsonConvert.DeserializeObject<List<ThermoDllFinderTestCase>>(json);
            foreach (var testCase in testCases)
            {
                try
                {
                    if (testCase.ExpectException.HasValue)
                    {
                        AssertEx.ThrowsException<IOException>(() => RunTest(testCase),
                            GetExceptionMessage(testCase.ExpectException.Value));
                    }
                    else
                    {
                        AssertEx.NoExceptionThrown<Exception>(() => RunTest(testCase));
                    }
                }
                catch (Exception e)
                {
                    throw new AssertFailedException(TextUtil.LineSeparate($"Test case failed '{testCase.TestName}'",
                        e.ToString()));
                }
            }
        }

        private void RunTest(ThermoDllFinderTestCase testCase)
        {
            var testServices = new TestDllFinderServices(testCase);

            var finder = new ThermoDllFinder(testServices); // May throw
            finder.EnsureDlls();    // May throw

            VerifyCopiedDlls(testCase, finder, testServices);

            var softwareInfo = finder.GetSoftwareInfo();
            Assert.AreEqual(testCase.ExpectedInstrumentType, softwareInfo.InstrumentType);
            Assert.AreEqual(testCase.ExpectedVersion, softwareInfo.Version);
            if (testCase.ExpectedCopyCount > 0)
            {
                Assert.IsNotNull(softwareInfo.Path);
                Assert.IsTrue(testServices.DirectoryExists(softwareInfo.Path), $"The directory {softwareInfo.Path} does not exist");
            }
        }

        private string GetExceptionMessage(int testCaseExpectException)
        {
            switch (testCaseExpectException)
            {
                case 0:
                    return ModelResources.ThermoMassListExporter_EnsureLibraries_Thermo_method_creation_software_may_not_be_installed_correctly_;
                case 1:
                    return ModelResources.ThermoMassListExporter_EnsureLibraries_Failed_to_find_a_valid_Thermo_instrument_installation_;
                case 2:
                    // CONSIDER: Only need to test once, but this feels like it should be more flexible
                    return string.Format(ModelResources.ThermoMassListExporter_EnsureLibraries_Thermo_instrument_software_may_not_be_installed_correctly__The_library__0__could_not_be_found_,
                        "C:\\Thermo\\instrumentSoftwarePath\\Thermo.TNG.MethodXMLInterface.dll");
            }

            return null;
        }

        private void VerifyCopiedDlls(ThermoDllFinderTestCase testCase, ThermoDllFinder finder,
            TestDllFinderServices testServices)
        {
            string destDir = finder.DestinationDir;
            if (testCase.RegistrySubKeys == null)
            {
                // If no dependency keys then it should only be possible to succeed if
                // the test started with all the required files in place
                Assert.IsTrue(finder.ContainsDependencyLibraries(destDir));
                return;
            }

            // Should end up with dependent libraries in the destination folder
            foreach (var dependencyLibrary in ThermoDllFinder.AllDependencyLibraries)
            {
                string fileName = dependencyLibrary.DllFileName;
                var sourceFile = GetTestFile(testCase, fileName, destDir, false);

                // If not a required library and the file was not present, continue
                if (!dependencyLibrary.IsRequired && sourceFile == null)
                    continue;

                Assert.IsNotNull(sourceFile, $"Missing required source file {fileName}");

                bool copied = testServices.CopiedFiles.Any(f => Equals(f.Src, sourceFile.Path));
                if (!copied)
                {
                    var destFile = GetTestFile(testCase, fileName, destDir, true);
                    Assert.IsNotNull(destFile, $"Dependency file {fileName} not copied unexpectedly.");
                    Assert.AreEqual(destFile.LastWriteTime, sourceFile.LastWriteTime, $"Dependency file {fileName} not overwritten unexpectedly.");
                }
            }

            // All files in the test that are not starting in the destination folder are expected to be copied
            Assert.AreEqual(testCase.ExpectedCopyCount ?? testCase.Files.Count(f => !Equals(destDir, Path.GetDirectoryName(f.Path))),
                testServices.CopiedFiles.Count);
        }

        private static FileData GetTestFile(ThermoDllFinderTestCase testCase, string fileName, string destDir, bool inDest)
        {
            var sourceFile = testCase.Files.FirstOrDefault(f => 
                Equals(fileName, Path.GetFileName(f.Path)) &&
                Equals(destDir, Path.GetDirectoryName(f.Path)) == inDest);
            return sourceFile;
        }

        /// <summary>
        /// A test double (mock/stub) for IDllFinderServices that returns data
        /// from the ThermoDllFinderTestCase instance instead of the real filesystem/registry.
        /// </summary>
        public class TestDllFinderServices : IDllFinderServices
        {
            private readonly ThermoDllFinderTestCase _testCase;
            private readonly Dictionary<string, FileData> _filesByPath;
            private readonly TestRegistryKey _rootKey;

            public TestDllFinderServices(ThermoDllFinderTestCase testCase)
            {
                _testCase = testCase;

                // Build a dictionary of file path => FileData for quick lookups
                _filesByPath = testCase.Files
                    .ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);

                // Create the root registry key structure in memory
                _rootKey = new TestRegistryKey(string.Empty, testCase.RegistrySubKeys ?? new List<RegistrySubKeyData>());
            }

            public string GetSkylineExePath()
            {
                return _testCase.SkylineExePath;
            }

            public bool Exists(string path)
            {
                // Return whether the file was defined as existing in the test data
                return _filesByPath.TryGetValue(path, out var f);
            }

            public bool DirectoryExists(string path)
            {
                return _filesByPath.Keys.Any(p => path.Equals(Path.GetDirectoryName(p)));
            }

            public DateTime GetLastWriteTime(string path)
            {
                // Return stored LastWriteTime, or DateTime.MinValue if not found
                if (_filesByPath.TryGetValue(path, out var f))
                {
                    return f.LastWriteTime ?? DateTime.Now;
                }
                return DateTime.MinValue;
            }

            // Keep a record of all Copy calls
            public List<CopiedFile> CopiedFiles { get; } = new List<CopiedFile>();

            public struct CopiedFile
            {
                public CopiedFile(string src, string dest)
                {
                    Src = src;
                    Dest = dest;
                }

                public string Src { get; }
                public string Dest { get; }
            }

            public void Copy(string srcFile, string destFile)
            {
                // Throw an exception if trying to copy a non-existent file
                if (!_filesByPath.TryGetValue(srcFile, out var f))
                    throw new FileNotFoundException($"Source file not found: {srcFile}");

                // Store the fact that this copy happened.
                CopiedFiles.Add(new CopiedFile(srcFile, destFile));
            }

            public void Delete(string file)
            {
                // Throw an exception if trying to copy a non-existent file
                if (!_filesByPath.TryGetValue(file, out var f))
                    throw new FileNotFoundException($"Deleted file not found: {file}");

                // Store the fact that this file deletion happened.
                CopiedFiles.Add(new CopiedFile(file, string.Empty));
            }

            public IDisposable GetRootKey()
            {
                return _rootKey;
            }

            public IDisposable OpenSubKey(IDisposable key, string subKeyName)
            {
                if (key is TestRegistryKey testKey)
                {
                    return testKey.OpenSubKey(subKeyName);
                }
                return null;
            }

            public string[] GetSubKeyNames(IDisposable key)
            {
                if (key is TestRegistryKey testKey)
                {
                    return testKey.GetSubKeyNames();
                }
                return Array.Empty<string>();
            }

            public string GetValue(IDisposable key, string subKeyName)
            {
                if (key is TestRegistryKey testKey)
                {
                    return testKey.GetValue(subKeyName);
                }
                return null;
            }
        }

        /// <summary>
        /// A simple in-memory implementation that mimics a registry key hierarchy.
        /// </summary>
        internal class TestRegistryKey : IDisposable
        {
            private readonly string _keyName;
            private readonly List<TestRegistryKey> _subKeys = new List<TestRegistryKey>();
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public TestRegistryKey(string keyName, List<RegistrySubKeyData> subKeyDataList)
            {
                _keyName = keyName;

                if (subKeyDataList != null)
                {
                    foreach (var subData in subKeyDataList)
                    {
                        var subKey = new TestRegistryKey(subData.SubKeyName, subData.SubKeys);

                        // If the JSON provides direct values, set them
                        if (!string.IsNullOrEmpty(subData.InstallType))
                            subKey._values["InstallType"] = subData.InstallType;
                        if (!string.IsNullOrEmpty(subData.ProgramPath))
                            subKey._values["ProgramPath"] = subData.ProgramPath;

                        _subKeys.Add(subKey);
                    }
                }
            }

            public TestRegistryKey(string keyName)
            {
                _keyName = keyName;
            }

            public TestRegistryKey OpenSubKey(string subKeyName)
            {
                // Return the child with matching SubKeyName
                return _subKeys.FirstOrDefault(s => s._keyName.Equals(subKeyName, StringComparison.OrdinalIgnoreCase));
            }

            public string[] GetSubKeyNames()
            {
                return _subKeys.Select(s => s._keyName).ToArray();
            }

            public string GetValue(string valueName)
            {
                if (_values.TryGetValue(valueName, out var val))
                    return val;
                return null;
            }

            public void Dispose()
            {
                // No resources to free
            }
        }

        public class ThermoDllFinderTestCase
        {
            public string TestName { get; set; }
            public string SkylineExePath { get; set; }
            public List<FileData> Files { get; set; }
            public List<RegistrySubKeyData> RegistrySubKeys { get; set; }
            public int? ExpectException { get; set; }
            public int? ExpectedCopyCount { get; set; }
            public string ExpectedInstrumentType { get; set; }
            public double ExpectedVersion { get; set; }
        }

        public class FileData
        {
            public string Path { get; set; }
            public DateTime? LastWriteTime { get; set; }
        }

        public class RegistrySubKeyData
        {
            public string SubKeyName { get; set; }

            // Optional values necessary on the leaf node
            public string InstallType { get; set; }
            public string ProgramPath { get; set; }

            // Nested sub-keys
            public List<RegistrySubKeyData> SubKeys { get; set; }
        }
    }
}
