/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Runtime.InteropServices;
using System.IO;

namespace pwiz.Common.SystemUtil
{
    public static class PathEx
    {
        /// <summary>
        /// Determines whether the file name in the given path has a
        /// specific extension.  Because this is Windows, the comparison
        /// is case insensitive.  Note that ToLowerInvariant() is used to make
        /// sure it works for Turkish with extensions containing the letter i
        /// (e.g. .wiff).  This does, however, make this function only work
        /// for extenstions with only lower ASCII characters, which all of
        /// ours are.
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <param name="ext">Extension to check for (only lower ASCII)</param>
        /// <returns>True if the path has the extension</returns>
        public static bool HasExtension(string path, string ext)
        {
            return path.ToLowerInvariant().EndsWith(ext.ToLowerInvariant());
        }

        public static string GetCommonRoot(IEnumerable<string> paths)
        {
            string rootPath = string.Empty;
            foreach (string path in paths)
            {
                string dirName = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dirName))
                {
                    return string.Empty;
                }
                if (dirName[dirName.Length - 1] != Path.DirectorySeparatorChar)
                {
                    dirName += Path.DirectorySeparatorChar;
                }
                rootPath = string.IsNullOrEmpty(rootPath)
                    ? dirName
                    : GetCommonRoot(rootPath, dirName);
            }
            return rootPath;
        }

        public static string GetCommonRoot(string path1, string path2)
        {
            int len = Math.Min(path1.Length, path2.Length);
            int lastSep = -1;
            for (int i = 0; i < len; i++)
            {
                if (path1[i] != path2[i])
                    return path1.Substring(0, lastSep + 1);

                if (path1[i] == Path.DirectorySeparatorChar)
                    lastSep = i;
            }
            return path1.Substring(0, len);
        }

        public static string ShortenPathForDisplay(string path)
        {
            var parts = new List<string>(path.Split(new[] { Path.DirectorySeparatorChar }));
            if (parts.Count < 2)
                return string.Empty;

            int iElipsis = parts.IndexOf(@"...");
            int iStart = (iElipsis != -1 ? iElipsis : parts.Count - 2);
            int iRemove = iStart - 1;
            if (iRemove < 1)
            {
                iRemove = iStart;
                if (iRemove == iElipsis)
                    iRemove++;
                if (iRemove >= parts.Count - 1)
                    return parts[parts.Count - 1];
            }
            parts.RemoveAt(iRemove);
            return string.Join(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), parts.ToArray());
        }

        // From http://stackoverflow.com/questions/3795023/downloads-folder-not-special-enough
        // Get the path for the Downloads directory, avoiding the assumption that it's under the 
        // user's personal directory (it's possible to relocate it under newer versions of Windows)
        public static string GetDownloadsPath()
        {
            string path = Environment.GetEnvironmentVariable(@"SKYLINE_DOWNLOAD_PATH");
            if (path != null)
            {
                return path;
            }
            else if (Environment.OSVersion.Version.Major >= 6)
            {
                IntPtr pathPtr;
                int hr = SHGetKnownFolderPath(ref FolderDownloads, 0, IntPtr.Zero, out pathPtr);
                if (hr == 0)
                {
                    path = Marshal.PtrToStringUni(pathPtr);
                    Marshal.FreeCoTaskMem(pathPtr);
                    return path;
                }
            }
            path = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
            if (string.IsNullOrEmpty(path)) // Keep multiple versions of ReSharper happy
                path = string.Empty;
            path = Path.Combine(path, @"Downloads");
            return path;
        }

        private static Guid FolderDownloads = new Guid(@"374DE290-123F-4565-9164-39C4925E467B");
        [DllImport(@"shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetKnownFolderPath(ref Guid id, int flags, IntPtr token, out IntPtr path);

        /// <summary>
        /// Wrapper around <see cref="Path.GetDirectoryName"/> which, if an error occurs, adds
        /// the path to the exception message.
        /// Eventually, this method might catch the exception and try to return something reasonable,
        /// but, for now, we are trying to figure out the source of invalid paths.
        /// </summary>
        public static String GetDirectoryName(String path)
        {
            try
            {
                return Path.GetDirectoryName(path);
            }
            catch (ArgumentException e)
            {
                throw AddPathToArgumentException(e, path);
            }
        }

        private static ArgumentException AddPathToArgumentException(ArgumentException argumentException, string path)
        {
            string messageWithPath = string.Join(Environment.NewLine, argumentException.Message, path);
            return new ArgumentException(messageWithPath, argumentException);
        }

        /// <summary>
        /// Given a path to an anchor file and a path where another file used to exist, the function
        /// tests for existence of the file in the same folder as the anchor and two parent folders up.
        /// </summary>
        /// <param name="relativeFilePath">Path to the anchor file</param>
        /// <param name="findFilePath">Outdated path to the file to find</param>
        /// <returns>The path to the file, if it exists, or null if it is not found</returns>
        public static string FindExistingRelativeFile(string relativeFilePath, string findFilePath)
        {
            string fileName = Path.GetFileName(findFilePath);
            string searchDir = Path.GetDirectoryName(relativeFilePath);
            // Look in document directory and two parent directories up
            for (int i = 0; i <= 2; i++)
            {
                string filePath = Path.Combine(searchDir ?? string.Empty, fileName ?? string.Empty);
                if (File.Exists(filePath))
                    return filePath;
                // Look in parent directory
                searchDir = Path.GetDirectoryName(searchDir);
                // Stop if the root directory was checked last
                if (searchDir == null)
                    break;
            }
            return null;
        }

        /// <summary>
        /// If the path starts with the prefix, then skip over the prefix; 
        /// otherwise, return the original path.
        /// </summary>
        public static string RemovePrefix(string path, string prefix)
        {
            if (path.StartsWith(prefix))
            {
                return path.Substring(prefix.Length);
            }
            return path;
        }

        /// <summary>
        /// If path is null, throw an ArgumentException
        /// </summary>
        /// <param name="path"></param>
        /// <returns>path</returns>
        public static string SafePath(string path)
        {
            if (path == null)
            {
                throw new ArgumentException(@"null path name");
            }
            return path;
        }
    }
}
