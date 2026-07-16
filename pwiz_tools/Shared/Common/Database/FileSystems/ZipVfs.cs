/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using pwiz.Common.Database;
using pwiz.Common.Properties;

namespace pwiz.Common.Database.FileSystems
{
    /// <summary>
    /// Opens a SQLite database (e.g. a .blib spectral library) that is stored UNCOMPRESSED at a
    /// byte range inside a .zip, in place, without extracting it. This is done with the native
    /// loadable extension "slicevfs.dll", which registers a read-only "slicevfs" VFS that exposes
    /// only the slice of the file's bytes at a given offset and length. The offset and length are
    /// passed as URI parameters.
    /// </summary>
    public static class ZipVfs
    {
        public const string VFS_NAME = "slicevfs";
        private const string EXTENSION_DLL = "slicevfs.dll";
        private const string EXTENSION_INIT = "sqlite3_slicevfs_init";

        private static readonly object RegisterLock = new object();
        private static bool _registered;

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        /// <summary>
        /// Opens a SQLite connection to a database that may be an ordinary file or stored
        /// uncompressed inside a .zip. If the path is inside a .zip, it is opened read-only in place
        /// through the zip VFS; otherwise it is opened normally.
        /// </summary>
        public static SQLiteConnection OpenConnection(string path)
        {
            if (new FilePath(path).TryGetZipByteRange(out var zipFilePath, out var dataOffset, out var length))
                return OpenConnection(zipFilePath, dataOffset, length);
            return SqliteOperations.OpenConnection(path);
        }

        /// <summary>
        /// Opens a read-only connection to a SQLite database stored uncompressed inside a .zip.
        /// </summary>
        /// <param name="zipFilePath">The .zip (container) file on disk.</param>
        /// <param name="dataOffset">Offset of the database's data inside the .zip.</param>
        /// <param name="length">Length of the database.</param>
        public static SQLiteConnection OpenConnection(string zipFilePath, long dataOffset, long length)
        {
            EnsureVfsRegistered();
            var uri = new Uri(zipFilePath).AbsoluteUri + @"?ofs=" + dataOffset + @"&len=" + length + @"&vfs=" + VFS_NAME;
            var connection = new SQLiteConnection(@"FullUri=""" + uri + @""";Version=3;Read Only=True;");
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Registers the "slicevfs" VFS with SQLite the first time it is needed. The extension DLL
        /// is pinned in memory for the process lifetime, because the registered VFS lives inside it.
        /// </summary>
        private static void EnsureVfsRegistered()
        {
            if (_registered)
                return;
            lock (RegisterLock)
            {
                if (_registered)
                    return;
                var dllPath = GetExtensionDllPath();
                // Pin the DLL so it is never unloaded (the registered VFS struct/functions live in it).
                if (LoadLibrary(dllPath) == IntPtr.Zero)
                    throw new IOException(string.Format(
                        Resources.ZipVfs_EnsureVfsRegistered_Unable_to_load__0_, dllPath));
                using (var connection = new SQLiteConnection(@"Data Source=:memory:;Version=3;"))
                {
                    connection.Open();
                    connection.EnableExtensions(true);
                    connection.LoadExtension(dllPath, EXTENSION_INIT);
                }
                _registered = true;
            }
        }

        private static string GetExtensionDllPath()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            return Path.Combine(dir, EXTENSION_DLL);
        }
    }
}
