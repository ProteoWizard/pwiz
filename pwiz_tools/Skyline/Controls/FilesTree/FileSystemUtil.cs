/*
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

using System;
using System.IO;

namespace pwiz.Skyline.Controls.FilesTree
{
    public static class FileSystemUtil
    {
        public static string GetDirectoryOrRoot(string filePath)
        {
            var result = Path.GetDirectoryName(filePath);
            if (result == null && Path.IsPathRooted(filePath))
            {
                // Special case for root directory: return the root itself
                result = Path.GetPathRoot(filePath);
            }

            return Normalize(result);
        }

        /// <summary>
        /// Compare two directory paths, remembering to ignore capitalization.
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        /// <returns>true if the paths are the same; false otherwise</returns>
        public static bool PathEquals(string path1, string path2)
        {
            return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true if the path refers to an NTFS Alternate Data Stream (ADS),
        /// such as "file.zip:Zone.Identifier". Detected by a colon in the final path
        /// segment — the drive letter colon always has a separator after it, ADS colons don't.
        /// </summary>
        public static bool IsAlternateDataStream(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            int lastColon = path.LastIndexOf(':');
            // Drive letter colons always have a separator after them; ADS stream colons don't.
            return lastColon >= 0 && lastColon < path.Length - 1
                && path.IndexOfAny(new[] { '\\', '/' }, lastColon + 1) < 0;
        }

        public static string Normalize(string path)
        {
            if (path == null)
                return null;

            try
            {
                return Path.GetFullPath(path);
            }
            catch (NotSupportedException) { return path; }
            catch (ArgumentException) { return path; }
        }

        /// <summary>
        /// Check whether the <see cref="filePath"/> is contained in the <see cref="directoryPath"/>.
        /// </summary>
        /// <param name="directoryPath">Path to a directory</param>
        /// <param name="filePath">Path to a directory or a file</param>
        /// <returns>true if child contained in parent. False otherwise.</returns>
        /// CONSIDER: how should this handle a filePath that's too long and throws PathTooLongException?
        public static bool IsFileInDirectory(string directoryPath, string filePath)
        {
            var normalizedDirectoryPath = Normalize(directoryPath);
            if (normalizedDirectoryPath == null)
                return false;

            if(!normalizedDirectoryPath.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
               !normalizedDirectoryPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                normalizedDirectoryPath += Path.DirectorySeparatorChar;
            }

            var normalizedFilePath = Normalize(filePath);
            if (normalizedFilePath == null)
                return false;

            var normalizedFileDirectoryPath = Path.GetDirectoryName(normalizedFilePath);
            if (normalizedFileDirectoryPath == null)
                return false;

            if (!normalizedFileDirectoryPath.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !normalizedFileDirectoryPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                normalizedFileDirectoryPath += Path.DirectorySeparatorChar;
            }

            return PathEquals(normalizedDirectoryPath, normalizedFileDirectoryPath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseDirectory"></param>
        /// <param name="possibleSubdirectory"></param>
        /// <returns></returns>
        /// CONSIDER: how should this handle a filePath that's too long and throws PathTooLongException?
        public static bool IsInOrSubdirectoryOf(string baseDirectory, string possibleSubdirectory)
        {
            // Normalize paths to ensure consistent comparison (e.g., handle relative paths, different separators)
            var normalizedPotentialSubdirectory = Normalize(possibleSubdirectory);
            var normalizedBaseDirectory = Normalize(baseDirectory);

            if (normalizedPotentialSubdirectory == null || normalizedBaseDirectory == null)
                return false;

            normalizedPotentialSubdirectory = normalizedPotentialSubdirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            normalizedBaseDirectory = normalizedBaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Add a directory separator to the base directory for accurate 'Contains' check
            // This prevents false positives where a directory name is a prefix of another (e.g., C:\Dir and C:\Directory)
            normalizedBaseDirectory += Path.DirectorySeparatorChar;

            // Perform a case-insensitive comparison. Returns true if possibleSubdirectory is somewhere below baseDirectory.
            return PathEquals(normalizedPotentialSubdirectory, normalizedBaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        }
    }
}