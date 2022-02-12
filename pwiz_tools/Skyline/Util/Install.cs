/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace pwiz.Skyline.Util
{
    public static class Install
    {
        static Install()
        {
            var assembly = typeof(Program).Assembly;
            try
            {
                string productVersion;
                var versionAttribute = assembly.GetCustomAttributes(false)
                    .OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault();
                if (versionAttribute != null)
                {
                    productVersion = versionAttribute.InformationalVersion;
                    if (productVersion.Contains(@"(developer build)"))
                    {
                        IsDeveloperInstall = true;
                        productVersion = productVersion.Replace(@"(developer build)", "").Trim();
                    }
                    else if (productVersion.Contains(@"(automated build)"))
                    {
                        IsAutomatedBuild = true;
                        productVersion = productVersion.Replace(@"(automated build)", "").Trim();
                    }
                }
                else
                {
                    // win32 version info
                    productVersion = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion?.Trim();
                }

                Version = productVersion;
            }
            catch (Exception)
            {
                Version = string.Empty;
            }

        }
        public enum InstallType { release, daily, developer }

        public static InstallType Type
        {
            get
            {
                if (IsDeveloperInstall)
                {
                    return InstallType.developer;
                }
                return Build == 0 ? InstallType.release : InstallType.daily;
            }
        }

        public static bool IsDeveloperInstall { get; }
        public static bool IsAutomatedBuild { get; private set; }

        public static bool Is64Bit
        {
            get
            {
                var myAssemblyLocation = Assembly.GetExecutingAssembly().Location;
                // ReSharper disable once AssignNullToNotNullAttribute
                var myAssemblyName = AssemblyName.GetAssemblyName(myAssemblyLocation);
                return ProcessorArchitecture.MSIL == myAssemblyName.ProcessorArchitecture;
            }
        }

        public static string BitsText
        {
            get { return Is64Bit ? @"64" : @"32"; }
        }

        public static int MajorVersion
        {
            get { return VersionPart(0); }
        }

        public static int MinorVersion
        {
            get { return VersionPart(1); }
        }

        public static int Build
        {
            get { return VersionPart(2); }
        }

        public static int Revision
        {
            get { return VersionPart(3); }
        }

        public static string GitHash
        {
            get
            {
                var parts = Version.Split('-');
                return parts.Length > 1 ? parts[1] : string.Empty;
            }
        }

        public static string Version
        {
            get;
        }

        private static int VersionPart(int index)
        {
            string[] versionParts = Version.Split('-')[0].Split('.');
            return (versionParts.Length > index ? Convert.ToInt32(versionParts[index]) : 0);
        }

        public static string Url32
        {
            get
            {
                return Type == InstallType.release
                    ? @"https://skyline.ms/skyline32.url"
                    : Type == InstallType.daily
                        ? @"https://skyline.ms/skyline-daily32.url"
                        : string.Empty;
            }
        }

        public static string ProgramNameAndVersion
        {
            get
            {
                return string.Format(@"{0} ({1}-bit{2}{3}) {4}",
                                     Program.Name, BitsText,
                                    (IsDeveloperInstall ? @" : developer build" : string.Empty),
                                    (IsAutomatedBuild ? @" : automated build" : string.Empty),
                                     Regex.Replace(Version, @"(\d+\.\d+\.\d+\.\d+)-(\S+)", "$1 ($2)"));
            } 
        }
    }
}
