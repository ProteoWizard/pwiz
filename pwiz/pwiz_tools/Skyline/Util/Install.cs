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
using System.Deployment.Application;
using System.IO;

namespace pwiz.Skyline.Util
{
    class Install
    {
        public enum InstallType { release, daily, developer }

        public static InstallType Type
        {
            get
            {
                return
                    (!ApplicationDeployment.IsNetworkDeployed)
                        ? InstallType.developer
                        : (Build == 0)
                              ? InstallType.release
                              : InstallType.daily;
            }
        }

        public static bool Is64Bit
        {
            // CONSIDER: a better way to determine whether this is a 64-bit build
            get { return File.Exists("fileio_x64.dll"); }
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

        public static string Version
        {
            get { return ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString(); }
        }

        private static int VersionPart(int index)
        {
            string[] versionParts = Version.Split('.');
            return (versionParts.Length > index ? Convert.ToInt32(versionParts[index]) : 0);
        }

        public static string Url
        {
            get
            {
                return
                    (Type == InstallType.release)
                        ? string.Format("http://proteome.gs.washington.edu/software/Skyline/install.html?majorVer={0}&minorVer={1}", MajorVersion, MinorVersion)
                        : (Type == InstallType.daily)
                              ? "http://proteome.gs.washington.edu/software/Skyline/install-daily.html"
                              : "";
            }
        }
    }
}
