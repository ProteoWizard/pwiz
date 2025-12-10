using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SharedBatchTest;

namespace AutoQCTest
{
    [TestClass]
    public class AutoQcFileSystemWatcherTest : AbstractUnitTest
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

            var defaultFileFilter = FileFilter.GetFileFilter(AllFileFilter.FilterName, string.Empty);
            var mainSettings = new MainSettings(skyFile, folderToWatch, false, defaultFileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());
            FilesFromWatcherEquals(mainSettings, new[] {dataFiles[0]});
            
            // include subfolders
           mainSettings = new MainSettings(skyFile, folderToWatch, true, defaultFileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());
           FilesFromWatcherEquals(mainSettings, new[] { dataFiles[0], dataFiles[1], dataFiles[2], dataFiles[3], dataFiles[4] });
            
            var fileFilter = FileFilter.GetFileFilter(ContainsFilter.FilterName, "QC");
            mainSettings = new MainSettings(skyFile, folderToWatch, true, fileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());
            FilesFromWatcherEquals(mainSettings, new[] { dataFiles[0], dataFiles[1], dataFiles[2], dataFiles[3]});

            fileFilter = FileFilter.GetFileFilter(StartsWithFilter.FilterName, "QC_");
            mainSettings = new MainSettings(skyFile, folderToWatch, true, fileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());
            FilesFromWatcherEquals(mainSettings, new[] { dataFiles[1] });

            fileFilter = FileFilter.GetFileFilter(EndsWithFilter.FilterName, "_QC_");
            mainSettings = new MainSettings(skyFile, folderToWatch, true, fileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());
            FilesFromWatcherEquals(mainSettings, new[] { dataFiles[0], dataFiles[2] });

            fileFilter = FileFilter.GetFileFilter(RegexFilter.FilterName, "[ab]_QC");
            mainSettings = new MainSettings(skyFile, folderToWatch, true, fileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());
            FilesFromWatcherEquals(mainSettings, new[] { dataFiles[2], dataFiles[3] });
        }

        private static void FilesFromWatcherEquals(MainSettings mainSettings, string[] expectedFiles)
        {
            var config = new AutoQcConfig("test", false, DateTime.MinValue, DateTime.MinValue, mainSettings,
                TestUtils.GetNoPublishPanoramaSettings(), TestUtils.GetTestSkylineSettings());
            var logger = TestUtils.GetTestLogger(config);

            using var configRunner = new ConfigRunner(config, logger);
            using var watcher = new AutoQCFileSystemWatcher(logger, configRunner);
            watcher.Init(config);
            var files = watcher.GetExistingFiles();
            Assert.AreEqual(expectedFiles.Length, files.Count);
            if (expectedFiles.Length == 1)
                Assert.AreEqual(expectedFiles[0], files[0]);
            foreach (var dataFile in expectedFiles)
                Assert.IsTrue(files.Contains(dataFile));
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
            // folder to watch
            var folderToWatch = CreateDirectory(testDir, instrument);
            // Create a .sky files
            var skyFile = CreateFile(folderToWatch, "test2_a.sky");

            var defaultFileFilter = FileFilter.GetFileFilter(AllFileFilter.FilterName, string.Empty);
            var mainSettings = new MainSettings(skyFile, folderToWatch, false, defaultFileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());

            var config = new AutoQcConfig("test", false, DateTime.MinValue, DateTime.MinValue, mainSettings,
                TestUtils.GetNoPublishPanoramaSettings(), TestUtils.GetTestSkylineSettings());
            var logger = TestUtils.GetTestLogger(config);

            using (var configRunner = new ConfigRunner(config, logger))
            {
                configRunner.ChangeStatus(RunnerStatus.Running);

                // 1. Look for files in folderToWatchOnly
                using var watcher = new AutoQCFileSystemWatcher(logger, configRunner);
                mainSettings.ValidateSettings();
                watcher.Init(config);
                StartWatching(watcher);

                // Create new files in the folder
                SetupTestFolder(folderToWatch, instrument, out var dataFiles);

                // Only one file should have been added to the queue since we are not monitoring sub-folders
                ValidateWatcherFiles(watcher, 1, dataFiles);
            }

            // 2. Look for files in subfolders
            // folder to watch
            folderToWatch = CreateDirectory(testDir, instrument + "_2");
            // Create a .sky files
            skyFile = CreateFile(folderToWatch, "test2_b.sky");

            mainSettings = new MainSettings(skyFile, folderToWatch, true, defaultFileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());

            config = new AutoQcConfig("test", false, DateTime.MinValue, DateTime.MinValue, mainSettings,
                TestUtils.GetNoPublishPanoramaSettings(), TestUtils.GetTestSkylineSettings());

            logger = TestUtils.GetTestLogger(config);
            using (var configRunner = new ConfigRunner(config, logger))
            {
                configRunner.ChangeStatus(RunnerStatus.Running);
                using var watcher = new AutoQCFileSystemWatcher(logger, configRunner);

                mainSettings.ValidateSettings();
                watcher.Init(config);

                StartWatching(watcher);

                SetupTestFolder(folderToWatch, instrument, out var dataFiles); // Create new files in the folder

                ValidateWatcherFiles(watcher, 5, dataFiles);
            }

            //  3. Look for files in subfolders matching a pattern
            // folder to watch
            folderToWatch = CreateDirectory(testDir, instrument + "_3");
            // Create a .sky files
            skyFile = CreateFile(folderToWatch, "test2_c.sky");
            var fileFilter = FileFilter.GetFileFilter(ContainsFilter.FilterName, "_QC_"); // file name pattern
            // watch sub folders
            mainSettings = new MainSettings(skyFile, folderToWatch, true, fileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());

            config = new AutoQcConfig("test", false, DateTime.MinValue, DateTime.MinValue, mainSettings,
                TestUtils.GetNoPublishPanoramaSettings(), TestUtils.GetTestSkylineSettings());

            logger = TestUtils.GetTestLogger(config);
            using (var configRunner = new ConfigRunner(config, logger))
            {
                configRunner.ChangeStatus(RunnerStatus.Running);
                using var watcher = new AutoQCFileSystemWatcher(logger, configRunner);


                mainSettings.ValidateSettings();
                watcher.Init(config);

                StartWatching(watcher);

                SetupTestFolder(folderToWatch, instrument, out var dataFiles); // Create new files in the folder

                var files = ValidateWatcherFiles(watcher, 2, Array.Empty<string>());
                Assert.IsTrue(files.Contains(dataFiles[0]));
                Assert.IsTrue(files.Contains(dataFiles[2]));
            }

            // 4. Add new files in directory by first creating a temp file/directory and renaming it.
            // This should trigger a "Renamed" event for the FileSystemWatcher
            // folder to watch
            folderToWatch = CreateDirectory(testDir, instrument + "_4");
            // Create a .sky files
            skyFile = CreateFile(folderToWatch, "test2_d.sky");

            mainSettings = new MainSettings(skyFile, folderToWatch, false, defaultFileFilter, true,
                MainSettings.ACCUM_TIME_WINDOW.ToString(), instrument, MainSettings.ACQUISITION_TIME.ToString());

            config = new AutoQcConfig("test", false, DateTime.MinValue, DateTime.MinValue, mainSettings,
                TestUtils.GetNoPublishPanoramaSettings(), TestUtils.GetTestSkylineSettings());

            logger = TestUtils.GetTestLogger(config);
            using (var configRunner = new ConfigRunner(config, logger))
            {
                configRunner.ChangeStatus(RunnerStatus.Running);
                using var watcher = new AutoQCFileSystemWatcher(logger, configRunner);

                //config.MainSettings = mainSettings;
                mainSettings.ValidateSettings();
                watcher.Init(config);

                watcher.StartWatching(); // Start watching
                Assert.AreEqual(0, watcher.GetExistingFiles().Count); // No existing files

                SetupTestFolder(folderToWatch, instrument, out var dataFiles,
                    true); // Create temp file first and rename it

                watcher.CheckDrive(); // Otherwise UNC tests might fail on //net-lab3

                var files = GetWatcherFiles(watcher).ToList();
                Assert.AreEqual(1, files.Count);
                Assert.IsTrue(files.Contains(dataFiles[0]));
            }
        }

        private static IList<string> ValidateWatcherFiles(AutoQCFileSystemWatcher watcher, int expectedFileCount, IList<string> dataFiles)
        {
            WaitForCondition(() => watcher.GetQueueCount() == expectedFileCount);
            var files = GetWatcherFiles(watcher).ToList();
            Assert.AreEqual(expectedFileCount, files.Count);
            for (int i = 0; i < Math.Min(expectedFileCount, dataFiles.Count); i++)
            {
                Assert.IsTrue(files.Contains(dataFiles[i]));
            }
            Assert.IsNull(watcher.GetFile()); // Nothing left in the queue
            return files;
        }

        private static IEnumerable<string> GetWatcherFiles(AutoQCFileSystemWatcher watcher)
        {
            while (watcher.GetFile() is { } f)
                yield return f;
        }

        private static void StartWatching(AutoQCFileSystemWatcher watcher)
        {
            watcher.StartWatching();
            WaitForCondition(() => watcher.GetExistingFiles().Count == 0,
                errorMessage: "Failed waiting for <0> files");
        }

        /*
        [TestMethod]
        public void TestMappedDrive()
        {
            // TODO: Only works on Vagisha Sharma's computer!
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
            // TODO: Only works on Vagisha Sharma's computer!
            var dir = @"\\net-lab3\maccoss_shared\vsharma\AutoQCTests";
            var testDir = CreateDirectory(dir, "TestAutoQcFileSystemWatcher_UNC");

            TestGetNewFilesForInstrument(testDir, MainSettings.THERMO);
            TestGetNewFilesForInstrument(testDir, MainSettings.SCIEX);
            TestGetNewFilesForInstrument(testDir, MainSettings.AGILENT);
            TestGetNewFilesForInstrument(testDir, MainSettings.WATERS);
            TestGetNewFilesForInstrument(testDir, MainSettings.BRUKER);
            TestGetNewFilesForInstrument(testDir, MainSettings.SHIMADZU);
        }
        */
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
