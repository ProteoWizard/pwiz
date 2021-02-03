/*
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
            ResultsWindow = ValidateIntTextField(resultsWindowString, Resources.AutoQcConfigForm_GetMainSettingsFromUI_Results_Window);
            InstrumentType = instrumentType;
            AcquisitionTime = ValidateIntTextField(acquisitionTimeString, Resources.AutoQcConfigForm_GetMainSettingsFromUI_Acquisition_Time);
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
                    Resources.AutoQcConfigForm_ValidateIntTextField_Invalid_value_for___0_____1__, fieldName,
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
                bool isRegex = QcFileFilter is RegexFilter;
                var pattern = QcFileFilter.Pattern;
                if (string.IsNullOrEmpty(pattern))
                {
                    var err = string.Format("Empty {0} for QC file names",
                          isRegex ? "regular expression" : "pattern");
                    throw new ArgumentException(err);  
                }
            }

            // Results time window.
            if (ResultsWindow < ACCUM_TIME_WINDOW)
            {
                throw new ArgumentException(string.Format("\"Results time window\" cannot be less than {0} days.",
                    ACCUM_TIME_WINDOW));
            }
            try
            {
                var unused = DateTime.Now.AddDays(-(ResultsWindow - 1));
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException("Results time window is too big.");
            }

            // Expected acquisition time
            if (AcquisitionTime < 0)
            {
                throw new ArgumentException("\"Expected acquisition time\" cannot be less than 0 minutes.");
            }
        }

        public static void ValidateSkylineFile(string skylineFile)
        {
            if (string.IsNullOrWhiteSpace(skylineFile))
            {
                throw new ArgumentException("Please specify path to a Skyline file.");
            }
            if (!File.Exists(skylineFile))
            {
                throw new ArgumentException(string.Format("Skyline file {0} does not exist.", skylineFile));
            }
        }

        public static void ValidateFolderToWatch(string folderToWatch)
        {
            if(string.IsNullOrWhiteSpace(folderToWatch))
            {
                throw new ArgumentException("Please specify path to a folder where mass spec. files will be written.");
            }
            if (!Directory.Exists(folderToWatch))
            {
                throw new ArgumentException(string.Format("Folder to watch: {0} does not exist.", folderToWatch));
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
                throw new ArgumentException("Results time window size has be greater than 0.");
            }

            DateTime startDate;
            try
            {
                startDate = endWindow.AddDays(-(windowSize - 1));
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException("Results time window is too big.");   
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
            switch (filterType)
            {
                case StartsWithFilter.FilterName:
                    return new StartsWithFilter(pattern);
                case EndsWithFilter.FilterName:
                    return new EndsWithFilter(pattern);
                case ContainsFilter.FilterName:
                    return new ContainsFilter(pattern);
                case RegexFilter.FilterName:
                    return new RegexFilter(pattern);
                default:
                    return new AllFileFilter(string.Empty);
            }
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
        public static readonly string FilterName = "All";

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
        public const string FilterName = "Starts with";

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
        public const string FilterName = "Ends with";

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
        public const string FilterName = "Contains";

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
        public const string FilterName = "Regular expression";

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
                throw new ArgumentException("Invalid regular expression for QC file names", e);
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
            return Directory.GetFiles(dirPath, "*.sky.zip");
        }

        public DateTime LastWriteTime(string filePath)
        {
            return File.GetLastWriteTime(filePath);
        }
    }
}