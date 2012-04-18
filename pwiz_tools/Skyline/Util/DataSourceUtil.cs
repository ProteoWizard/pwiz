/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.IO;
using System.Xml;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Util
{
    public static class DataSourceUtil
    {
        public const string EXT_THERMO_RAW = ".raw";
        public const string EXT_WIFF = ".wiff";
        public const string EXT_MZXML =  ".mzxml";
        public const string EXT_MZDATA = ".mzdata";
        public const string EXT_MZML = ".mzml";
        public const string EXT_XML = ".xml";

        public const string TYPE_WIFF = "ABSciex WIFF";
        public const string TYPE_AGILENT = "Agilent Data";
        public const string TYPE_THERMO_RAW = "Thermo RAW";
        public const string TYPE_WATERS_RAW = "Waters RAW";
        public const string TYPE_MZML = "mzML";
        public const string TYPE_MZXML = "mzXML";
        public const string TYPE_MZDATA = "mzData";
        public const string FOLDER_TYPE = "File Folder";
        public const string UNKNOWN_TYPE = "unknown";


        public static bool IsDataSource(DirectoryInfo dirInfo)
        {
            return !Equals(GetSourceType(dirInfo), FOLDER_TYPE);
        }

        public static string GetSourceType(DirectoryInfo dirInfo)
        {
            try
            {
                if (dirInfo.Name.EndsWith(".raw") &&
                        dirInfo.GetFiles("_FUNC*.DAT").Length > 0)
                    return TYPE_WATERS_RAW;
                if (dirInfo.Name.EndsWith(".d") &&
                        dirInfo.GetDirectories("AcqData").Length > 0)
                    return TYPE_AGILENT;
                return FOLDER_TYPE;
            }
            catch (Exception)
            {
                // TODO: Folder without access type
                return FOLDER_TYPE;
            }
        }

        public static bool IsDataSource(FileInfo fileInfo)
        {
            return !Equals(GetSourceType(fileInfo), UNKNOWN_TYPE);
        }

        public static string GetSourceType(FileInfo fileInfo)
        {
            //if (fileInfo.Name == "fid")
            //    return "Bruker FID";

            switch (fileInfo.Extension.ToLower())
            {
                case EXT_THERMO_RAW: return TYPE_THERMO_RAW;
                case EXT_WIFF: return TYPE_WIFF;
                //case ".mgf": return "Mascot Generic";
                //case ".dta": return "Sequest DTA";
                //case ".yep": return "Bruker YEP";
                //case ".baf": return "Bruker BAF";
                //case ".ms2": return "MS2";
                case EXT_MZXML: return TYPE_MZXML;
                case EXT_MZDATA: return TYPE_MZDATA;
                case EXT_MZML: return TYPE_MZML;
                case EXT_XML: return GetSourceTypeFromXML(fileInfo.FullName);
                default: return UNKNOWN_TYPE;
            }
        }

        public static bool IsWiffFile(string fileName)
        {
            return fileName.ToLower().EndsWith(EXT_WIFF);
        }

        public static bool IsFolderType(string type)
        {
            return Equals(type, FOLDER_TYPE);
        }

        public static bool IsUnknownType(string type)
        {
            return Equals(type, UNKNOWN_TYPE);
        }

        private static string GetSourceTypeFromXML(string filepath)
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.None,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using (XmlReader reader = XmlReader.Create(new StreamReader(filepath, true), settings))
            {
                try
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name.ToLower())
                            {
                                case "mzml":
                                case "indexmzml":
                                    return "mzML";
                                case "mzxml":
                                case "msrun":
                                    return "mzXML";
                                //case "mzdata":
                                //    return "mzData";
                                case "root":
                                    return "Bruker Data Exchange";
                                default:
                                    return UNKNOWN_TYPE;
                            }
                        }
                    }
                }
                catch (XmlException)
                {
                    return UNKNOWN_TYPE;
                }
            }
            return UNKNOWN_TYPE;
        }

        // This method can throw an IOException if there is an error reading .wiff files in 
        // the given directory.
        public static IEnumerable<KeyValuePair<string, string[]>> GetDataSources(string dirRoot)
        {
            return GetDataSources(dirRoot, true, true);
        }

        public static IEnumerable<KeyValuePair<string, string[]>> GetDataSourcesInSubdirs(string dirRoot)
        {
            return GetDataSources(dirRoot, false, true);
        }

        private static IEnumerable<KeyValuePair<string, string[]>> GetDataSources(string dirRoot, bool addSourcesInRootDir,
            bool addSourcesInSubDirs)
        {
            var listNamedPaths = new List<KeyValuePair<string, string[]>>();

            if (addSourcesInSubDirs)
            {
                var dirRootInfo = new DirectoryInfo(dirRoot);
                foreach (var subDirInfo in dirRootInfo.GetDirectories())
                {
                    var listDataPaths = new List<string>();
                    foreach (var dataDirInfo in subDirInfo.GetDirectories())
                    {
                        if (IsDataSource(dataDirInfo))
                            listDataPaths.Add(dataDirInfo.FullName);
                    }
                    foreach (var dataFileInfo in subDirInfo.GetFiles())
                    {
                        if (IsDataSource(dataFileInfo))
                            listDataPaths.Add(dataFileInfo.FullName);
                    }
                    if (listDataPaths.Count == 0)
                        continue;

                    listDataPaths.Sort();
                    listNamedPaths.Add(new KeyValuePair<string, string[]>(
                                           subDirInfo.Name, listDataPaths.ToArray()));
                }
            }

            if (addSourcesInRootDir)
            {
                // get a list of all valid files in this directory
                var dirInfo = new DirectoryInfo(dirRoot);

                // This if for WATERS(.raw) and AGILENT(.d) data directories
                foreach (var dataDirInfo in dirInfo.GetDirectories())
                {
                    if (IsDataSource(dataDirInfo))
                    {
                        string dataSource = dataDirInfo.FullName;
                        listNamedPaths.Add(new KeyValuePair<string, string[]>(
                                       Path.GetFileNameWithoutExtension(dataSource), new[] { dataSource }));
                    }
                }

                foreach (var dataFileInfo in dirInfo.GetFiles())
                {
                    if (IsDataSource(dataFileInfo))
                    {
                        string dataSource = dataFileInfo.FullName;
                        // Only .wiff files currently support multiple samples per file.
                        // Keep from doing the extra work on other types.
                        if (IsWiffFile(dataSource))
                        {
                            string[] paths = GetWiffSubPaths(dataSource);
                            if (paths == null)
                                return null;    // An error occurred
                            // Multiple paths then add as samples
                            if (paths.Length > 1 ||
                                // If just one, make sure it has a sample part.  Otherwise,
                                // drop through to add the entire file.
                                (paths.Length == 1 && SampleHelp.GetPathSampleNamePart(paths[0]) != null))
                            {
                                foreach (string path in paths)
                                {
                                    listNamedPaths.Add(new KeyValuePair<string, string[]>(
                                                           SampleHelp.GetPathSampleNamePart(path), new[] { path }));
                                }
                                continue;
                            }
                        }

                        listNamedPaths.Add(new KeyValuePair<string, string[]>(
                                       Path.GetFileNameWithoutExtension(dataSource), new[] { dataSource }));
                    }
                }
            }

            listNamedPaths.Sort((p1, p2) => Comparer<string>.Default.Compare(p1.Key, p2.Key));
            return listNamedPaths;
        }

        private static string[] GetWiffSubPaths(string filePath)
        {
            string[] dataIds;
            try
            {
                dataIds = MsDataFileImpl.ReadIds(filePath);
            }
            catch (Exception x)
            {
                throw new IOException(string.Format("An error occurred attempting to read sample information from the file {0}.\nThe file may be corrupted, missing, or the correct libraries may not be installed.\n{1}", filePath, x.Message));
            }

            return GetWiffSubPaths(filePath, dataIds, null);
        }

        public static string[] GetWiffSubPaths(string filePath, string[] dataIds, Func<string, string[], IEnumerable<int>> sampleChooser)
        {
            if (dataIds == null)
                return null;

            // WIFF without at least 2 samples just use its file name.
            if (dataIds.Length < 2)
                return new[] { filePath };

            // Escape all the sample ID names, so that they may be used in file names.
            for (int i = 0; i < dataIds.Length; i++)
                dataIds[i] = SampleHelp.EscapeSampleId(dataIds[i]);


            IEnumerable<int> sampleIndices;

            if (sampleChooser != null)
            {
                // Allow the user to choose from the list
                sampleIndices = sampleChooser(filePath, dataIds);
                if (sampleIndices == null)
                    return null;
            }
            else
            {
                int[] indexes = new int[dataIds.Length];
                for (int i = 0; i < dataIds.Length; i++)
                    indexes[i] = i;
                sampleIndices = indexes;
            }

            // Encode sub-paths
            var listPaths = new List<string>();
            foreach (int sampleIndex in sampleIndices)
                listPaths.Add(SampleHelp.EncodePath(filePath, dataIds[sampleIndex], sampleIndex));

            if (listPaths.Count == 0)
                return null;
            return listPaths.ToArray();
        }
    }
}
