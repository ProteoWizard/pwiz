/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.Database.FileSystems;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Loads a ChromatogramCache from a .skyd which is stored uncompressed inside a .sky.zip,
    /// without extracting it, and compares it against the same .skyd extracted to disk. Reports
    /// how long each takes, since reading in place should not be much slower than reading a file.
    /// </summary>
    [TestClass]
    public class ChromatogramCacheFromZipTest : AbstractUnitTest
    {
        private const string TEST_ZIP_PATH = @"Test\ChromatogramCacheFromZipTest.zip";
        private const string SKY_ZIP_NAME = "Bereman_ChromPerf.sky.zip";
        private const string SKYD_NAME = "Bereman_ChromPerf.skyd";

        [TestMethod]
        public void TestChromatogramCacheFromZip()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string skyZipPath = TestFilesDir.GetTestPath(SKY_ZIP_NAME);
            AssertEx.FileExists(skyZipPath);

            // The .skyd must be stored uncompressed, or it could not be read in place at all
            var zip = new RandomAccessZipFile(skyZipPath);
            var skydEntry = zip.FindEntryByFileName(SKYD_NAME);
            Assert.IsNotNull(skydEntry, "no " + SKYD_NAME + " in " + skyZipPath);
            Assert.IsTrue(skydEntry.IsStored, SKYD_NAME + " is not stored uncompressed");

            // The first call to SrmSettingsList.GetDefault() is slow, so call it once to avoid timing it in the test
            SrmSettingsList.GetDefault();

            // Extract the same .skyd to a file, to compare reading it in place against reading it
            string skydOnDisk = TestFilesDir.GetTestPath(SKYD_NAME);
            using (var entryStream = zip.OpenStoredEntry(skydEntry))
            using (var fileStream = File.Create(skydOnDisk))
            {
                entryStream.CopyTo(fileStream);
            }

            // Load one cache without timing it, so that neither of the timings below is really
            // measuring the one-time cost of JIT compiling everything that loading a cache touches
            LoadCacheFromDisk(skydOnDisk).Dispose();

            // Read the .skyd from inside the .zip, in place
            string skydInZip = skyZipPath + Path.DirectorySeparatorChar + SKYD_NAME;
            Assert.IsTrue(new FilePath(skydInZip).IsInZipFile);
            //PurgeFileFromDiskCache(skyZipPath);
            var inZipTime = Stopwatch.StartNew();
            var inZipCache = LoadCacheInZipFile(skydInZip);
            inZipTime.Stop();

            PurgeFileFromDiskCache(skydOnDisk);
            var onDiskTime = Stopwatch.StartNew();
            var onDiskCache = LoadCacheFromDisk(skydOnDisk);
            onDiskTime.Stop();

            try
            {
                Assert.AreEqual(onDiskCache.ChromGroupHeaderInfos.Count, inZipCache.ChromGroupHeaderInfos.Count);
                Assert.IsTrue(inZipCache.ChromGroupHeaderInfos.Count > 0);
                Assert.AreEqual(onDiskCache.CachedFilePaths.Count(), inZipCache.CachedFilePaths.Count());
                Assert.AreEqual(onDiskCache.Version, inZipCache.Version);

                // Reading every peak makes sure the whole .skyd really can be read through the .zip
                long inZipPeaks = CountPeaks(inZipCache);
                long onDiskPeaks = CountPeaks(onDiskCache);
                Assert.AreEqual(onDiskPeaks, inZipPeaks);
                Assert.IsTrue(inZipPeaks > 0);
            }
            finally
            {
                inZipCache.Dispose();
                onDiskCache.Dispose();
            }

            Console.WriteLine(@"Loaded {0} chromatogram groups: {1} ms in place from the .zip, {2} ms from the file.",
                inZipCache.ChromGroupHeaderInfos.Count, inZipTime.ElapsedMilliseconds, onDiskTime.ElapsedMilliseconds);
        }

        /// <summary>
        /// A ChromPeak with an extra field after it, so that a StructSerializer for it has to use
        /// the path which converts every item separately, the way it does whenever the size of the
        /// struct on disk is not the size of the struct in memory. That path reads the items on
        /// several threads, so this makes sure it gets the same answer as reading them on one, and
        /// says which was faster.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ChromPeakWithExtra
        {
            public ChromPeak Peak;
            public int Extra;
        }

        [TestMethod]
        public void TestReadPaddedChromPeaks()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string skydInZip = TestFilesDir.GetTestPath(SKY_ZIP_NAME) + Path.DirectorySeparatorChar + SKYD_NAME;
            var filePath = new FilePath(skydInZip);

            int chromPeakSize;
            long locationPeaks;
            int numPeaks;
            using (var stream = filePath.OpenRead())
            {
                var cacheHeader = CacheHeaderStruct.Read(stream);
                var cacheFormat = CacheFormat.FromCacheHeader(cacheHeader);
                chromPeakSize = cacheFormat.ChromPeakSize;
                locationPeaks = cacheHeader.locationPeaks;
                numPeaks = cacheHeader.numPeaks;
            }
            Assert.IsTrue(numPeaks > 0);
            Assert.AreEqual(Marshal.SizeOf<ChromPeak>(), chromPeakSize,
                "this .skyd is not in the current format, so the padded read would not line up");

            // The serializer believes the struct in memory is bigger than the one on disk, which is
            // what makes it convert each item instead of reading the array directly
            var paddedSerializer = new StructSerializer<ChromPeakWithExtra>
            {
                ItemSizeOnDisk = chromPeakSize,
                PadFromStart = false // the extra field is after the ChromPeak
            };
            Assert.AreNotEqual(paddedSerializer.ItemSizeInMemory, paddedSerializer.ItemSizeOnDisk);

            // Read each way once without timing it, so that neither timing below is really
            // measuring JIT compiling the path it is the first to use
            ReadPadded(filePath, paddedSerializer, locationPeaks, numPeaks);
            ReadPaddedSerially(filePath, paddedSerializer, locationPeaks, numPeaks, chromPeakSize);

            // There are not enough peaks in this .skyd to time a single read of them, so read them
            // enough times to be able to tell the two apart
            const int REPEAT = 20;
            ChromPeakWithExtra[] paddedPeaks = null;
            var paddedTime = Stopwatch.StartNew();
            for (int i = 0; i < REPEAT; i++)
                paddedPeaks = ReadPadded(filePath, paddedSerializer, locationPeaks, numPeaks);
            paddedTime.Stop();

            // The same items read one at a time, which is what ReadArray used to do for all of them
            ChromPeakWithExtra[] serialPeaks = null;
            var serialTime = Stopwatch.StartNew();
            for (int i = 0; i < REPEAT; i++)
                serialPeaks = ReadPaddedSerially(filePath, paddedSerializer, locationPeaks, numPeaks, chromPeakSize);
            serialTime.Stop();

            // Reading them on several threads must not change the answer
            CollectionAssert.AreEqual(serialPeaks, paddedPeaks);

            // And the padded peaks must be the real peaks, with the extra field left alone
            var realPeaks = ReadRealChromPeaks(filePath, locationPeaks, numPeaks);
            for (int i = 0; i < numPeaks; i++)
            {
                Assert.AreEqual(realPeaks[i], paddedPeaks[i].Peak, "peak {0} differs", i);
                Assert.AreEqual(0, paddedPeaks[i].Extra);
            }

            Console.WriteLine(@"Read {0} padded peaks x{1}: {2} ms with the QueueWorker, {3} ms one at a time.",
                numPeaks, REPEAT, paddedTime.ElapsedMilliseconds, serialTime.ElapsedMilliseconds);
        }

        private static ChromPeakWithExtra[] ReadPadded(FilePath filePath,
            StructSerializer<ChromPeakWithExtra> serializer, long locationPeaks, int numPeaks)
        {
            using (var stream = filePath.OpenRead())
            {
                stream.Seek(locationPeaks, SeekOrigin.Begin);
                return serializer.ReadArray(stream, numPeaks);
            }
        }

        private static ChromPeakWithExtra[] ReadPaddedSerially(FilePath filePath,
            StructSerializer<ChromPeakWithExtra> serializer, long locationPeaks, int numPeaks, int chromPeakSize)
        {
            var peaks = new ChromPeakWithExtra[numPeaks];
            using (var stream = filePath.OpenRead())
            {
                stream.Seek(locationPeaks, SeekOrigin.Begin);
                var buffer = new byte[serializer.ItemSizeInMemory];
                for (int i = 0; i < numPeaks; i++)
                {
                    Assert.AreEqual(chromPeakSize, stream.Read(buffer, 0, chromPeakSize));
                    peaks[i] = serializer.FromByteArray(buffer);
                }
            }
            return peaks;
        }

        private static ChromPeak[] ReadRealChromPeaks(FilePath filePath, long locationPeaks, int numPeaks)
        {
            using (var stream = filePath.OpenRead())
            {
                var cacheHeader = CacheHeaderStruct.Read(stream);
                var serializer = CacheFormat.FromCacheHeader(cacheHeader).ChromPeakSerializer();
                stream.Seek(locationPeaks, SeekOrigin.Begin);
                return serializer.ReadArray(stream, numPeaks);
            }
        }

        private ChromatogramCache LoadCacheInZipFile(string path)
        {
            return LoadCache(path);
        }

        private ChromatogramCache LoadCacheFromDisk(string path)
        {
            return LoadCache(path);
        }

        private ChromatogramCache LoadCache(string cachePath)
        {
            var loader = new DefaultFileLoadMonitor(new SilentProgressMonitor());
            return ChromatogramCache.Load(cachePath, new ProgressStatus(), loader, new SrmDocument(SrmSettingsList.GetDefault()));
        }
        private static long CountPeaks(ChromatogramCache cache)
        {
            long count = 0;
            foreach (var header in cache.ChromGroupHeaderInfos)
            {
                count += cache.ReadPeaks(header).Count;
            }
            return count;
        }

        private static void PurgeFileFromDiskCache(string filePath)
        {
            FileOptions NoBuffering = (FileOptions)0x20000000;
            using (var fs = new FileStream(
                       filePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite,
                       bufferSize: 4096, // Must be sector-aligned, 4096 is standard
                       options: FileOptions.WriteThrough | NoBuffering))
            {
                // We don't need to read any data. 
                // Merely acquiring and closing this unbuffered handle forces the purge.
            }
        }
    }
}
