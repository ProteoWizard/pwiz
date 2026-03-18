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

using System;
using System.Runtime.InteropServices;

namespace TestRunnerLib.PInvoke
{
    public static class Shell32Test
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public const int GPS_READWRITE = 2;
        public const ushort VT_LPWSTR = 31;

        public static PropertyKey PKEY_Title = new PropertyKey
        {
            fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"),
            pid = 2
        };

        public static PropertyKey PKEY_Comment = new PropertyKey
        {
            fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"),
            pid = 6
        };

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHGetPropertyStoreFromParsingName(
            string pszPath, IntPtr pbc, int flags, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertyStore
        {
            int GetCount(out uint cProps);
            int GetAt(uint iProp, out PropertyKey key);
            int GetValue(ref PropertyKey key, out PropVariant pv);
            int SetValue(ref PropertyKey key, ref PropVariant pv);
            int Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PropertyKey
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PropVariant
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr pwszVal;
        }
    }
}
