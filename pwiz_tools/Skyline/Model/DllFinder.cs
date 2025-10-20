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

using Microsoft.Win32;
using pwiz.Common.Collections;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace pwiz.Skyline.Model
{
    public interface IDllFinderServices
    {
        string GetSkylineExePath();
        bool Exists(string path);
        DateTime GetLastWriteTime(string path);
        void Copy(string srcFile, string destFile);
        void Delete(string file);

        IDisposable GetRootKey();
        IDisposable OpenSubKey(IDisposable key, string subKeyName);
        string[] GetSubKeyNames(IDisposable key);
        string GetValue(IDisposable key, string subKeyName);
    }

    internal class DllFinderSystemServices : IDllFinderServices
    {
        private string _rootKeyPath;

        public DllFinderSystemServices(string rootKeyPath)
        {
            _rootKeyPath = rootKeyPath;
        }

        public string GetSkylineExePath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public DateTime GetLastWriteTime(string path)
        {
            return File.GetLastWriteTime(path);
        }

        public void Copy(string srcFile, string destFile)
        {
            File.Copy(srcFile, destFile, true);
        }

        public void Delete(string file)
        {
            File.Delete(file);
        }

        public IDisposable GetRootKey()
        {
            return Registry.LocalMachine.OpenSubKey(_rootKeyPath);
        }

        public IDisposable OpenSubKey(IDisposable key, string subKeyName)
        {
            return (key as RegistryKey)?.OpenSubKey(subKeyName);
        }

        public string[] GetSubKeyNames(IDisposable key)
        {
            return (key as RegistryKey)?.GetSubKeyNames();
        }

        public string GetValue(IDisposable key, string subKeyName)
        {
            return (key as RegistryKey)?.GetValue(subKeyName)?.ToString();
        }
    }

    public struct DllDependency
    {
        public DllDependency(string dllFileName, bool isRequired)
        {
            DllFileName = dllFileName;
            IsRequired = isRequired;
        }

        public string DllFileName { get; }
        public bool IsRequired { get; }
    }

    public class ThermoDllFinder
    {
        // Can be changed in test mode to avoid reading from the registry
        public static IDllFinderServices DEFAULT_SERVICES = new DllFinderSystemServices(ROOT_KEY_PATH);

        public ThermoDllFinder(IDllFinderServices services = null)
        {
            Services = services ?? DEFAULT_SERVICES;

            var rootExeDir = Services.GetSkylineExePath();
            if (!string.IsNullOrEmpty(rootExeDir))
                rootExeDir = Path.GetDirectoryName(rootExeDir);
            if (string.IsNullOrEmpty(rootExeDir))
                throw new IOException(ModelResources.ThermoMassListExporter_EnsureLibraries_Thermo_method_creation_software_may_not_be_installed_correctly_);

            DestinationDir = Path.Combine(rootExeDir, ThermoMassListExporter.ExeBuildRelativePath);
        }

        public string DestinationDir { get; }

        public IDllFinderServices Services { get; }

        public const string ROOT_KEY_PATH = @"SOFTWARE\Wow6432Node\Thermo Instruments\TNG";

        private static readonly string[] DEPENDENCY_LIBRARIES =
        {
            @"Thermo.TNG.MethodXMLFactory.dll",
            @"Thermo.TNG.MethodXMLInterface.dll"
        };

        private static readonly string[] OPTIONAL_DEPENDENCY_LIBRARIES =
        {
            @"Thermo.TNG.MethodXMLInterface2.dll"
        };

        public void EnsureDlls()
        {
            // ReSharper disable ConstantNullCoalescingCondition
            var info = GetSoftwareInfo();
            if (info.Path == null)
            {
                // If all the required libraries exist, then continue even if Thermo installation is gone.
                if (!ContainsDependencyLibraries(DestinationDir))
                    throw new IOException(TextUtil.LineSeparate(ModelResources.ThermoMassListExporter_EnsureLibraries_Failed_to_find_a_valid_Thermo_instrument_installation_, info.FailureReason));
                return;
            }

            // ReSharper restore ConstantNullCoalescingCondition
            foreach (var dllDependency in AllDependencyLibraries)
            {
                string library = dllDependency.DllFileName;
                string srcFile = Path.Combine(info.Path, library);
                string destFile = Path.Combine(DestinationDir, library);
                bool srcExists = Services.Exists(srcFile);
                if (!srcExists)
                {
                    if (!dllDependency.IsRequired)
                    {
                        // Avoid a mismatch between required and optional DLLs
                        if (Services.Exists(destFile))
                            Services.Delete(destFile);
                        continue;
                    }

                    throw new IOException(
                        string.Format(ModelResources.ThermoMassListExporter_EnsureLibraries_Thermo_instrument_software_may_not_be_installed_correctly__The_library__0__could_not_be_found_,
                            srcFile));
                }
                // If destination file does not exist or has a different modification time from
                // the source, then copy the source file from the installation.
                if (!Services.Exists(destFile) || !Equals(Services.GetLastWriteTime(destFile), Services.GetLastWriteTime(srcFile)))
                    Services.Copy(srcFile, destFile);
            }
        }

        public bool ContainsDependencyLibraries(string dir)
        {
            return MissingDependencyLibraries(dir).IsNullOrEmpty();
        }

        public IEnumerable<string> MissingDependencyLibraries(string dir)
        {
            return DEPENDENCY_LIBRARIES.Where(libraryName => !Services.Exists(Path.Combine(dir, libraryName)));
        }

        public static IEnumerable<DllDependency> AllDependencyLibraries
        {
            get
            {
                foreach (var lib in DEPENDENCY_LIBRARIES)
                    yield return new DllDependency(lib, true);
                foreach (var lib in OPTIONAL_DEPENDENCY_LIBRARIES)
                    yield return new DllDependency(lib, false);
            }
        }

        public class SoftwareInfo
        {
            public string Path { get; set; }
            public string InstrumentType { get; set; }
            public double Version { get; set; }
            public string FailureReason { get; set; }
        }

        public SoftwareInfo GetSoftwareInfo()
        {
            try
            {
                using var tngKey = Services.GetRootKey();
                return GetSoftwareInfo(tngKey);
            }
            catch (Exception x)
            {
                return new SoftwareInfo { FailureReason = x.ToString() };
            }
        }

        private SoftwareInfo GetSoftwareInfo(IDisposable tngKey)
        {
            var info = new SoftwareInfo();
            if (tngKey == null)
            {
                info.FailureReason = string.Format(ModelResources.ThermoDllFinder_GetSoftwareInfo_The_key__0__was_not_found_in_the_Windows_registry_, ROOT_KEY_PATH);
                return info;
            }

            var listMachineNames = new List<string>();
            foreach (var subKeyName in Services.GetSubKeyNames(tngKey))
            {
                if (Equals(subKeyName, @"DataAccess"))
                    continue;

                listMachineNames.Add(subKeyName);
                using var machineKey = Services.OpenSubKey(tngKey, subKeyName);
                double? version;
                using var versionKey = GetVersionSubKey(machineKey, out version);
                var keyPath = GetMachineProgramPath(versionKey);
                if (keyPath == null)
                    continue;

                // If the path for the key does not contain the necessary DLLs keep looking
                if (!ContainsDependencyLibraries(keyPath.Path))
                {
                    // If no other path found yet, note the missing DLLs as the failure reason
                    if (info.Path == null)
                    {
                        info.FailureReason = TextUtil.LineSeparate(
                            string.Format(ModelResources.ThermoDllFinder_GetSoftwareInfo_The_ProgramPath__0__for_the_instrument__1__is_missing_required_files_,
                                keyPath.Path, subKeyName),
                            TextUtil.LineSeparate(MissingDependencyLibraries(keyPath.Path)));
                    }
                    continue;
                }
                var infoForKey = new SoftwareInfo
                    { Path = keyPath.Path, InstrumentType = subKeyName, Version = version.Value };
                // Return the first full installation with the DLLs
                if (keyPath.IsFullInstall)
                    return infoForKey;
                // Otherwise, if the current best path is null, use this one
                if (info.Path == null)
                    info = infoForKey;
            }

            if (info.Path == null && info.FailureReason == null)
            {
                info.FailureReason = TextUtil.LineSeparate(string.Format(ModelResources.ThermoDllFinder_GetSoftwareInfo_Failed_to_find_a_machine_name_key_with_a_valid_ProgramPath, ROOT_KEY_PATH),
                    TextUtil.LineSeparate(listMachineNames));
            }

            return info;
        }

        private class ProgramPath
        {
            public string Path;         // ProgramPath registry value
            public bool IsFullInstall;  // InstallType == "Full"
        }

        private ProgramPath GetMachineProgramPath(IDisposable versionKey)
        {
            if (versionKey == null)
                return null;
            string programPath = Services.GetValue(versionKey, @"ProgramPath");
            if (string.IsNullOrEmpty(programPath))
                return null;
            var installTypeObject = Services.GetValue(versionKey, @"InstallType");
            bool full = installTypeObject is @"Full";
            return new ProgramPath { Path = programPath, IsFullInstall = full };
        }

        private IDisposable GetVersionSubKey(IDisposable parentKey, out double? version)
        {
            version = null;
            if (parentKey == null)
                return null;
            var subKeyNames = Services.GetSubKeyNames(parentKey);
            string maxName = null;
            double? maxValue = null;
            foreach (var name in subKeyNames)
            {
                double versionNum;
                if (double.TryParse(name, NumberStyles.Float, CultureInfo.InvariantCulture, out versionNum))
                {
                    if (!maxValue.HasValue || maxValue.Value < versionNum)
                    {
                        maxValue = versionNum;
                        maxName = name;
                    }
                }
            }

            if (!maxValue.HasValue)
                return null;
            version = maxValue;
            return Services.OpenSubKey(parentKey, maxName);
        }
    }
}
