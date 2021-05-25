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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.AccessControl;

namespace IDPicker
{
    public static partial class Util
    {
        public static string[] FindFileInSearchPath (string fileNameWithoutExtension,
                                                     string[] matchingFileExtensions,
                                                     string[] directoriesToSearch,
                                                     bool stopAtFirstMatch)
        {
            List<string> fileMatches = new List<string>();
            foreach (string searchPath in directoriesToSearch)
            {
                DirectoryInfo dir = new DirectoryInfo(searchPath);
                foreach (string ext in matchingFileExtensions)
                {
                    string queryPath = Path.Combine(dir.FullName, fileNameWithoutExtension + ext);
                    if (File.Exists(queryPath))
                    {
                        fileMatches.Add(queryPath);
                        if (stopAtFirstMatch)
                            break;
                    }
                    else if (!ext.StartsWith("."))
                    {
                        queryPath = Path.Combine(dir.FullName, fileNameWithoutExtension + "." + ext);
                        if (File.Exists(queryPath))
                        {
                            fileMatches.Add(queryPath);
                            if (stopAtFirstMatch)
                                break;
                        }
                    }
                }

                if (stopAtFirstMatch && fileMatches.Count > 0)
                    break;
            }

            return fileMatches.ToArray();
        }

        public static string FindDatabaseInSearchPath (string databaseName, string rootInputDirectory)
        {
            databaseName = Path.GetFileNameWithoutExtension(databaseName);
            List<string> paths = new List<string>(StringCollectionToStringArray(Properties.Settings.Default.FastaPaths));
            if (!paths.Contains("<RootInputDirectory>"))
                paths.Add("<RootInputDirectory>");
            KeyValuePair<string, string>[] replacePairs = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("<DatabaseDirectory>", databaseName),
                new KeyValuePair<string, string>("<RootInputDirectory>", rootInputDirectory)
            };
            paths = new List<string>(ReplaceKeysWithValues(paths.ToArray(), replacePairs));

            string[] extensions = new string[] { "fasta" };
            string[] matches = FindFileInSearchPath(databaseName, extensions, paths.ToArray(), true);

            if (matches.Length == 0)
                throw new ArgumentException("Cannot find database file corresponding to \"" +
                                            databaseName + "\"\r\n\r\n" +
                                            "Check that this database file can be " +
                                            "found in the database search paths " +
                                            "(configured in Tools/Options) with a database file " +
                                            "extension (FASTA).");

            return matches[0];
        }

        public static string FindSourceInSearchPath (string source, string rootInputDirectory)
        {
            source = Path.GetFileNameWithoutExtension(source);
            List<string> paths = new List<string>(StringCollectionToStringArray(Properties.Settings.Default.SourcePaths));
            if (!paths.Contains("<RootInputDirectory>"))
                paths.Add("<RootInputDirectory>");
            KeyValuePair<string, string>[] replacePairs = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("<SourceName>", source),
                new KeyValuePair<string, string>("<RootInputDirectory>", rootInputDirectory)
            };
            paths = new List<string>(ReplaceKeysWithValues(paths.ToArray(), replacePairs));

            string[] extensions = Properties.Settings.Default.SourceExtensions.Split(";".ToCharArray());
            string[] matches = FindFileInSearchPath(source, extensions, paths.ToArray(), true);

            if (matches.Length == 0)
                throw new ArgumentException("Cannot find source file corresponding to \"" +
                                            source + "\"\r\nin these directories:\r\n" +
                                            String.Join("\r\n", paths) + "\r\n\r\n" +
                                            "Check that this source file can be " +
                                            "found in the source search paths " +
                                            "(configured in Tools/Options) with one of the " +
                                            "configured source file extensions:\r\n" +
                                            Properties.Settings.Default.SourceExtensions);

            return matches[0];
        }

        /// <summary>
        /// Returns the root element (i.e. drive letter) of a path.
        /// If the path is relative, returns the root element of Environment.CurrentDirectory.
        /// </summary>
        public static string GetPathRoot (string path)
        {
            string root = Path.GetPathRoot(path);
            if (String.IsNullOrEmpty(root))
                return Path.GetPathRoot(Environment.CurrentDirectory);
            return root;
        }

        public class PrecacheProgressUpdateEventArgs : CancelEventArgs
        {
            public float PercentComplete { get; set; }
        }

        public static bool PrecacheFile(string filepath, EventHandler<PrecacheProgressUpdateEventArgs> progressUpdateHandler = null)
        {
            try
            {
                // if the file is on a hard drive and can fit in the available RAM, populate the disk cache
                long ramBytesAvailable = (long)new System.Diagnostics.PerformanceCounter("Memory", "Available Bytes").NextValue();
                if (ramBytesAvailable > new FileInfo(filepath).Length && IsPathOnFixedDrive(filepath))
                {
                    using (var fs = new FileStream(filepath, FileMode.Open, FileSystemRights.ReadData, FileShare.ReadWrite, UInt16.MaxValue, FileOptions.SequentialScan))
                    {
                        var buffer = new byte[UInt16.MaxValue];
                        float totalBytes = (float) fs.Length;

                        if (progressUpdateHandler != null)
                        {
                            var e = new PrecacheProgressUpdateEventArgs { PercentComplete = 0 };
                            progressUpdateHandler(filepath, e);

                            long bytesRead = 0;
                            do
                            {
                                if (bytesRead / totalBytes > e.PercentComplete + 0.01)
                                {
                                    e.PercentComplete = bytesRead/totalBytes;
                                    progressUpdateHandler(filepath, e);
                                    if (e.Cancel)
                                        break;
                                }
                                bytesRead += fs.Read(buffer, 0, UInt16.MaxValue);
                            } while (bytesRead < totalBytes);
                            e.PercentComplete = 1;
                            progressUpdateHandler(filepath, e);
                        }
                        else
                            while (fs.Read(buffer, 0, UInt16.MaxValue) > 0) { }
                    }
                    return true;
                }
            }
            catch (Exception)
            {
                // ignore precaching errors; could be due to user privileges and it's an optional step
            }
            return false;
        }

        /// <summary>
        /// Tests whether a path is on a "fixed" drive (e.g. not a network, optical, tape, or flash drive).
        /// UNC and URI paths always count as network drives. The path does not actually need to exist.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when an invalid path is passed.</exception>
        public static bool IsPathOnFixedDrive(string path)
        {
            try
            {
                if (path.TrimStart().StartsWith(@"\\"))
                    return false;
                if (Uri.IsWellFormedUriString(path.TrimStart(), UriKind.Absolute))
                    return false;
                return DriveType.Fixed == new DriveInfo(GetPathRoot(path)).DriveType;
            }
            catch(Exception e)
            {
                throw new ArgumentException("[IsPathOnFixedDrive] error checking filepath \"" + path + "\"", e);
            }
        }

        /// <summary>
        /// If necessary, adds a third backslash to UNC paths to work around a "working as designed" issue in System.Data.SQLite:
        /// http://system.data.sqlite.org/index.html/tktview/01a6c83d51a203ff?plaintext
        /// </summary>
        public static string GetSQLiteUncCompatiblePath(string path)
        {
            return path.TrimStart().StartsWith(@"\\") ? @"\" + path : path;
        }
    }
}