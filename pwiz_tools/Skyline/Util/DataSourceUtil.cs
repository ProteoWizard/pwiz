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
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    public static class DataSourceUtil
    {
        // ReSharper disable LocalizableElement
        public const string EXT_THERMO_RAW = ".raw";
        public const string EXT_WIFF = ".wiff";
        public const string EXT_WIFF2 = ".wiff2";
        public const string EXT_SHIMADZU_RAW = ".lcd";
        public const string EXT_MZXML =  ".mzxml";
        public const string EXT_MZDATA = ".mzdata";
        public const string EXT_MZML = ".mzml";
        public const string EXT_MZ5 = ".mz5";
        public const string EXT_XML = ".xml";
        public const string EXT_UIMF = ".uimf";
        public const string EXT_WATERS_RAW = ".raw";
        public const string EXT_AGILENT_BRUKER_RAW = ".d";
        public const string EXT_MOBILION_MBI = ".mbi";
        public static readonly string[] EXT_FASTA = {".fasta", ".fa", ".faa"};

        public const string TYPE_WIFF = "Sciex WIFF/WIFF2";
        public const string TYPE_AGILENT = "Agilent MassHunter Data";
        public const string TYPE_BRUKER = "Bruker BAF/TDF";
        public const string TYPE_SHIMADZU = "Shimadzu LCD";
        public const string TYPE_THERMO_RAW = "Thermo RAW";
        public const string TYPE_WATERS_RAW = "Waters RAW";
        public const string TYPE_MZML = "mzML";
        public const string TYPE_MZXML = "mzXML";
        public const string TYPE_MZ5 = "mz5";
        public const string TYPE_MZDATA = "mzData";
        public const string TYPE_UIMF = "Unified Ion Mobility Frame";
        public const string TYPE_MBI = "Mobilion MBI";
        public const string TYPE_CHORUSRESPONSE = "Chorus Response";
        public const string FOLDER_TYPE = "File Folder";
        public const string UNKNOWN_TYPE = "unknown";
        // ReSharper restore LocalizableElement

        public static bool IsDataSource(string path)
        {
            return IsDataSource(new FileInfo(path)) || IsDataSource(new DirectoryInfo(path));
        }

        public static bool IsDataSource(DirectoryInfo dirInfo)
        {
            return !Equals(GetSourceType(dirInfo), FOLDER_TYPE);
        }

        public static string GetSourceType(DirectoryInfo dirInfo)
        {
            // ReSharper disable LocalizableElement
            try
            {
                if (dirInfo.HasExtension(EXT_WATERS_RAW) &&
                        dirInfo.GetFiles("_FUNC*.DAT").Length > 0)
                    return TYPE_WATERS_RAW;
                if (dirInfo.HasExtension(EXT_AGILENT_BRUKER_RAW))
                {
                    if (dirInfo.GetDirectories("AcqData").Length > 0)
                        return TYPE_AGILENT;
                    if (dirInfo.GetFiles("analysis.baf").Length > 0 || 
                        dirInfo.GetFiles("analysis.tdf").Length > 0) // TIMS ion mobility data
                        return TYPE_BRUKER;
                }
                return FOLDER_TYPE;
            }
            // ReSharper restore LocalizableElement
            catch (Exception)
            {
                // TODO: Folder without access type
                return FOLDER_TYPE;
            }
        }

        private static bool HasExtension(this DirectoryInfo dirInfo, string ext)
        {
            var startIndex = dirInfo.Name.Length - ext.Length;
            return startIndex >= 0 && dirInfo.Name.Substring(startIndex).ToLowerInvariant().Equals(ext);
        }

        public static bool IsDataSource(FileInfo fileInfo)
        {
            return !Equals(GetSourceType(fileInfo), UNKNOWN_TYPE);
        }

        public static string GetSourceType(FileInfo fileInfo)
        {
            switch (fileInfo.Extension.ToLowerInvariant())
            {
                case EXT_THERMO_RAW: return TYPE_THERMO_RAW;
                case EXT_WIFF: return TYPE_WIFF;
                case EXT_WIFF2: return TYPE_WIFF;
                case EXT_SHIMADZU_RAW: return TYPE_SHIMADZU;
                //case ".mgf": return "Mascot Generic";
                //case ".dta": return "Sequest DTA";
                //case ".yep": return "Bruker YEP";
                //case ".baf": return "Bruker BAF";
                //case ".ms2": return "MS2";
                case EXT_MZXML: return TYPE_MZXML;
                case EXT_MZDATA: return TYPE_MZDATA;
                case EXT_MZML: return TYPE_MZML;
                case EXT_MZ5: return TYPE_MZ5;
                case EXT_XML: return GetSourceTypeFromXML(fileInfo.FullName);
                case EXT_UIMF: return TYPE_UIMF;
                case EXT_MOBILION_MBI: return TYPE_MBI;
                default: return UNKNOWN_TYPE;
            }
        }

        public static bool IsWiffFile(MsDataFileUri fileName)
        {
            MsDataFilePath msDataFilePath = fileName as MsDataFilePath;
            return null != msDataFilePath && IsWiffFile(msDataFilePath.FilePath);
        }

        public static bool IsWiffFile(string filePath)
        {
            return PathEx.HasExtension(filePath, EXT_WIFF);
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
            using (var stream = new StreamReader(filepath, true))
            using (var reader = XmlReader.Create(stream, settings))
            {
                try
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            // ReSharper disable LocalizableElement
                            switch (reader.Name.ToLowerInvariant())
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
                            // ReSharper restore LocalizableElement
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
        public static IEnumerable<KeyValuePair<string, MsDataFileUri[]>> GetDataSources(string dirRoot, bool addSourcesInSubDirs = true)
        {
            return GetDataSources(dirRoot, true, addSourcesInSubDirs);
        }

        public static IEnumerable<KeyValuePair<string, MsDataFileUri[]>> GetDataSourcesInSubdirs(string dirRoot)
        {
            return GetDataSources(dirRoot, false, true);
        }

        private static IEnumerable<KeyValuePair<string, MsDataFileUri[]>> GetDataSources(string dirRoot, bool addSourcesInRootDir,
            bool addSourcesInSubDirs)
        {
            var listNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();

            if (addSourcesInSubDirs)
            {
                var dirRootInfo = new DirectoryInfo(dirRoot);
                foreach (var subDirInfo in GetDirectories(dirRootInfo))
                {
                    var listDataPaths = new List<MsDataFileUri>();
                    foreach (var dataDirInfo in GetDirectories(subDirInfo))
                    {
                        if (IsDataSource(dataDirInfo))
                            listDataPaths.Add(new MsDataFilePath(dataDirInfo.FullName));
                    }
                    foreach (var dataFileInfo in GetFiles(subDirInfo))
                    {
                        if (IsDataSource(dataFileInfo))
                            listDataPaths.Add(new MsDataFilePath(dataFileInfo.FullName));
                    }
                    if (listDataPaths.Count == 0)
                        continue;

                    listDataPaths.Sort();
                    listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(
                                           subDirInfo.Name, listDataPaths.ToArray()));
                }
            }

            if (addSourcesInRootDir)
            {
                // get a list of all valid files in this directory
                var dirInfo = new DirectoryInfo(dirRoot);

                // This if for WATERS(.raw) and AGILENT(.d) data directories
                foreach (var dataDirInfo in GetDirectories(dirInfo))
                {
                    if (IsDataSource(dataDirInfo))
                    {
                        string dataSource = dataDirInfo.FullName;
                        listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(
                                       Path.GetFileNameWithoutExtension(dataSource), new MsDataFileUri[] { new MsDataFilePath(dataSource),  }));
                    }
                }

                foreach (var dataFileInfo in GetFiles(dirInfo))
                {
                    if (IsDataSource(dataFileInfo))
                    {
                        string dataSource = dataFileInfo.FullName;
                        // Only .wiff files currently support multiple samples per file.
                        // Keep from doing the extra work on other types.
                        if (IsWiffFile(dataSource))
                        {
                            MsDataFilePath[] paths = GetWiffSubPaths(dataSource);
                            if (paths == null)
                                return null;    // An error occurred
                            // Multiple paths then add as samples
                            if (paths.Length > 1 ||
                                // If just one, make sure it has a sample part.  Otherwise,
                                // drop through to add the entire file.
                                (paths.Length == 1 && paths[0].SampleName != null))
                            {
                                foreach (var path in paths)
                                {
                                    listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(
                                                           path.SampleName, new MsDataFileUri[]{ path }));
                                }
                                continue;
                            }
                        }

                        listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(
                                       Path.GetFileNameWithoutExtension(dataSource), new MsDataFileUri[] { new MsDataFilePath(dataSource) }));
                    }
                }
            }

            listNamedPaths.Sort((p1, p2) => Comparer<string>.Default.Compare(p1.Key, p2.Key));
            return listNamedPaths;
        }

        private static IEnumerable<DirectoryInfo> GetDirectories(DirectoryInfo dirInfo)
        {
            try
            {
                return dirInfo.GetDirectories();
            }
            catch (Exception)
            {
                // Just ignore directories that throw exceptions
                return new DirectoryInfo[0];
            }
        }

        private static IEnumerable<FileInfo> GetFiles(DirectoryInfo dirInfo)
        {
            try
            {
                return dirInfo.GetFiles();
            }
            catch (Exception)
            {
                // Just ignore directories that throw exceptions
                return new FileInfo[0];
            }
        }

        private static MsDataFilePath[] GetWiffSubPaths(string filePath)
        {
            string[] dataIds;
            try
            {
                dataIds = MsDataFileImpl.ReadIds(filePath);
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(
                    string.Format(Resources.DataSourceUtil_GetWiffSubPaths_An_error_occurred_attempting_to_read_sample_information_from_the_file__0__,filePath),
                                    Resources.DataSourceUtil_GetWiffSubPaths_The_file_may_be_corrupted_missing_or_the_correct_libraries_may_not_be_installed,
                                    x.Message);
                throw new IOException(message);
            }

            return GetWiffSubPaths(filePath, dataIds, null);
        }

        public static MsDataFilePath[] GetWiffSubPaths(string filePath, string[] dataIds, Func<string, string[], IEnumerable<int>> sampleChooser)
        {
            if (dataIds == null)
                return null;
            // WIFF without at least 2 samples just use its file name.
            if (dataIds.Length < 2)
                return new[] { new MsDataFilePath(filePath, null, -1),  };

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
            var listPaths = new List<MsDataFilePath>();
            foreach (int sampleIndex in sampleIndices)
                listPaths.Add(new MsDataFilePath(filePath, dataIds[sampleIndex], sampleIndex));

            if (listPaths.Count == 0)
                return null;
            return listPaths.ToArray();
        }

        /// <summary>
        /// If the passed in MsDataFileUri is a multi-sample wiff file, then return a list of
        /// MsDataFileUri's representing the samples, otherwise, return the MsDataFileUri itself.
        /// </summary>
        public static IEnumerable<MsDataFileUri> ListSubPaths(MsDataFileUri msDataFileUri)
        {
            var msDataFilePath = msDataFileUri as MsDataFilePath;
            if (msDataFilePath == null || !IsWiffFile(msDataFilePath.FilePath))
            {
                return new[] {msDataFileUri};
            }
            return GetWiffSubPaths(msDataFilePath.FilePath);
        }
    }
}
