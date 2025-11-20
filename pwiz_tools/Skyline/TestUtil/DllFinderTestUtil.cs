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
using Newtonsoft.Json;
using pwiz.Skyline.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pwiz.SkylineTestUtil
{
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
            if (testCase.Files == null)
                _filesByPath = new Dictionary<string, FileData>();
            else
            {
                _filesByPath = testCase.Files
                    .ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
            }

            // Create the root registry key structure in memory
            if (testCase.RegistrySubKeys != null)
                _rootKey = new TestRegistryKey(string.Empty, testCase.RegistrySubKeys);
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
        public int? ExpectFailureReason { get; set; }
        public int? ExpectedCopyCount { get; set; }
        public string ExpectedInstrumentType { get; set; }
        public double ExpectedVersion { get; set; }

        public IDllFinderServices DllFinderServices => new TestDllFinderServices(this);

        public static IList<ThermoDllFinderTestCase> LoadAll(Stream stream)
        {
            Assert.IsNotNull(stream);
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<List<ThermoDllFinderTestCase>>(json);
        }
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
