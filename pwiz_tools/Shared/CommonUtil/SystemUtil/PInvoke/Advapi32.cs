/*
 * Copyright 2024 University of Washington - Seattle, WA
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

using System.Runtime.InteropServices;
using System.Text;
using System;

namespace pwiz.Common.SystemUtil.PInvoke
{
    // CONSIDER: can this whole class be replaced with .NET's built-in Registry API?
    //
    // ReSharper disable once IdentifierTypo
    public static class Advapi32
    {
        // ReSharper disable InconsistentNaming IdentifierTypo
        private static readonly UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);

        private const int KEY_READ = 0x20019;
        private const int KEY_WOW64_32KEY = 0x0200;
        // ReSharper restore InconsistentNaming IdentifierTypo

        public static string GetPathFromProgId(string progId)
        {
            var clsid = RegQueryKeyValue(@"SOFTWARE\Classes\" + progId + @"\CLSID", string.Empty);
            if (clsid == null)
                return null;
            return RegQueryKeyValue(@"SOFTWARE\Classes\CLSID\" + clsid + @"\InprocServer32", string.Empty);
        }

        public static string RegQueryKeyValue(string path, string valueName)
        {
            return RegQueryKeyValue(HKEY_LOCAL_MACHINE, path, valueName);
        }

        private static string RegQueryKeyValue(UIntPtr hKey, string path, string valueName)
        {
            UIntPtr hKeyQuery;
            if (RegOpenKeyEx(hKey, path, 0, KEY_READ, out hKeyQuery) != 0)
            {
                if (RegOpenKeyEx(hKey, path, 0, KEY_READ | KEY_WOW64_32KEY, out hKeyQuery) != 0)
                    return null;
            }

            uint size = 1024;
            var sb = new StringBuilder(1024);

            try
            {
                if (RegQueryValueEx(hKeyQuery, valueName, 0, out _, sb, ref size) != 0)
                    return null;
            }
            finally
            {
                RegCloseKey(hKeyQuery);
            }
            return sb.ToString();
        }

        // ReSharper disable once StringLiteralTypo
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern int RegOpenKeyEx(
            UIntPtr hKey,
            string subKey,
            int ulOptions,
            int samDesired,
            out UIntPtr hkResult);

        // ReSharper disable once StringLiteralTypo
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = @"RegQueryValueExW", SetLastError = true)]
        private static extern int RegQueryValueEx(
            UIntPtr hKey,
            string lpValueName,
            int lpReserved,
            out uint lpType,
            StringBuilder lpData,
            ref uint lpcbData);

        // ReSharper disable once StringLiteralTypo
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(UIntPtr hKey);
    }
}