using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AutoQC
{
    public class MainSettings: TabSettings
    {
        private const int ACCUM_TIME_WINDOW = 31;

        public DateTime LastArchivalDate { get; set; }
        public string SkylineFilePath { get; set; }
        public string FolderToWatch { get; set; }
        private int AccumulationWindow { get; set; }
        private string InstrumentType { get; set; }
        public bool ImportExistingFiles { get; set; }

        public override void InitializeFromDefaultSettings()
        {
            SkylineFilePath = Properties.Settings.Default.SkylineFilePath;
            MainForm.textSkylinePath.Text = SkylineFilePath;

            FolderToWatch = Properties.Settings.Default.FolderToWatch;
            MainForm.textFolderToWatchPath.Text = FolderToWatch;

            var accumWin = Properties.Settings.Default.AccumulationWindow;
            AccumulationWindow =  accumWin == 0 ? ACCUM_TIME_WINDOW : accumWin; 
            MainForm.textAccumulationTimeWindow.Text = AccumulationWindow.ToString();

            var instrumentType = Properties.Settings.Default.InstrumentType;
            InstrumentType = string.IsNullOrEmpty(instrumentType) ? "Thermo" : instrumentType;
            MainForm.comboBoxInstrumentType.SelectedItem = InstrumentType;

            ImportExistingFiles = Properties.Settings.Default.ImportExistingFiles;
            MainForm.cbImportExistingFiles.Checked = ImportExistingFiles;
        }

        public override bool IsSelected()
        {
            return true;
        }

        public override bool ValidateSettings()
        {
            LogOutput("Validating settings...");
            var error = false;
            var path = MainForm.textSkylinePath.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                LogErrorOutput("Please specify path to a Skyline file.");
                error = true;
            }
            else if (!File.Exists(path))
            {
                LogErrorOutput(string.Format("Skyline file {0} does not exist.", path));
                error = true;
            }

            path = MainForm.textFolderToWatchPath.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                LogErrorOutput("Please specify path to a folder where mass spec. files will be written.");
                error = true;
            }
            else if (!Directory.Exists(path))
            {
                LogErrorOutput(string.Format("Folder {0} does not exist.", path));
                error = true;
            }


            if (string.IsNullOrWhiteSpace(MainForm.textAccumulationTimeWindow.Text))
            {
                LogErrorOutput("Please specify a value for the \"Accumulation time window\".");
                error = true;
            }
            else
            {
                int accumWindow;
                if (!Int32.TryParse(MainForm.textAccumulationTimeWindow.Text, out accumWindow))
                {
                    LogErrorOutput(string.Format("Invalid value for \"Accumulation time window\": {0}.",
                        MainForm.textAccumulationTimeWindow.Text));
                    error = true;
                }
                else if (accumWindow < ACCUM_TIME_WINDOW)
                {
                    LogErrorOutput(string.Format("\"Accumulation time window\" cannot be less than {0} days.", ACCUM_TIME_WINDOW));
                    error = true;
                }
            }
            return !error;
        }

        public override void SaveSettings()
        {
            SkylineFilePath = MainForm.textSkylinePath.Text;
            Properties.Settings.Default.SkylineFilePath = SkylineFilePath;

            FolderToWatch = MainForm.textFolderToWatchPath.Text;
            Properties.Settings.Default.FolderToWatch = FolderToWatch;

            AccumulationWindow = Convert.ToInt32(MainForm.textAccumulationTimeWindow.Text);
            Properties.Settings.Default.AccumulationWindow = AccumulationWindow;

            InstrumentType = MainForm.comboBoxInstrumentType.SelectedText;
            Properties.Settings.Default.InstrumentType = InstrumentType;

            ImportExistingFiles = MainForm.cbImportExistingFiles.Checked;
            Properties.Settings.Default.ImportExistingFiles = ImportExistingFiles;
        }

        public override IEnumerable<string> SkylineRunnerArgs(ImportContext importContext, bool toPrint = false)
        {
            // Get the current accumulation window
            var currentDate = DateTime.Today;
            var accumulationWindow = Window.GetAccumulationWindow(currentDate, AccumulationWindow);
            Log("Current accumulation window is {0} TO {1}",
                accumulationWindow.StartDate.ToShortDateString(), accumulationWindow.EndDate.ToShortDateString());

            // Input Skyline file
            var argLines = new List<string> { string.Format("--in=\"{0}\"", SkylineFilePath) };

            if (importContext.ImportExisting())
            {
                // We are importing existing files in the folder, import regardless of when the file was created.
                argLines.Add(string.Format("--import-file=\"{0}\"", importContext.GetResultsFilePath()));

                if (importContext.ImportingLast())
                {
                    DateTime oldestFileDate = importContext.GetOldestFileDate();
                    DateTime today = DateTime.Today;
                    if (oldestFileDate.AddMonths(1).CompareTo(today) < 0)
                    {
                        AddArchiveArgs(argLines, currentDate.AddMonths(-1), currentDate);
                    }
                }
            }
            else
            {
                // Add arguments to archive, if required
                AddArchiveArgs(argLines, GetLastArchivalDate(), DateTime.Today);

                // Add arguments to roll over
                AddRollOverArgs(argLines, accumulationWindow);

                // Add arguments to import the results file
                argLines.Add(string.Format("--import-on-or-after={1} --import-file=\"{0}\"", importContext.GetResultsFilePath(),
                    accumulationWindow.StartDate.ToShortDateString()));
            }
            
            // Save the Skyline file
            argLines.Add("--save");

            return argLines;
        }

        private DateTime GetLastArchivalDate()
        {
            return GetLastArchivalDate(new FileSystemUtil());
        }

        public DateTime GetLastArchivalDate(IFileSystemUtil fileUtil)
        {
            if (LastArchivalDate.Equals(DateTime.MinValue)) 
            {
                var fileName = Path.GetFileNameWithoutExtension(SkylineFilePath);
                var pattern = fileName + "_\\d{4}_\\d{2}.sky.zip";
                var regex = new Regex(pattern);

                var skylineFileDir = Path.GetFileNameWithoutExtension(SkylineFilePath);
                Debug.Assert(skylineFileDir != null);

                // Look at any existing .sky.zip files to determine the last archival date
                // Look for shared zip files with file names like <skyline_file_name>_<yyyy>_<mm>.sky.zip
//                var archiveFiles =
//                    new DirectoryInfo(skylineFileDir).GetFiles("*.sky.zip").Where(f => regex.IsMatch(f.Name)).OrderBy(f => f.LastWriteTime).ToList();
                var archiveFiles =
                    fileUtil.GetSkyZipFiles(skylineFileDir)
                        .Where(f => regex.IsMatch(Path.GetFileName(f) ?? string.Empty)).OrderBy(fileUtil.LastWriteTime).ToList();

                LastArchivalDate = archiveFiles.Any() ? fileUtil.LastWriteTime(archiveFiles.Last()) : DateTime.Today;
            }

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

        public void AddArchiveArgs(ICollection<string> argLines, DateTime archiveDate, DateTime currentDate)
        {
            if (archiveDate.CompareTo(currentDate) > 0)
                return;

            if (currentDate.Year > archiveDate.Year || currentDate.Month > archiveDate.Month)
            {
                // Add args to archive the file: create a shared zip
                var archiveFileName = string.Format("{0}_{1:D4}_{2:D2}.sky.zip",
                    Path.GetFileNameWithoutExtension(SkylineFilePath),
                    archiveDate.Year,
                    archiveDate.Month);

                // Archive file will be written in the same directory as the Skyline file.
                argLines.Add(string.Format("--share-zip --share-file={0}", archiveFileName));       

                LastArchivalDate = currentDate;
            }
        }

        private void AddRollOverArgs(ICollection<string> argLines, Window accumulationWindow)
        {
            // Add arguments to remove files older than the start of the rolling window.   
            argLines.Add((string.Format("--remove-before={0}", accumulationWindow.StartDate.Date)));
        }

        private class Window
        {
            public DateTime StartDate { get; private set; }
            public DateTime EndDate { get; private set; }

            public static Window GetAccumulationWindow(DateTime currentDate, int windowSize)
            {
                var window = new Window
                {
                    EndDate = currentDate,
                    StartDate = currentDate.AddDays(-(windowSize - 1))
                };
                return window;
            }
        }
    }
}