//
// $Id: $
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
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
using System.IO;
using System.Linq;
using System.Text;

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
                    string queryPath = Path.Combine(dir.FullName, fileNameWithoutExtension + "." + ext);
                    if (File.Exists(queryPath))
                    {
                        fileMatches.Add(queryPath);
                        if (stopAtFirstMatch)
                            break;
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
                                            source + "\"\r\n\r\n" +
                                            "Check that this source file can be " +
                                            "found in the source search paths " +
                                            "(configured in Tools/Options) with one of the " +
                                            "configured source file extensions:\r\n" +
                                            Properties.Settings.Default.SourceExtensions);

            return matches[0];
        }

        public static string FindSearchInSearchPath (string source, string rootInputDirectory)
        {
            source = Path.GetFileNameWithoutExtension(source);
            List<string> paths = new List<string>(StringCollectionToStringArray(Properties.Settings.Default.SearchPaths));
            if (!paths.Contains("<RootInputDirectory>"))
                paths.Add("<RootInputDirectory>");
            KeyValuePair<string, string>[] replacePairs = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("<SourceName>", source),
                new KeyValuePair<string, string>("<RootInputDirectory>", rootInputDirectory)
            };
            paths = new List<string>(ReplaceKeysWithValues(paths.ToArray(), replacePairs));

            string[] extensions = new string[] { "pepXML", "idpXML" };
            string[] matches = FindFileInSearchPath(source, extensions, paths.ToArray(), true);

            if (matches.Length == 0)
                throw new ArgumentException("Cannot find search file corresponding to \"" +
                                            source + "\"\r\n\r\n" +
                                            "Check that this search file can be " +
                                            "found in the search file search paths " +
                                            "(configured in Tools/Options) with a search file " +
                                            "extension (pepXML or idpXML).");

            return matches[0];
        }
    }
}