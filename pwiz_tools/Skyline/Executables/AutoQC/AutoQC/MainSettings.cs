﻿/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    [XmlRoot("main_settings")]
    public class MainSettings
    {
        public const int ACCUM_TIME_WINDOW = 31;
        public const int ACQUISITION_TIME = 75;
        public const string THERMO = "Thermo";
        public const string WATERS = "Waters";
        public const string SCIEX = "SCIEX";
        public const string AGILENT = "Agilent";
        public const string BRUKER = "Bruker";
        public const string SHIMADZU = "Shimadzu";

        // Default getters
        public static FileFilter GetDefaultQcFileFilter() { return FileFilter.GetFileFilter(AllFileFilter.FilterName, string.Empty); }
        public static bool GetDefaultRemoveResults() { return true; }
        public static string GetDefaultResultsWindow() { return ACCUM_TIME_WINDOW.ToString(); }
        public static string GetDefaultInstrumentType() { return THERMO; }
        public static string GetDefaultAcquisitionTime() { return ACQUISITION_TIME.ToString(); }


        public readonly string SkylineFilePath;
        public readonly string FolderToWatch;
        public readonly bool IncludeSubfolders;
        public readonly FileFilter QcFileFilter;
        public readonly bool RemoveResults;
        public readonly int ResultsWindow;
        public readonly string InstrumentType;
        public readonly int AcquisitionTime;


        public DateTime LastAcquiredFileDate; // Not saved to Properties.Settings
        public DateTime LastArchivalDate; // TODO: finish making readonly


        public MainSettings(string skylineFilePath, string folderToWatch, bool includeSubFolders, FileFilter qcFileFilter, 
            bool removeResults, string resultsWindowString, string instrumentType, string acquisitionTimeString)
        {
            SkylineFilePath = skylineFilePath;
            FolderToWatch = folderToWatch;
            IncludeSubfolders = includeSubFolders;
            QcFileFilter = qcFileFilter;
            RemoveResults = removeResults;
            ResultsWindow = ValidateIntTextField(resultsWindowString, Resources.MainSettings_MainSettings_results_window);
            InstrumentType = instrumentType;
            AcquisitionTime = ValidateIntTextField(acquisitionTimeString, Resources.MainSettings_MainSettings_acquisition_time);
        }

        public virtual bool IsSelected()
        {
            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Skyline file: ").AppendLine(SkylineFilePath);
            sb.Append("Folder to watch: ").AppendLine(FolderToWatch);
            sb.Append("Include subfolders: ").AppendLine(IncludeSubfolders.ToString());
            sb.AppendLine(QcFileFilter.ToString());
            sb.Append("Instrument: ").AppendLine(InstrumentType);
            sb.Append("Results window: ").Append(ResultsWindow.ToString()).AppendLine(" days");
            sb.Append("Acquisition time: ").Append(AcquisitionTime.ToString()).AppendLine(" minutes");
            return sb.ToString();
        }

        private int ValidateIntTextField(string textToParse, string fieldName)
        {
            int parsedInt;
            if (!Int32.TryParse(textToParse, out parsedInt))
            {
                throw new ArgumentException(string.Format(
                    Resources.MainSettings_ValidateIntTextField_Invalid_value_for__0____1_, fieldName,
                    textToParse));
            }
            return parsedInt;
        }

        public void ValidateSettings()
        {
            // Path to the Skyline file.
            ValidateSkylineFile(SkylineFilePath);

            // Path to the folder to monitor for mass spec. results files
            ValidateFolderToWatch(FolderToWatch);

            // File filter
            if (!(QcFileFilter is AllFileFilter))
            {
                var pattern = QcFileFilter.Pattern;
                if (string.IsNullOrEmpty(pattern))
                {
                    var err = Resources.MainSettings_ValidateSettings_The_file_filter_cannot_have_an_empty_pattern_ + Environment.NewLine +
                              Resources.MainSettings_ValidateSettings_Please_enter_a_pattern_;
                    throw new ArgumentException(err);  
                }
            }

            // Results time window.
            if (ResultsWindow < ACCUM_TIME_WINDOW)
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateSettings__Results_time_window__cannot_be_less_than__0__days_,
                    ACCUM_TIME_WINDOW) + Environment.NewLine + 
                    string.Format(Resources.MainSettings_ValidateSettings_Please_enter_a_value_larger_than__0__, ACCUM_TIME_WINDOW));
            }
            try
            {
                var unused = DateTime.Now.AddDays(-(ResultsWindow - 1));
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException(Resources.MainSettings_ValidateSettings_The_results_time_window_is_too_big__Please_enter_a_smaller_number_);
            }

            // Expected acquisition time
            if (AcquisitionTime < 0)
            {
                throw new ArgumentException(Resources.MainSettings_ValidateSettings__Expected_acquisition_time__cannot_be_less_than_0_minutes_ +Environment.NewLine +
                      string.Format(Resources.MainSettings_ValidateSettings_Please_enter_a_value_larger_than__0__, 0));
            }
        }

        public static void ValidateSkylineFile(string skylineFile)
        {
            if (string.IsNullOrWhiteSpace(skylineFile))
            {
                throw new ArgumentException(Resources.MainSettings_ValidateSkylineFile_Skyline_file_name_cannot_be_empty__Please_specify_path_to_a_Skyline_file_);
            }
            if (!File.Exists(skylineFile))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateSkylineFile_The_Skyline_file__0__does_not_exist_, skylineFile) + Environment.NewLine +
                                            Resources.MainSettings_ValidateSkylineFile_Please_enter_a_path_to_an_existing_file_);
            }
        }

        public static void ValidateFolderToWatch(string folderToWatch)
        {
            if(string.IsNullOrWhiteSpace(folderToWatch))
            {
                throw new ArgumentException(Resources.MainSettings_ValidateFolderToWatch_The_folder_to_watch_cannot_be_empty__Please_specify_path_to_a_folder_where_mass_spec__files_will_be_written_);
            }
            if (!Directory.Exists(folderToWatch))
            {
                throw new ArgumentException(string.Format(Resources.MainSettings_ValidateFolderToWatch_The_folder_to_watch___0__does_not_exist_, folderToWatch) + Environment.NewLine +
                                            Resources.MainSettings_ValidateFolderToWatch_Please_enter_a_path_to_an_existing_folder_);
            }
        }

        public virtual string SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            // Get the current results time window
            var currentDate = DateTime.Today;
            var accumulationWindow = AccumulationWindow.Get(currentDate, ResultsWindow);

            var args = new StringBuilder();
            // Input Skyline file
            args.Append(string.Format(" --in=\"{0}\"", SkylineFilePath));

            string importOnOrAfter = string.Empty;
            if (importContext.ImportExisting)
            {
                // We are importing existing files in the folder.  The import-on-or-after is determined
                // by the last acquisition date on the files already imported in the Skyline document.
                // If the Skyline document does not have any results files, we will import all existing
                // files in the folder.
                if (LastAcquiredFileDate != DateTime.MinValue)
                {
                    importOnOrAfter = string.Format(" --import-on-or-after={0}", LastAcquiredFileDate);
                }
            }
            else
            {
                importOnOrAfter = string.Format(" --import-on-or-after={0}",
                    accumulationWindow.StartDate.ToShortDateString());

                if (RemoveResults)
                {
                    // Add arguments to remove files older than the start of the rolling window.   
                    args.Append(string.Format(" --remove-before={0}",
                        accumulationWindow.StartDate.ToShortDateString()));
                }
            }

            // Add arguments to import the results file
            args.Append(string.Format(" --import-file=\"{0}\"{1}", importContext.GetCurrentFile(), importOnOrAfter));

            // Save the Skyline file
            args.Append(" --save");

            return args.ToString();
        }

        public string GetArchiveArgs(DateTime archiveDate, DateTime currentDate)
        {
            if (currentDate.CompareTo(archiveDate) < 0)
                return null;

            if (currentDate.Year == archiveDate.Year && currentDate.Month == archiveDate.Month)
            {
                return null;
            }

            // Return args to archive the file: create a shared zip
            var archiveFileName = string.Format("{0}_{1:D4}_{2:D2}.sky.zip",
                Path.GetFileNameWithoutExtension(SkylineFilePath),
                archiveDate.Year,
                archiveDate.Month);

            LastArchivalDate = currentDate;

            // Archive file will be written in the same directory as the Skyline file.
            return string.Format("--share-zip={0}", archiveFileName);
        }

        public DateTime GetLastArchivalDate()
        {
            return GetLastArchivalDate(new FileSystemUtil());
        }

        public DateTime GetLastArchivalDate(IFileSystemUtil fileUtil)
        {
            if (!LastArchivalDate.Equals(DateTime.MinValue))
            {
                return LastArchivalDate;
            }

            if (!LastAcquiredFileDate.Equals(DateTime.MinValue))
            {
                LastArchivalDate = LastAcquiredFileDate;
                return LastArchivalDate;
            }

            var fileName = Path.GetFileNameWithoutExtension(SkylineFilePath);
            var pattern = fileName + "_\\d{4}_\\d{2}.sky.zip";
            var regex = new Regex(pattern);

            var skylineFileDir = Path.GetDirectoryName(SkylineFilePath);

            // Look at any existing .sky.zip files to determine the last archival date
            // Look for shared zip files with file names like <skyline_file_name>_<yyyy>_<mm>.sky.zip
            var archiveFiles =
                fileUtil.GetSkyZipFiles(skylineFileDir)
                    .Where(f => regex.IsMatch(Path.GetFileName(f) ?? string.Empty))
                    .OrderBy(filePath => fileUtil.LastWriteTime(filePath))
                    .ToList();

            LastArchivalDate = archiveFiles.Any() ? fileUtil.LastWriteTime(archiveFiles.Last()) : DateTime.Today;

            return LastArchivalDate;
        }

      
        #region Implementation of IXmlSerializable interface

        private enum Attr
        {
            skyline_file_path,
            folder_to_watch,
            include_subfolders,
            file_filter_type,
            qc_file_pattern,
            remove_results,
            results_window,
            instrument_type,
            acquisition_time
        };

        public XmlSchema GetSchema()
        {
            return null;
        }

        public static MainSettings ReadXml(XmlReader reader)
        {
            var skylineFilePath = reader.GetAttribute(Attr.skyline_file_path);
            var folderToWatch = reader.GetAttribute(Attr.folder_to_watch);
            var includeSubfolders = reader.GetBoolAttribute(Attr.include_subfolders);
            var pattern = reader.GetAttribute(Attr.qc_file_pattern);
            var filterType = reader.GetAttribute(Attr.file_filter_type);
            if (string.IsNullOrEmpty(filterType) && !string.IsNullOrEmpty(pattern))
            {
                // Support for older version where filter type was not written to XML; only regex filters were allowed
                filterType = RegexFilter.FilterName;
            }
            var qcFileFilter = FileFilter.GetFileFilter(filterType, pattern);
            var removeResults = reader.GetBoolAttribute(Attr.remove_results, true);
            var resultsWindow = reader.GetAttribute(Attr.results_window);
            var instrumentType = reader.GetAttribute(Attr.instrument_type);
            var acquisitionTime = reader.GetAttribute(Attr.acquisition_time);


            return new MainSettings(skylineFilePath, folderToWatch, includeSubfolders, 
                qcFileFilter, removeResults, resultsWindow, instrumentType, 
                acquisitionTime);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("main_settings");
            writer.WriteAttributeIfString(Attr.skyline_file_path, SkylineFilePath);
            writer.WriteAttributeIfString(Attr.folder_to_watch, FolderToWatch);
            writer.WriteAttribute(Attr.include_subfolders, IncludeSubfolders);
            writer.WriteAttributeIfString(Attr.qc_file_pattern, QcFileFilter.Pattern);
            writer.WriteAttributeString(Attr.file_filter_type, QcFileFilter.Name());   
            writer.WriteAttribute(Attr.remove_results, RemoveResults, true);
            writer.WriteAttributeNullable(Attr.results_window, ResultsWindow);
            writer.WriteAttributeIfString(Attr.instrument_type, InstrumentType);
            writer.WriteAttributeNullable(Attr.acquisition_time, AcquisitionTime);
            writer.WriteEndElement();
        }
        #endregion

        #region Equality members

        protected bool Equals(MainSettings other)
        {
            return string.Equals(SkylineFilePath, other.SkylineFilePath)
                   && string.Equals(FolderToWatch, other.FolderToWatch)
                   && IncludeSubfolders == other.IncludeSubfolders
                   && Equals(QcFileFilter, other.QcFileFilter)
                   && ResultsWindow == other.ResultsWindow
                   && RemoveResults == other.RemoveResults
                   && string.Equals(InstrumentType, other.InstrumentType)
                   && AcquisitionTime == other.AcquisitionTime
                   && LastAcquiredFileDate.Equals(other.LastAcquiredFileDate);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MainSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (SkylineFilePath != null ? SkylineFilePath.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (FolderToWatch != null ? FolderToWatch.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ IncludeSubfolders.GetHashCode();
                hashCode = (hashCode*397) ^ (QcFileFilter != null ? QcFileFilter.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ ResultsWindow;
                hashCode = (hashCode*397) ^ (InstrumentType != null ? InstrumentType.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ AcquisitionTime;
                return hashCode;
            }
        }

        #endregion
    }

    public class AccumulationWindow
    {
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        public static AccumulationWindow Get(DateTime endWindow, int windowSize)
        {
            if (windowSize < 1)
            {
                throw new ArgumentException(Resources.AccumulationWindow_Get_Results_time_window_size_has_be_greater_than_0_);
            }

            DateTime startDate;
            try
            {
                startDate = endWindow.AddDays(-(windowSize - 1));
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException(Resources.AccumulationWindow_Get_Results_time_window_is_too_big_);   
            }
            
            var window = new AccumulationWindow
            {
                EndDate = endWindow,
                StartDate = startDate
            };
            return window;
        }
    }

    public abstract class FileFilter
    {
        public abstract bool Matches(string path);
        public abstract string Name();
        public string Pattern { get; }

        protected FileFilter(string pattern)
        {
            Pattern = pattern ?? string.Empty;
        }

        public static FileFilter GetFileFilter(string filterType, string pattern)
        {
            if (filterType.Equals(StartsWithFilter.FilterName))
                return new StartsWithFilter(pattern);
            if(filterType.Equals(EndsWithFilter.FilterName))
                return new EndsWithFilter(pattern);
            if(filterType.Equals(ContainsFilter.FilterName))
                return new ContainsFilter(pattern);
            if(filterType.Equals(RegexFilter.FilterName))
                return new RegexFilter(pattern);

            return new AllFileFilter(string.Empty);
        }

        protected static string GetLastPathPartWithoutExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            // We need the last part of the path; it could be a directory or a file
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return Path.GetFileNameWithoutExtension(path); 
        }

        #region Overrides of Object

        public override string ToString()
        {
            var toStr = string.Format("Filter Type: {0}{1}", Name(),
                (!(string.IsNullOrEmpty(Pattern))) ? string.Format("; Pattern: {0}", Pattern) : string.Empty);
            return toStr;
        }

        #endregion

        #region Equality members

        protected bool Equals(FileFilter other)
        {
            return string.Equals(Pattern, other.Pattern);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FileFilter) obj);
        }

        public override int GetHashCode()
        {
            return Pattern != null ? Pattern.GetHashCode() : 0;
        }

        #endregion
    }

    public class AllFileFilter : FileFilter
    {
        public static readonly string FilterName = Resources.AllFileFilter_FilterName_All;

        public AllFileFilter(string pattern)
            : base(pattern)
        {
        }

        #region Overrides of FileFilter
        public override bool Matches(string path)
        {
            return !string.IsNullOrEmpty(path);
        }

        public override string Name()
        {
            return FilterName;
        }

        #endregion
    }

    public class StartsWithFilter: FileFilter
    {
        public static readonly string FilterName = Resources.StartsWithFilter_FilterName_Starts_with;

        public StartsWithFilter(string pattern)
            : base(pattern)
        {
        }

        #region Overrides of FileFilter

        public override bool Matches(string path)
        {
            return GetLastPathPartWithoutExtension(path).StartsWith(Pattern);
        }

        public override string Name()
        {
            return FilterName;
        }

        #endregion
    }

    public class EndsWithFilter : FileFilter
    {
        public static readonly string FilterName = Resources.EndsWithFilter_FilterName_Ends_with;

        public EndsWithFilter(string pattern)
            : base(pattern)
        {
        }

        #region Overrides of FileFilter

        public override bool Matches(string path)
        {
            return GetLastPathPartWithoutExtension(path).EndsWith(Pattern);
        }

        public override string Name()
        {
            return FilterName;
        }
        #endregion
    }

    public class ContainsFilter : FileFilter
    {
        public static readonly string FilterName = Resources.ContainsFilter_FilterName_Contains;

        public ContainsFilter(string pattern)
            : base(pattern)
        {
        }

        #region Overrides of FileFilter

        public override bool Matches(string path)
        {
            return GetLastPathPartWithoutExtension(path).Contains(Pattern);
        }

        public override string Name()
        {
            return FilterName;
        }
        #endregion
    }

    public class RegexFilter : FileFilter
    {
        public static readonly string FilterName = Resources.RegexFilter_FilterName_Regular_expression;

        public readonly Regex Regex;

        public RegexFilter(string pattern)
            : base(pattern)
        {
            // Validate the regular expression
            try
            {
                Regex = new Regex(pattern);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(Resources.RegexFilter_RegexFilter_Invalid_regular_expression_for_QC_file_names, e);
            }  
        }

        #region Overrides of FileFilter

        public override bool Matches(string path)
        {
            return Regex.IsMatch(GetLastPathPartWithoutExtension(path));
        }

        public override string Name()
        {
            return FilterName;
        }
        #endregion
    }

    public interface IFileSystemUtil
    {
        IEnumerable<string> GetSkyZipFiles(string dirPath);
        DateTime LastWriteTime(string filePath);
    }

    public class FileSystemUtil : IFileSystemUtil
    {
        public IEnumerable<string> GetSkyZipFiles(string dirPath)
        {
            return Directory.GetFiles(dirPath, $"*{TextUtil.EXT_SKY_ZIP}");
        }

        public DateTime LastWriteTime(string filePath)
        {
            return File.GetLastWriteTime(filePath);
        }
    }
}