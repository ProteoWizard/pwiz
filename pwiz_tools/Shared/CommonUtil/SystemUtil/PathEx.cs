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
using System.Text.RegularExpressions;
using System.Linq;
using pwiz.Common.SystemUtil.PInvoke;
using System.Text;

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
                if (char.ToLowerInvariant(path1[i]) != char.ToLowerInvariant(path2[i]))
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

        /// <summary>
        /// Environment variable which can be used to override <see cref="GetDownloadsPath"/>.
        /// </summary>
        public const string SKYLINE_DOWNLOAD_PATH = @"SKYLINE_DOWNLOAD_PATH";

        // From http://stackoverflow.com/questions/3795023/downloads-folder-not-special-enough
        // Get the path for the Downloads directory, avoiding the assumption that it's under the 
        // user's personal directory (it's possible to relocate it under newer versions of Windows)
        public static string GetDownloadsPath()
        {
            string path = Environment.GetEnvironmentVariable(SKYLINE_DOWNLOAD_PATH);
            if (path != null)
            {
                return path;
            }
            else if (Environment.OSVersion.Version.Major >= 6)
            {
                path = Shell32.GetDownloadsFolder();
                if (path != null)
                {
                    return path;
                }
            }
            path = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
            if (string.IsNullOrEmpty(path)) // Keep multiple versions of ReSharper happy
                path = string.Empty;
            path = Path.Combine(path, @"Downloads");
            return path;
        }

        /// <summary>
        /// Returns true if the <see cref="GetDownloadsPath"/> is likely to be a folder
        /// which is shared with other users on the computer
        /// </summary>
        public static bool IsDownloadsPathShared()
        {
            // If the "SKYLINE_DOWNLOAD_PATH" environment variable has been set in a system environment
            // variable and not a user environment variable, then assume the folder is shared
            // with other users
            return null != Environment.GetEnvironmentVariable(SKYLINE_DOWNLOAD_PATH)
                   && null == Environment.GetEnvironmentVariable(SKYLINE_DOWNLOAD_PATH, EnvironmentVariableTarget.User);
        }

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

        public const string PREFIX_LONG_PATH = @"\\?\";

        /// <summary>
        /// Returns a path with a long-path prefix. If the passed in path already has
        /// a long-path prefix it is returned as is. Otherwise, one is prepended.
        /// </summary>
        /// <param name="path">Path to convert to long-path syntax. This must be a fully qualified path.</param>
        public static string ToLongPath(this string path)
        {
            if (path != null && path.StartsWith(PREFIX_LONG_PATH))
                return path;

            // Note: Using this function requires an already
            // fully qualified path.
            if (!IsPathFullyQualified(path))
                throw new ArgumentException($@"Failed attempting to use long-path syntax for the path '{path}' which is not fully qualified.");

            // Avoid adding the long-path prefix to a path that already has it.
            return PREFIX_LONG_PATH + path;
        }

        /// <summary>
        /// Returns true if the path specified is relative to the current drive or working directory.
        /// Returns false if the path is fixed to a specific drive or UNC path.  This method does no
        /// validation of the path (URIs will be returned as relative as a result).
        /// </summary>
        /// <remarks>
        /// Copied from .NET 8 source code
        /// Handles paths that use the alternate directory separator.  It is a frequent mistake to
        /// assume that rooted paths (Path.IsPathRooted) are not relative.  This isn't the case.
        /// "C:a" is drive relative- meaning that it will be resolved against the current directory
        /// for C: (rooted, but relative). "C:\a" is rooted and not relative (the current directory
        /// will not be used to modify the path).
        /// </remarks>
        public static bool IsPathFullyQualified(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 2)
            {
                // It isn't fixed, it must be relative.  There is no way to specify a fixed
                // path with one character (or less).
                return false;
            }

            if (IsDirectorySeparator(path[0]))
            {
                // There is no valid way to specify a relative path with two initial slashes or
                // \? as ? isn't valid for drive relative paths and \??\ is equivalent to \\?\
                return path[1] == '?' || IsDirectorySeparator(path[1]);
            }

            // The only way to specify a fixed path that doesn't begin with two slashes
            // is the drive, colon, slash format- i.e. C:\
            return path.Length >= 3
                   && path[1] == Path.VolumeSeparatorChar
                   && IsDirectorySeparator(path[2])
                   // To match old behavior we'll check the drive character for validity as the path is technically
                   // not qualified if you don't have a valid drive. "=:\" is the "=" file's default data stream.
                   && IsValidDriveChar(path[0]);
        }

        /// <summary>
        /// True if the given character is a directory separator.
        /// </summary>
        private static bool IsDirectorySeparator(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Returns true if the given character is a valid drive letter. (borrowed from .NET 8 code)
        /// </summary>
        private static bool IsValidDriveChar(char value)
        {
            return (uint)((value | 0x20) - 'a') <= 'z' - 'a';
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
        /// Apparently this was added to Path in .NET 5 and later. Several places in the project have
        /// now implemented this with either RemovePrefix or some other method.
        /// </summary>
        /// <param name="relativePathTo">The base path to make the path relative to</param>
        /// <param name="path">The full path to make relative</param>
        /// <returns>The relative path from the base path to the given path</returns>
        public static string GetRelativePath(string relativePathTo, string path)
        {
            if (!relativePathTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
                relativePathTo += Path.DirectorySeparatorChar;
            return RemovePrefix(path, relativePathTo);
        }

        /// <summary>
        /// If the path starts with the prefix, then skip over the prefix; 
        /// otherwise, return the original path. This works well as a method of getting a relative
        /// path in combination with <see cref="GetCommonRoot(System.Collections.Generic.IEnumerable{string})"/>, since
        /// it is guaranteed to return a directory path ending with a Path.DirectorySeparatorChar, but not in
        /// cases where a path to a directory lacks a terminating separator. In those cases, use <see cref="GetRelativePath"/>.
        /// </summary>
        public static string RemovePrefix(string path, string prefix)
        {
            if (path.ToLowerInvariant().StartsWith(prefix.ToLowerInvariant()))
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

        /// <summary>
        /// Puts escaped quotation marks before and after the text passed in if it contains any spaces
        /// Useful for constructing command lines with arguments needing nested quotes
        /// e.g. msconvert --filter "mzRefiner \"my input1.pepXML\" \"your input2.mzid\""
        ///
        /// </summary>
        public static string EscapedPathForNestedCommandLineQuotes(this string text)
        {
            if (text.Contains(@" "))
            {
                return @"\""" + text + @"\""";
            }
            return text;
        }

        // Inspect a file path for characters that must be escaped for use in XML (currently just "&")
        // Return a suitably escaped version of the string
        public static string EscapePathForXML(string path)
        {
            if (path.Contains(@"&")) // Valid windows filename character, may need escaping
            {
                // But it may also be in use as an escape character - don't mess with &quot; etc
                path = Regex.Replace(path, @"&(?!(?:apos|quot|[gl]t|amp);|#)", @"&amp;");
            }
            return path;
        }

        /// <summary>
        /// Returns true iff c is a valid filename character (e.g. isn't one of \/*:?&lt;>|" nor a non-printable chars).
        /// </summary>
        public static bool IsValidFilenameChar(char c)
        {
            const string illegalFilename = "\\/*:?<>|\"";
            return !(c < 0x20 || c == 0x7f || illegalFilename.Contains(c));
        }

        /// <summary>
        /// Replaces invalid filename characters (\/*:?&lt;>|" and non-printable chars) with a replacement character (default '_').
        /// </summary>
        public static string ReplaceInvalidFilenameCharacters(string filename, char replacementChar = '_')
        {
            return filename.All(IsValidFilenameChar) ? filename : new string(filename.Select(c => IsValidFilenameChar(c) ? c : '_').ToArray());
        }

        /// <summary>
        /// Like Path.GetRandomFileName(), but in test context adds some legal but unusual characters for robustness testing
        /// </summary>
        /// <returns>a random filename</returns>
        public static string GetRandomFileName()
        {
            var result = Path.GetRandomFileName();
            if (!string.IsNullOrEmpty(RandomFileNameDecoration))
            {
                // Introduce some potentially troublesome characters like space, caret, ampersand to test our handling
                // Adding Unicode here (e.g. 试验, means "test") breaks many 3rd party tools (e.g. msFragger), causes trouble
                // with mz5 reader, etc so we don't do it for certain tests
                result = RandomFileNameDecoration + result;
            }
            return result;
        }

        // Test framework can set this to something like  @""t^m&p 试验" to help check our handling of unusual filename characters
        public static string RandomFileNameDecoration { get; set; }

        // Like Path.GetTempFileName(), but allows you to set the extension of the created tempfile
        public static string GetTempFileNameWithExtension(string ext)
        {
            var fileName = Path.GetTempFileName();
            if (!string.IsNullOrWhiteSpace(ext))
            {
                var fileNameNew = Path.ChangeExtension(fileName, ext);
                if (!fileName.Equals(fileNameNew))
                {
                    File.Move(fileName, fileNameNew);
                    return fileNameNew;
                }
            }
            return fileName;
        }

        /// <summary>
        /// We often encounter tools that can't deal with Unicode characters in file paths, this method
        /// will try to convert such paths to a non-Unicode version using the 8.3 format short path name.
        /// Converts only the segments that need it, to avoid trashing filename extensions.
        /// e.g. "C:\Program Files\Common Files\my files with ünicode\foo.mzml" (note the umlaut U) => ""C:\Program Files\Common Files\MYFILE~1\foo.mzml"
        ///
        /// Only works on NTFS volumes, with 8.3 support enabled. So, for example, not on Docker instances.
        ///
        /// N.B there is an equivalent function in pwiz CLI util.Filesystem, but not all uses of
        /// this capability necessarily want to depend on the CLI. The Skyline test system checks for
        /// identical performance of the two implementations.
        ///
        /// </summary>
        /// <param name="path">Path to an existing file</param>
        /// <returns>Path with unicode segments changed to 8.3 representation, if possible</returns>    
        public static string GetNonUnicodePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            // Check for non-printable or non-ASCII characters
            var hasNonAscii = path.Any(c => c < 32 || c > 126);
            if (!hasNonAscii)
            {
                return path;
            }

            var fullPath = Path.GetFullPath(path);
            var segments = fullPath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var currentPath = segments[0] + Path.DirectorySeparatorChar; // Root directory

            foreach (var segment in segments.Skip(1))
            {
                var nextPath = Path.Combine(currentPath, segment);

                // Replace segment with 8.3 short name only if it contains non-ASCII
                var needsShort = segment.Any(c => c < 32 || c > 126);
                if (needsShort && (Directory.Exists(nextPath) || File.Exists(nextPath)))
                {
                    var sb = new StringBuilder(260);
                    var result = Kernel32.GetShortPathName(nextPath, sb, sb.Capacity);
                    if (result > 0)
                    {
                        var shortSegment = Path.GetFileName(sb.ToString());
                        nextPath = Path.Combine(currentPath, shortSegment);
                    }
                }

                currentPath = nextPath;
            }

            return currentPath;
        }

    }

    /// <summary>
    /// Sets the current directory for the duration of the object's lifetime (typically within a using() block),
    /// then restores it back to its original value
    /// </summary>
    public class CurrentDirectorySetter : IDisposable
    {
        private string PreviousDirectory { get; }

        public CurrentDirectorySetter(string directory)
        {
            PreviousDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(directory);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(PreviousDirectory);
        }
    }
}
