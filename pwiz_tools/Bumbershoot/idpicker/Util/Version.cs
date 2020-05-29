//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace IDPicker
{
    public static partial class Util
    {
        public static string Version { get { return GetAssemblyVersion(Assembly.GetCallingAssembly()); } }
        public static DateTime LastModified { get { return GetAssemblyLastModified(Assembly.GetCallingAssembly().GetName()); } }

        public static AssemblyName GetAssemblyByName (string assemblyName)
        {
            if (Assembly.GetCallingAssembly().GetName().FullName.Contains(assemblyName))
                return Assembly.GetCallingAssembly().GetName();

            foreach (AssemblyName a in Assembly.GetCallingAssembly().GetReferencedAssemblies())
            {
                if (a.FullName.Contains(assemblyName + ','))
                    return a;
            }
            return null;
        }

        public static string GetAssemblyVersion (Assembly assembly)
        {
            // custom attribute
            object[] attrs = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
            // Play it safe with a null check no matter what ReSharper thinks
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (attrs != null && attrs.Length > 0)
            {
                return ((AssemblyInformationalVersionAttribute) attrs[0]).InformationalVersion;
            }
            else
            {
                // win32 version info
                //productVersion = FileVersionInfo.GetVersionInfo(entryAssembly.Location).ProductVersion?.Trim();
                Match versionMatch = Regex.Match(assembly.ToString(), @"Version=([\d.]+)");
                return versionMatch.Groups[1].Success ? versionMatch.Groups[1].Value : "unknown";
            }
        }

        public static DateTime GetAssemblyLastModified (AssemblyName assembly)
        {
            return File.GetLastWriteTime(Assembly.ReflectionOnlyLoad(assembly.FullName).Location);
        }
    }
}