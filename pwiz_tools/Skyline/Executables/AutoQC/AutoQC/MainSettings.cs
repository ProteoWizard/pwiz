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

namespace AutoQC
{
    [XmlRoot("main_settings")]
    public class MainSettings : IXmlSerializable, IConfigSettings
    {
        public const int ACCUM_TIME_WINDOW = 31;
        public const int ACQUISITION_TIME = 75;
        public const string THERMO = "Thermo";
        public const string WATERS = "Waters";
        public const string SCIEX = "SCIEX";
        public const string AGILENT = "Agilent";
        //public const string BRUKER = "Bruker";R
        //public const string SCHIMADZU = "Schimadzu";

        public string SkylineFilePath { get; set; }

        public string SkylineFileDir
        {
            get { return string.IsNullOrEmpty(SkylineFilePath) ? "" : Path.GetDirectoryName(SkylineFilePath); }
        }

        public string FolderToWatch { get; set; }

        public string QcFilePattern { get; set; }

        public Regex QcFileRegex { get; set; }

        public int ResultsWindow { get; set; }

        public string InstrumentType { get; set; }

        public int AcquisitionTime { get; set; }


        public DateTime LastAcquiredFileDate { get; set; } // Not saved to Properties.Settings
        public DateTime LastArchivalDate { get; set; }


        public static MainSettings GetDefault()
        {
            var settings = new MainSettings
            {
                InstrumentType = THERMO,
                ResultsWindow = ACCUM_TIME_WINDOW,
                AcquisitionTime = ACQUISITION_TIME
            };
            return settings;
        }

        public MainSettings Clone()
        {
            return new MainSettings
            {
                SkylineFilePath = SkylineFilePath,
                FolderToWatch = FolderToWatch,
                QcFilePattern = QcFilePattern,
                ResultsWindow = ResultsWindow,
                AcquisitionTime = AcquisitionTime,
                InstrumentType = InstrumentType
            };
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
            sb.Append("QC file pattern: ").AppendLine(QcFilePattern);
            sb.Append("Instrument: ").AppendLine(InstrumentType);
            sb.Append("Results window: ").Append(ResultsWindow.ToString()).AppendLine(" days");
            sb.Append("Acquisition time: ").Append(AcquisitionTime.ToString()).AppendLine(" minutes");
            return sb.ToString();
        }

        public void ValidateSettings()
        {
            // Path to the Skyline file.
            if (string.IsNullOrWhiteSpace(SkylineFilePath))
            {
                throw new ArgumentException("Please specify path to a Skyline file.");
            }
            if (!File.Exists(SkylineFilePath))
            {
                throw new ArgumentException(string.Format("Skyline file {0} does not exist.", SkylineFilePath));
            }

            // Path to the folder to monitor for mass spec. results files
            if (string.IsNullOrWhiteSpace(FolderToWatch))
            {
                throw new ArgumentException("Please specify path to a folder where mass spec. files will be written.");
            }
            if (!Directory.Exists(FolderToWatch))
            {
                throw new ArgumentException(string.Format("Folder to watch: {0} does not exist.", FolderToWatch));
            }

            // Regular expression for QC file names (optional)
            if (!string.IsNullOrWhiteSpace(QcFilePattern))
            {
                // Validate the regular expression
                try
                {
                    // TODO: Should we ignore case?
                    QcFileRegex = new Regex(QcFilePattern, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException("Invalid regular expression for QC results file names", e);
                }
            }

            // Results time window.
            if (ResultsWindow < ACCUM_TIME_WINDOW)
            {
                throw new ArgumentException(string.Format("\"Accumulation time window\" cannot be less than {0} days.",
                    ACCUM_TIME_WINDOW));
            }
            try
            {
               DateTime.Now.AddDays(-(ResultsWindow - 1));
            }
            catch (ArgumentOutOfRangeException x)
            {
                throw new ArgumentException("Results time window is too big.");
            }

            // Expected acquisition time
            if (AcquisitionTime < 0)
            {
                throw new ArgumentException("\"Expected acquisition time\" cannot be less than 0 minutes.");
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

            string importOnOrAfter = "";
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

                // Add arguments to remove files older than the start of the rolling window.   
                args.Append(string.Format(" --remove-before={0}", accumulationWindow.StartDate.ToShortDateString()));
            }

            // Add arguments to import the results file
            args.Append(string.Format(" --import-file=\"{0}\"{1}", importContext.GetCurrentFile(), importOnOrAfter));

            // Save the Skyline file
            args.Append(" --save");

            return args.ToString();
        }

        public virtual ProcessInfo RunBefore(ImportContext importContext)
        {
            string archiveArgs = null;
            if (!importContext.ImportExisting)
            {
                // If we are NOT importing existing results, create an archive (if required) of the 
                // Skyline document BEFORE importing a results file.
                archiveArgs = GetArchiveArgs(GetLastArchivalDate(), DateTime.Today);
            }
            if (string.IsNullOrEmpty(archiveArgs))
            {
                return null;
            }
            var args = string.Format("--in=\"{0}\" {1}", SkylineFilePath, archiveArgs);
            return new ProcessInfo(MainForm.SkylineRunnerPath, MainForm.SKYLINE_RUNNER, args, args);
        }

        public virtual ProcessInfo RunAfter(ImportContext importContext)
        {
            string archiveArgs = null;
            var currentDate = DateTime.Today;
            if (importContext.ImportExisting && importContext.ImportingLast())
            {
                // If we are importing existing files in the folder, create an archive (if required) of the 
                // Skyline document AFTER importing the last results file.
                var oldestFileDate = importContext.GetOldestImportedFileDate(LastAcquiredFileDate);
                var today = DateTime.Today;
                if (oldestFileDate.Year < today.Year || oldestFileDate.Month < today.Month)
                {
                    archiveArgs = GetArchiveArgs(currentDate.AddMonths(-1), currentDate);
                }
            }
            if (string.IsNullOrEmpty(archiveArgs))
            {
                return null;
            }
            var args = string.Format("--in=\"{0}\" {1}", SkylineFilePath, archiveArgs);
            return new ProcessInfo(MainForm.SkylineRunnerPath, MainForm.SKYLINE_RUNNER, args, args);
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

        private DateTime GetLastArchivalDate()
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

        private enum ATTR
        {
            skyline_file_path,
            folder_to_watch,
            qc_file_pattern,
            results_window,
            instrument_type,
            acquisition_time
        };

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            SkylineFilePath = reader.GetAttribute(ATTR.skyline_file_path);
            FolderToWatch = reader.GetAttribute(ATTR.folder_to_watch);
            QcFilePattern = reader.GetAttribute(ATTR.qc_file_pattern);
            ResultsWindow = reader.GetIntAttribute(ATTR.results_window);
            InstrumentType = reader.GetAttribute(ATTR.instrument_type);
            AcquisitionTime = reader.GetIntAttribute(ATTR.acquisition_time);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("main_settings");
            writer.WriteAttributeIfString(ATTR.skyline_file_path, SkylineFilePath);
            writer.WriteAttributeIfString(ATTR.folder_to_watch, FolderToWatch);
            writer.WriteAttributeIfString(ATTR.qc_file_pattern, QcFilePattern);
            writer.WriteAttributeNullable(ATTR.results_window, ResultsWindow);
            writer.WriteAttributeIfString(ATTR.instrument_type, InstrumentType);
            writer.WriteAttributeNullable(ATTR.acquisition_time, AcquisitionTime);
            writer.WriteEndElement();
        }
        #endregion

        #region Equality members

        protected bool Equals(MainSettings other)
        {
            return string.Equals(SkylineFilePath, other.SkylineFilePath) 
                && string.Equals(FolderToWatch, other.FolderToWatch) 
                && string.Equals(QcFilePattern, other.QcFilePattern) 
                && ResultsWindow == other.ResultsWindow 
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
                hashCode = (hashCode*397) ^ (QcFilePattern != null ? QcFilePattern.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ ResultsWindow;
                hashCode = (hashCode*397) ^ (InstrumentType != null ? InstrumentType.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ AcquisitionTime;
                hashCode = (hashCode*397) ^ LastAcquiredFileDate.GetHashCode();
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