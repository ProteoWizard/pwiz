using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
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
            TestGetExistingFilesForInstrument(testDir, MainSettings.BRUKER);
            TestGetExistingFilesForInstrument(testDir, MainSettings.SHIMADZU);

        }

        private void TestGetExistingFilesForInstrument(string testDir, string instrument)
        {
            var folderToWatch = CreateDirectory(testDir, instrument);

            // Create a .sky files
            var skyFile = CreateFile(folderToWatch, "test.sky");

            List<string> dataFiles;
            SetupTestFolder(folderToWatch, instrument, out dataFiles);

            var watcher = new AutoQCFileSystemWatcher(new TestLogger(), new TestConfigRunner());
            AutoQcConfig config = new AutoQcConfig();
            
            var mainSettings = MainSettings.GetDefault();
            config.MainSettings = mainSettings;

            Assert.AreEqual(mainSettings.QcFileFilter, FileFilter.GetFileFilter(AllFileFilter.NAME, string.Empty));
            mainSettings.SkylineFilePath = skyFile;
            mainSettings.IncludeSubfolders = false;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.ValidateSettings();

            watcher.Init(config);
            var files = watcher.GetExistingFiles();
            Assert.AreEqual(1, files.Count);
            Assert.AreEqual(dataFiles[0], files[0]);

            mainSettings.IncludeSubfolders = true;
            mainSettings.ValidateSettings();

            watcher.Init(config);
            files = watcher.GetExistingFiles();
            Assert.AreEqual(5, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));
            Assert.IsTrue(files.Contains(dataFiles[1]));
            Assert.IsTrue(files.Contains(dataFiles[2]));
            Assert.IsTrue(files.Contains(dataFiles[3]));
            Assert.IsTrue(files.Contains(dataFiles[4]));

            /* Files:
              "root_QC_"
              "QC_one"
              "one_1_a_QC_"
              "one_1_b_QC"
              "two_qc_"
             */
            mainSettings.QcFileFilter = FileFilter.GetFileFilter(ContainsFilter.NAME, "QC");
            watcher.Init(config);
            files = watcher.GetExistingFiles();
            Assert.AreEqual(4, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));
            Assert.IsTrue(files.Contains(dataFiles[1]));
            Assert.IsTrue(files.Contains(dataFiles[2]));
            Assert.IsTrue(files.Contains(dataFiles[3]));

            mainSettings.QcFileFilter = FileFilter.GetFileFilter(StartsWithFilter.NAME, "QC_");
            watcher.Init(config);
            files = watcher.GetExistingFiles();
            Assert.AreEqual(1, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[1]));

            mainSettings.QcFileFilter = FileFilter.GetFileFilter(EndsWithFilter.NAME, "_QC_");
            watcher.Init(config);
            files = watcher.GetExistingFiles();
            Assert.AreEqual(2, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));
            Assert.IsTrue(files.Contains(dataFiles[2]));

            mainSettings.QcFileFilter = FileFilter.GetFileFilter(RegexFilter.NAME, "[ab]_QC");
            watcher.Init(config);
            files = watcher.GetExistingFiles();
            Assert.AreEqual(2, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[2]));
            Assert.IsTrue(files.Contains(dataFiles[3]));

        }

        private static void SetupTestFolder(string folderToWatch, string instrument, out List<string> dataFiles, bool createRenamedData = false)
        {
            // Add subfolders
            var sf1 = CreateDirectory(folderToWatch, "One_QC");
            var sf1_1 = CreateDirectory(sf1, "QC_a");
            var sf2 = CreateDirectory(folderToWatch, "Two_QC_");

            var ext = AutoQCFileSystemWatcher.GetDataFileExt(instrument);

            var dataInDirs = AutoQCFileSystemWatcher.IsDataInDirectories(instrument);

            // Add files in the folders
            dataFiles = new List<string>
            {
                CreateDataFile(folderToWatch, "root_QC_" + ext, dataInDirs, createRenamedData),
                CreateDataFile(sf1, "QC_one" + ext, dataInDirs, createRenamedData),
                CreateDataFile(sf1_1, "one_1_a_QC_" + ext, dataInDirs, createRenamedData),
                CreateDataFile(sf1_1, "one_1_b_QC" + ext, dataInDirs, createRenamedData),
                CreateDataFile(sf2, "two_qc_" + ext, dataInDirs, createRenamedData)
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
            if (!instrument.Equals(MainSettings.AGILENT) && !instrument.Equals(MainSettings.BRUKER))
            {
                CreateInstrumentFile(sf1, MainSettings.AGILENT);
            }
            if (!instrument.Equals(MainSettings.WATERS))
            {
                CreateInstrumentFile(sf1, MainSettings.WATERS);
            }
            if (!instrument.Equals(MainSettings.BRUKER) && !instrument.Equals(MainSettings.AGILENT))
            {
                CreateInstrumentFile(sf1, MainSettings.BRUKER);
            }
            if (!instrument.Equals(MainSettings.SHIMADZU))
            {
                CreateInstrumentFile(sf1, MainSettings.SHIMADZU);
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
            TestGetNewFilesForInstrument(testDir, MainSettings.BRUKER);
            TestGetNewFilesForInstrument(testDir, MainSettings.SHIMADZU);
        }

        private static void TestGetNewFilesForInstrument(string testDir, string instrument)
        {
            var logger = new TestLogger();
            IConfigRunner configRunner = new TestConfigRunner();
            configRunner.ChangeStatus(ConfigRunner.RunnerStatus.Running);
            

            // folder to watch
            var folderToWatch = CreateDirectory(testDir, instrument);
            // Create a .sky files
            var skyFile = CreateFile(folderToWatch, "test2_a.sky");

            var config = new AutoQcConfig();

            // 1. Look for files in folderToWatchOnly
            var watcher = new AutoQCFileSystemWatcher(logger, configRunner);
            var mainSettings = MainSettings.GetDefault();
            config.MainSettings = mainSettings;

            mainSettings.SkylineFilePath = skyFile;
            mainSettings.IncludeSubfolders = false;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.ValidateSettings();

            watcher.Init(config);
            watcher.StartWatching(); // Start watching
            Assert.AreEqual(0, watcher.GetExistingFiles().Count); // No existing files

            // Create new files in the folder
            List<string> dataFiles;
            SetupTestFolder(folderToWatch, instrument, out dataFiles);

            // Only one file should have been added to the queue since we are not monitoring sub-folders
            Assert.AreEqual(1, watcher.GetExistingFiles().Count);
            var files = new List<string>();
            string f;
            while ((f = watcher.GetFile()) != null)
            {
                files.Add(f);
            }
            Assert.AreEqual(1, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));  
            Assert.IsNull(watcher.GetFile()); // Nothing in the queue

            watcher.Stop();

            // 2. Look for files in subfolders
            watcher = new AutoQCFileSystemWatcher(logger, configRunner);
            // folder to watch
            folderToWatch = CreateDirectory(testDir, instrument + "_2");
            // Create a .sky files
            skyFile = CreateFile(folderToWatch, "test2_b.sky");
            mainSettings.SkylineFilePath = skyFile;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.IncludeSubfolders = true; // watch sub-folders
            mainSettings.ValidateSettings();
            watcher.Init(config);

            watcher.StartWatching(); // Start watching
            Assert.AreEqual(0, watcher.GetExistingFiles().Count); // No existing files

            dataFiles.Clear();
            SetupTestFolder(folderToWatch, instrument, out dataFiles); // Create new files in the folder

            files = new List<string>();
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

            watcher.Stop();

            //  3. Look for files in subfolders matching a pattern
            watcher = new AutoQCFileSystemWatcher(logger, configRunner);
            // folder to watch
            folderToWatch = CreateDirectory(testDir, instrument + "_3");
            // Create a .sky files
            skyFile = CreateFile(folderToWatch, "test2_c.sky");
            mainSettings.SkylineFilePath = skyFile;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.IncludeSubfolders = true; // watch sub-folders
            mainSettings.QcFileFilter = FileFilter.GetFileFilter(ContainsFilter.NAME, "_QC_"); // file name pattern
            mainSettings.ValidateSettings();
            watcher.Init(config);

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

            watcher.Stop();

            // 4. Add new files in directory by first creating a temp file/directory and renaming it.
            // This should trigger a "Renamed" event for the FileSystemWatcher
            watcher = new AutoQCFileSystemWatcher(logger, configRunner);
            // folder to watch
            folderToWatch = CreateDirectory(testDir, instrument + "_4");
            // Create a .sky files
            skyFile = CreateFile(folderToWatch, "test2_d.sky");
            mainSettings.SkylineFilePath = skyFile;
            mainSettings.InstrumentType = instrument;
            mainSettings.FolderToWatch = folderToWatch;
            mainSettings.IncludeSubfolders = false;
            mainSettings.QcFileFilter = FileFilter.GetFileFilter(AllFileFilter.NAME, string.Empty);
            mainSettings.ValidateSettings();
            watcher.Init(config);

            watcher.StartWatching(); // Start watching
            Assert.AreEqual(0, watcher.GetExistingFiles().Count); // No existing files

            dataFiles.Clear();
            SetupTestFolder(folderToWatch, instrument, out dataFiles, 
                true); // Create temp file first and rename it

            watcher.CheckDrive(); // Otherwise UNC tests might fail on //net-lab3

            files = new List<string>();
            while ((f = watcher.GetFile()) != null)
            {
                files.Add(f);
            }
            Assert.AreEqual(1, files.Count);
            Assert.IsTrue(files.Contains(dataFiles[0]));

            watcher.Stop();
        }

        [TestMethod]
        public void TestMappedDrive()
        {
            var dir = @"Y:\vsharma\AutoQCTests"; // \\skyline\data\tmp\vsharma
            var testDir = CreateDirectory(dir, "TestAutoQcFileSystemWatcher_MappedDrive");

            TestGetNewFilesForInstrument(testDir, MainSettings.THERMO);
            TestGetNewFilesForInstrument(testDir, MainSettings.SCIEX);
            TestGetNewFilesForInstrument(testDir, MainSettings.AGILENT);
            TestGetNewFilesForInstrument(testDir, MainSettings.WATERS);
            TestGetNewFilesForInstrument(testDir, MainSettings.BRUKER);
            TestGetNewFilesForInstrument(testDir, MainSettings.SHIMADZU);
        }

        [TestMethod]
        public void TestUncPaths()
        {
            var dir = @"\\net-lab3\maccoss_shared\vsharma\AutoQCTests";
            var testDir = CreateDirectory(dir, "TestAutoQcFileSystemWatcher_UNC");

            TestGetNewFilesForInstrument(testDir, MainSettings.THERMO);
            TestGetNewFilesForInstrument(testDir, MainSettings.SCIEX);
            TestGetNewFilesForInstrument(testDir, MainSettings.AGILENT);
            TestGetNewFilesForInstrument(testDir, MainSettings.WATERS);
            TestGetNewFilesForInstrument(testDir, MainSettings.BRUKER);
            TestGetNewFilesForInstrument(testDir, MainSettings.SHIMADZU);
        }

        private static string CreateDirectory(string parent, string dirName, bool rename = false)
        {
            if (!rename)
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
            else
            {
                var tempDir = CreateDirectory(parent, dirName + ".TMP");
                var dir = Path.Combine(parent, dirName);
                Assert.IsFalse(Directory.Exists(dir));
                Thread.Sleep(500);
                Directory.Move(tempDir, dir);
                Assert.IsTrue(Directory.Exists(dir));
                return dir;
            }
        }

        private static string CreateFile(string parentDir, string fileName, bool rename = false)
        {
            if (!rename)
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

            var tempFile = CreateFile(parentDir, fileName + ".TMP");
            var file = Path.Combine(parentDir, fileName);
            Assert.IsFalse(File.Exists(file));
            Thread.Sleep(500);
            File.Move(tempFile, file);
            Assert.IsTrue(File.Exists(file));
            return file;
        }

        private static string CreateDataFile(string parentDir, string fileName, bool isDir, bool renamed = false)
        {
            return isDir ?  CreateDirectory(parentDir, fileName, renamed) : CreateFile(parentDir, fileName, renamed);
        }

        private static void CreateInstrumentFile(string parentDir, string instrument)
        {
            CreateDataFile(parentDir, instrument + "_QC_" + AutoQCFileSystemWatcher.GetDataFileExt(instrument),
                                            AutoQCFileSystemWatcher.IsDataInDirectories(instrument));
        }
    }
}
