using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AutoQCTest
{
    [TestClass]
    public class AutoQcFileSystemWatcherTest
    {
        [TestMethod]
        public void TestGetExistingFiles()
        {
            // Create a test directory to monitor
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Assert.IsNotNull(dir);

            var testDir = CreateDirectory(dir, "TestAutoQcFileSystemWatcher_1");

            TestGetExistingFilesForInstrument(testDir, MainSettings.THERMO);
            TestGetExistingFilesForInstrument(testDir, MainSettings.SCIEX);
            TestGetExistingFilesForInstrument(testDir, MainSettings.AGILENT);
            TestGetExistingFilesForInstrument(testDir, MainSettings.WATERS);

        }

        private void TestGetExistingFilesForInstrument(string testDir, string instrument)
        {
            var folderToWatch = CreateDirectory(testDir, instrument);

            // Create a .sky files
            var skyFile = CreateFile(folderToWatch, "test.sky");

            List<string> dataFiles;
            SetupTestFolder(folderToWatch, instrument, out dataFiles);

            var watcher = new AutoQCFileSystemWatcher(new TestLogger());
            var mainSettings = MainSettings.GetDefault();
            mainSettings.SkylineFilePath = skyFile;
            mainSettings.IncludeSubfolders = false;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.ValidateSettings();

            watcher.Init(mainSettings);
            var files = watcher.GetExistingFiles();
            Assert.AreEqual(1, files.Count);
            Assert.AreEqual(dataFiles[0], files[0]);

            mainSettings.IncludeSubfolders = true;
            mainSettings.ValidateSettings();

            watcher.Init(mainSettings);
            files = watcher.GetExistingFiles();
            Assert.AreEqual(5, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));
            Assert.IsTrue(files.Contains(dataFiles[1]));
            Assert.IsTrue(files.Contains(dataFiles[2]));
            Assert.IsTrue(files.Contains(dataFiles[3]));
            Assert.IsTrue(files.Contains(dataFiles[4]));

            mainSettings.QcFilePattern = "_QC_";
            mainSettings.IsQcFilePatternRegex = false;
            mainSettings.ValidateSettings();

            watcher.Init(mainSettings);
            files = watcher.GetExistingFiles();
            Assert.AreEqual(2, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));
            Assert.IsTrue(files.Contains(dataFiles[2]));
        }

        private static void SetupTestFolder(string folderToWatch, string instrument, out List<string> dataFiles)
        {
            // Add subfolders
            var sf1 = CreateDirectory(folderToWatch, "One");
            var sf1_1 = CreateDirectory(sf1, "One_1");
            var sf2 = CreateDirectory(folderToWatch, "Two");

            var ext = AutoQCFileSystemWatcher.GetDataFileExt(instrument);

            var dataInDirs = AutoQCFileSystemWatcher.IsDataInDirectories(instrument);

            // Add files in the folders
            dataFiles = new List<string>
            {
                CreateDataFile(folderToWatch, "root_QC_" + ext, dataInDirs),
                CreateDataFile(sf1, "QC_one" + ext, dataInDirs),
                CreateDataFile(sf1_1, "one_1_a_QC_" + ext, dataInDirs),
                CreateDataFile(sf1_1, "one_1_b_QC" + ext, dataInDirs),
                CreateDataFile(sf2, "two_qc_" + ext, dataInDirs)
            };

            // Add files that should not match
            if (!instrument.Equals(MainSettings.THERMO))
            {
                CreateInstrumentFile(sf1, MainSettings.THERMO);
            }
            if (!instrument.Equals(MainSettings.SCIEX))
            {
                CreateInstrumentFile(sf1, MainSettings.SCIEX);
            }
            if (!instrument.Equals(MainSettings.AGILENT))
            {
                CreateInstrumentFile(sf1, MainSettings.AGILENT);
            }
            if (!instrument.Equals(MainSettings.WATERS))
            {
                CreateInstrumentFile(sf1, MainSettings.WATERS);
            }
        }

        [TestMethod]
        public void TestGetNewFiles()
        {
            // Create a test directory to monitor
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Assert.IsNotNull(dir);

            var testDir = CreateDirectory(dir, "TestAutoQcFileSystemWatcher_2");

            TestGetNewFilesForInstrument(testDir, MainSettings.THERMO);
            TestGetNewFilesForInstrument(testDir, MainSettings.SCIEX);
            TestGetNewFilesForInstrument(testDir, MainSettings.AGILENT);
            TestGetNewFilesForInstrument(testDir, MainSettings.WATERS);
        }

        private static void TestGetNewFilesForInstrument(string testDir, string instrument)
        {
            var logger = new TestLogger();

            // folder to watch
            var folderToWatch = CreateDirectory(testDir, instrument);
            // Create a .sky files
            var skyFile = CreateFile(folderToWatch, "test2_a.sky");


            // 1. Look for files in folderToWatchOnly
            var watcher = new AutoQCFileSystemWatcher(logger);
            var mainSettings = MainSettings.GetDefault();
            mainSettings.SkylineFilePath = skyFile;
            mainSettings.IncludeSubfolders = false;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.ValidateSettings();

            watcher.Init(mainSettings);
            watcher.StartWatching(); // Start watching
            Assert.AreEqual(0, watcher.GetExistingFiles().Count); // No existing files

            // Create new files in the folder
            List<string> dataFiles;
            SetupTestFolder(folderToWatch, instrument, out dataFiles);

            // Only one file should have been added to the queue since we are not monitoring sub-folders
            Assert.IsNotNull(watcher.GetFile());
            Assert.IsNull(watcher.GetFile());

            watcher.Stop();

            // 2. Look for files in subfolders
            watcher = new AutoQCFileSystemWatcher(logger);
            // folder to watch
            folderToWatch = CreateDirectory(testDir, instrument + "_2");
            // Create a .sky files
            skyFile = CreateFile(folderToWatch, "test2_b.sky");
            mainSettings.SkylineFilePath = skyFile;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.IncludeSubfolders = true; // watch sub-folders
            mainSettings.ValidateSettings();
            watcher.Init(mainSettings);

            watcher.StartWatching(); // Start watching
            Assert.AreEqual(0, watcher.GetExistingFiles().Count); // No existing files

            dataFiles.Clear();
            SetupTestFolder(folderToWatch, instrument, out dataFiles); // Create new files in the folder

            var files = new List<string>();
            string f;
            while ((f = watcher.GetFile()) != null)
            {
                files.Add(f);
            }
            Assert.AreEqual(5, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));
            Assert.IsTrue(files.Contains(dataFiles[1]));
            Assert.IsTrue(files.Contains(dataFiles[2]));
            Assert.IsTrue(files.Contains(dataFiles[3]));
            Assert.IsTrue(files.Contains(dataFiles[4]));

            //  3. Look for files in subfolders matching a pattern
            watcher = new AutoQCFileSystemWatcher(logger);
            // folder to watch
            folderToWatch = CreateDirectory(testDir, instrument + "_3");
            // Create a .sky files
            skyFile = CreateFile(folderToWatch, "test2_c.sky");
            mainSettings.SkylineFilePath = skyFile;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.IncludeSubfolders = true; // watch sub-folders
            mainSettings.QcFilePattern = "_QC_"; // file name pattern
            mainSettings.IsQcFilePatternRegex = false;
            mainSettings.ValidateSettings();
            watcher.Init(mainSettings);

            watcher.StartWatching(); // Start watching
            Assert.AreEqual(0, watcher.GetExistingFiles().Count); // No existing files

            dataFiles.Clear();
            SetupTestFolder(folderToWatch, instrument, out dataFiles); // Create new files in the folder

            files = new List<string>();
            while ((f = watcher.GetFile()) != null)
            {
                files.Add(f);
            }
            Assert.AreEqual(2, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));
            Assert.IsTrue(files.Contains(dataFiles[2]));
        }

        private static string CreateDirectory(string parent, string dirName)
        {
            var dir = Path.Combine(parent, dirName);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
            Assert.IsFalse(Directory.Exists(dir));
            Directory.CreateDirectory(dir);
            Assert.IsTrue(Directory.Exists(dir));

            return dir;
        }

        private static string CreateFile(string parentDir, string fileName)
        {
            var filePath = Path.Combine(parentDir, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            using (File.Create(filePath))
            {
            }
            Assert.IsTrue(File.Exists(filePath));

            return filePath; 
        }

        private static string CreateDataFile(string parentDir, string fileName, bool isDir)
        {
            return isDir ?  CreateDirectory(parentDir, fileName) : CreateFile(parentDir, fileName);
        }

        private static void CreateInstrumentFile(string parentDir, string instrument)
        {
            CreateDataFile(parentDir, instrument + "_QC_" + AutoQCFileSystemWatcher.GetDataFileExt(instrument),
                                            AutoQCFileSystemWatcher.IsDataInDirectories(instrument));
        }
    }
}
