/*
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoQC
{
    public class MainSettings
    {
        public const int ACCUM_TIME_WINDOW = 31;
        public const string THERMO = "Thermo";
        public const string WATERS = "Waters";
        public const string BRUKER = "Bruker";
        public const string SCIEX = "SCIEX";
        public const string AGILENT = "Agilent";
        public const string SCHIMADZU = "Schimadzu";

        public string SkylineFilePath { get; set; }
        public string FolderToWatch { get; set; }
        public int AccumulationWindow
        {
            get
            {
                int val;
                return Int32.TryParse(AccumulationWindowString, out val) ? val : 0;
            }
        }
        public string AccumulationWindowString { get; set; }
        public string InstrumentType { get; set; }
        public bool ImportExistingFiles { get; set; }
        public int DelayTime { get; set; } // TODO: Add this in the UI

        

        public static MainSettings InitializeFromDefaults()
        {
            var settings = new MainSettings
            {
                SkylineFilePath = Properties.Settings.Default.SkylineFilePath,
                FolderToWatch = Properties.Settings.Default.FolderToWatch,
                ImportExistingFiles = Properties.Settings.Default.ImportExistingFiles
            };

            var accumWin = Properties.Settings.Default.AccumulationWindow;
            settings.AccumulationWindowString = accumWin == 0 ? ACCUM_TIME_WINDOW.ToString() : accumWin.ToString();
            
            var instrumentType = Properties.Settings.Default.InstrumentType;
            settings.InstrumentType = string.IsNullOrEmpty(instrumentType) ? THERMO : instrumentType;

            return settings;
        }

        internal void Save()
        {
            Properties.Settings.Default.SkylineFilePath = SkylineFilePath;

            Properties.Settings.Default.FolderToWatch = FolderToWatch;

            Properties.Settings.Default.AccumulationWindow = AccumulationWindow;

            Properties.Settings.Default.InstrumentType = InstrumentType;

            Properties.Settings.Default.ImportExistingFiles = ImportExistingFiles;
        }
    }

    public class MainSettingsTab: SettingsTab
    {
        public MainSettings Settings { get; set; }

        public DateTime LastArchivalDate { get; set; }

        public MainSettingsTab(IAppControl appControl, IAutoQCLogger logger)
            : base(appControl, logger)
        {
            Settings = new MainSettings();
        }

        public override void InitializeFromDefaultSettings()
        {
            Settings = MainSettings.InitializeFromDefaults();
            _appControl.SetUIMainSettings(Settings);
        }

        public override bool IsSelected()
        {
            return true;
        }

        public override bool ValidateSettings()
        {
            LogOutput("Validating settings...");

            var mainSettingsUI = _appControl.GetUIMainSettings();

            var error = false;

            // Path to the Skyline file.
            var path = mainSettingsUI.SkylineFilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                LogErrorOutput("Please specify path to a Skyline file.");
                error = true;
            }
            else if (!File.Exists(path))
            {
                LogErrorOutput("Skyline file {0} does not exist.", path);
                error = true;
            }

            // Path to the folder to monitor for mass spec. results files
            path = mainSettingsUI.FolderToWatch;
            if (string.IsNullOrWhiteSpace(path))
            {
                LogErrorOutput("Please specify path to a folder where mass spec. files will be written.");
                error = true;
            }
            else if (!Directory.Exists(path))
            {
                LogErrorOutput("Folder {0} does not exist.", path);
                error = true;
            }

            // Accumulation window.
            var accumWin = mainSettingsUI.AccumulationWindowString;
            if (string.IsNullOrWhiteSpace(accumWin))
            {
                LogErrorOutput("Please specify a value for the \"Accumulation time window\".");
                error = true;
            }
            else
            {
                int accumWindow;
                if (!Int32.TryParse(mainSettingsUI.AccumulationWindowString, out accumWindow))
                {
                    LogErrorOutput("Invalid value for \"Accumulation time window\": {0}.",
                        mainSettingsUI.AccumulationWindowString);
                    error = true;
                }
                else if (accumWindow < MainSettings.ACCUM_TIME_WINDOW)
                {
                    LogErrorOutput("\"Accumulation time window\" cannot be less than {0} days.", MainSettings.ACCUM_TIME_WINDOW);
                    error = true;
                }
            }
            if (!error) Settings = mainSettingsUI;
            return !error;
        }

        public override void SaveSettings()
        {
            Settings.Save();
        }

        public override string SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            // Get the current accumulation window
            var currentDate = DateTime.Today;
            var accumulationWindow = AccumulationWindow.Get(currentDate, Settings.AccumulationWindow);
            if (toPrint)
            {
                Log("Current accumulation window is {0} TO {1}",
                    accumulationWindow.StartDate.ToShortDateString(), accumulationWindow.EndDate.ToShortDateString());
            }


            var args = new StringBuilder();
            // Input Skyline file
            args.Append(string.Format(" --in=\"{0}\"", Settings.SkylineFilePath));


            if (importContext.ImportExisting)
            {
                // We are importing existing files in the folder, import regardless of when the file was created.
                args.Append(string.Format(" --import-file=\"{0}\"", importContext.GetCurrentFile()));
            }
            else
            {
                // Add arguments to remove files older than the start of the rolling window.   
                args.Append(string.Format(" --remove-before={0}", accumulationWindow.StartDate.ToShortDateString()));

                // Add arguments to import the results file
                args.Append(string.Format(" --import-on-or-after={1} --import-file=\"{0}\"", importContext.GetCurrentFile(),
                    accumulationWindow.StartDate.ToShortDateString()));
                // args.Append(string.Format(" --import-file=\"{0}\"", importContext.GetCurrentFile()));
            }
            
            // Save the Skyline file
            args.Append(" --save");

            return args.ToString();
        }

        public override ProcessInfo RunBefore(ImportContext importContext)
        {
            string archiveArgs = null;
            if (!importContext.ImportExisting)
            {
                // If we are NOT importing existing results, create an archive (if required) of the 
                // Skyline document BEFORE importing a results file.
                archiveArgs = GetArchiveArgs(GetLastArchivalDate(), DateTime.Today);
            }
            if (String.IsNullOrEmpty(archiveArgs))
            {
                return null;
            }
            var args = string.Format("--in=\"{0}\" {1}", Settings.SkylineFilePath, archiveArgs);
            return new ProcessInfo(AutoQCForm.SkylineRunnerPath, args);
        }

        public override ProcessInfo RunAfter(ImportContext importContext)
        {
            string archiveArgs = null;
            var currentDate = DateTime.Today;
            if (importContext.ImportExisting && importContext.ImportingLast())
            {
                // If We are importing existing files in the folder, create an archive (if required) of the 
                // Skyline document AFTER importing the last results file.
                var oldestFileDate = importContext.GetOldestFileDate();
                var today = DateTime.Today;
                if(oldestFileDate.Year < today.Year || oldestFileDate.Month < today.Month)
                {
                    archiveArgs = GetArchiveArgs(currentDate.AddMonths(-1), currentDate);
                }
            }
            if (String.IsNullOrEmpty(archiveArgs))
            {
                return null;
            }
            var args = string.Format("--in=\"{0}\" {1}", Settings.SkylineFilePath, archiveArgs);
            return new ProcessInfo(AutoQCForm.SkylineRunnerPath, args);
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

            var fileName = Path.GetFileNameWithoutExtension(Settings.SkylineFilePath);
            var pattern = fileName + "_\\d{4}_\\d{2}.sky.zip";
            var regex = new Regex(pattern);

            var skylineFileDir = Path.GetDirectoryName(Settings.SkylineFilePath);
            Debug.Assert(skylineFileDir != null);

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

        public interface IFileSystemUtil
        {
            IEnumerable<string> GetSkyZipFiles(string dirPath);
            DateTime LastWriteTime(string filePath);
        }

        public class FileSystemUtil: IFileSystemUtil
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
                Path.GetFileNameWithoutExtension(Settings.SkylineFilePath),
                archiveDate.Year,
                archiveDate.Month);

            LastArchivalDate = currentDate;

            // Archive file will be written in the same directory as the Skyline file.
            return string.Format("--share-zip={0}", archiveFileName);
        }

        public class AccumulationWindow
        {
            public DateTime StartDate { get; private set; }
            public DateTime EndDate { get; private set; }

            public static AccumulationWindow Get(DateTime endWindow, int windowSize)
            {
                if (windowSize < 1)
                {
                    throw new ArgumentException("Window size has be greater than 0.");
                }
                var window = new AccumulationWindow
                {
                    EndDate = endWindow,
                    StartDate = endWindow.AddDays(-(windowSize - 1))
                };
                return window;
            }
        }
    }
}