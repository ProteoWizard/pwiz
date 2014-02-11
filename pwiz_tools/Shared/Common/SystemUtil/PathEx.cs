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
            string rootPath = "";
            foreach (string path in paths)
            {
                string dirName = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
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
                return "";

            int iElipsis = parts.IndexOf("...");
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
    }
}