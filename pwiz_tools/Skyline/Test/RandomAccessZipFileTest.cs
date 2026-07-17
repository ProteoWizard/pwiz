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
using System.IO;
using System.Text;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Database.FileSystems;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests <see cref="RandomAccessZipFile"/> and <see cref="SliceStream"/>: locating a
    /// stored (uncompressed) zip entry and reading its bytes in place, without extraction.
    /// Also tests <see cref="SrmDocumentSharing.CanOpenInPlace"/>, the decision about whether a
    /// .sky.zip can be read this way at all.
    /// </summary>
    [TestClass]
    public class RandomAccessZipFileTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestRandomAccessZipFile()
        {
            TestContext.EnsureTestResultsDir();
            string zipPath = TestContext.GetTestResultsPath("random_access.zip");

            // A stored entry standing in for a .skyd/.blib (random, incompressible), plus a
            // deflated entry standing in for the .sky document XML (compressible text).
            var storedBytes = MakeRandomBytes(200 * 1024, seed: 1234);
            var deflatedText = Encoding.UTF8.GetBytes(string.Concat(Enumerable_Repeat("<peptide sequence=\"ELVISLIVESK\"/>\n", 3000)));

            // Build the zip with Ionic (the same library File > Share uses), storing the
            // .skyd uncompressed the way the write side will, and deflating the .sky.
            using (var zipFile = new ZipFile(Encoding.UTF8))
            {
                var stored = zipFile.AddEntry("sample.skyd", storedBytes);
                stored.CompressionMethod = CompressionMethod.None;
                zipFile.AddEntry("doc.sky", deflatedText); // default Deflate
                zipFile.Save(zipPath);
            }

            var zip = new RandomAccessZipFile(zipPath);

            var storedEntry = zip.FindEntryByFileName("sample.skyd");
            Assert.IsNotNull(storedEntry, "stored entry not found");
            Assert.IsTrue(storedEntry.IsStored, "expected .skyd to be stored uncompressed");
            Assert.AreEqual(storedBytes.Length, storedEntry.UncompressedSize);

            var deflatedEntry = zip.FindEntryByFileName("doc.sky");
            Assert.IsNotNull(deflatedEntry, "deflated entry not found");
            Assert.IsFalse(deflatedEntry.IsStored, "expected .sky to be deflated");

            // Read the stored entry in place and confirm it is byte-identical to the original.
            using (var stream = zip.OpenStoredEntry(storedEntry))
            {
                Assert.AreEqual(storedBytes.Length, stream.Length);
                var readBack = ReadAll(stream);
                CollectionAssert.AreEqual(storedBytes, readBack, "in-place read did not match original bytes");
            }

            // Seek to an arbitrary interior range and confirm the slice matches.
            using (var stream = zip.OpenStoredEntry(storedEntry))
            {
                const int start = 100 * 1024 + 37;
                const int count = 8192;
                Assert.AreEqual(start, stream.Seek(start, SeekOrigin.Begin));
                var slice = new byte[count];
                RandomAccessZipFile.ReadExactly(stream, slice, 0, count);
                for (int i = 0; i < count; i++)
                    Assert.AreEqual(storedBytes[start + i], slice[i], "mismatch at interior offset " + i);
                Assert.AreEqual(start + count, stream.Position);

                // Reading at the very end returns 0 and does not overrun.
                Assert.AreEqual(storedBytes.Length, stream.Seek(0, SeekOrigin.End));
                Assert.AreEqual(0, stream.Read(new byte[16], 0, 16));
            }

            // A compressed entry cannot be read in place.
            AssertEx.ThrowsException<InvalidOperationException>(() => zip.OpenStoredEntry(deflatedEntry));
        }

        [TestMethod]
        public void TestSliceStreamBounds()
        {
            // SliceStream must expose exactly [offset, offset+length) of the base stream,
            // never reading outside the window even if the base stream has more data.
            var full = MakeRandomBytes(1000, seed: 99);
            const int offset = 250;
            const int length = 400;
            using (var baseStream = new MemoryStream(full))
            using (var window = new SliceStream(baseStream, offset, length, leaveOpen: true))
            {
                Assert.AreEqual(length, window.Length);

                var all = ReadAll(window);
                Assert.AreEqual(length, all.Length);
                for (int i = 0; i < length; i++)
                    Assert.AreEqual(full[offset + i], all[i]);

                // A read straddling the end is clamped to the window.
                window.Position = length - 10;
                var buf = new byte[100];
                int n = window.Read(buf, 0, 100);
                Assert.AreEqual(10, n);
                Assert.AreEqual(0, window.Read(buf, 0, 100));

                // Seek honors the same bounds as the Position setter: seeking before the start throws.
                Assert.AreEqual(length, window.Seek(0, SeekOrigin.End));
                AssertEx.ThrowsException<IOException>(() => window.Seek(-1, SeekOrigin.Begin));
                AssertEx.ThrowsException<IOException>(() => window.Seek(-length - 1, SeekOrigin.End));
            }
        }

        [TestMethod]
        public void TestPooledZipEntryStream()
        {
            TestContext.EnsureTestResultsDir();
            string zipPath = TestContext.GetTestResultsPath("pooled.zip");
            var storedBytes = MakeRandomBytes(64 * 1024, seed: 7);
            using (var zipFile = new ZipFile(Encoding.UTF8))
            {
                var stored = zipFile.AddEntry("data.skyd", storedBytes);
                stored.CompressionMethod = CompressionMethod.None;
                zipFile.Save(zipPath);
            }

            var zip = new RandomAccessZipFile(zipPath);
            var entry = zip.FindEntryByFileName("data.skyd");
            var pool = new ConnectionPool();
            var pooled = new PooledZipEntryStream(pool, zip, entry, @"C:\docs\data.skyd");

            Assert.AreEqual(@"C:\docs\data.skyd", pooled.FilePath, "logical path");
            Assert.IsFalse(pooled.IsOpen, "should not be open before use");

            // Accessing Stream connects (opens) the pooled stream; bytes must match in place.
            var readBack = ReadAll(pooled.Stream);
            Assert.IsTrue(pooled.IsOpen, "should be open after use");
            Assert.IsFalse(pooled.IsModified, "zip not modified");
            CollectionAssert.AreEqual(storedBytes, readBack, "pooled in-place read did not match");

            pooled.CloseStream();
            Assert.IsFalse(pooled.IsOpen, "should be closed after CloseStream");

            // A compressed entry cannot back a PooledZipEntryStream.
            using (var zf = new ZipFile(Encoding.UTF8))
            {
                zf.AddEntry("doc.sky", Encoding.UTF8.GetBytes(new string('x', 5000)));
                zf.Save(zipPath);
            }
            var zip2 = new RandomAccessZipFile(zipPath);
            var compressed = zip2.FindEntryByFileName("doc.sky");
            Assert.IsFalse(compressed.IsStored);
            AssertEx.ThrowsException<ArgumentException>(() =>
                new PooledZipEntryStream(pool, zip2, compressed, null));
        }

        [TestMethod]
        public void TestFilePath()
        {
            TestContext.EnsureTestResultsDir();
            string zipPath = TestContext.GetTestResultsPath("filepath.zip");
            var storedBytes = MakeRandomBytes(50000, seed: 11);
            var skyText = Encoding.UTF8.GetBytes(string.Concat(Enumerable_Repeat("<peptide/>\n", 2000)));
            using (var zf = new ZipFile(Encoding.UTF8))
            {
                var s = zf.AddEntry("data.skyd", storedBytes); s.CompressionMethod = CompressionMethod.None;
                zf.AddEntry("doc.sky", skyText); // deflated
                zf.Save(zipPath);
            }

            // The zip file itself is an ordinary path, not "in" a zip.
            Assert.IsFalse(new FilePath(zipPath).IsInZipFile);
            Assert.IsTrue(new FilePath(zipPath).Exists());

            var storedPath = new FilePath(zipPath + @"\data.skyd");
            var skyPath = new FilePath(zipPath + @"\doc.sky");
            var missingPath = new FilePath(zipPath + @"\nope.skyd");

            Assert.IsTrue(storedPath.IsInZipFile);
            Assert.IsTrue(storedPath.Exists());
            Assert.IsTrue(skyPath.Exists());
            Assert.IsFalse(missingPath.Exists());

            // The ".zip" container extension is detected case-insensitively (a file may be named .ZIP).
            var upperZipPath = zipPath.Substring(0, zipPath.Length - 4) + ".ZIP";
            Assert.IsTrue(new FilePath(upperZipPath + @"\data.skyd").IsInZipFile);

            // GetLastWriteTime returns the outermost .zip's time.
            Assert.AreEqual(File.GetLastWriteTime(zipPath), storedPath.GetLastWriteTime());

            // Stored entry: a seekable, in-place, byte-identical read.
            using (var stream = storedPath.OpenRandomAccessStream())
            {
                Assert.IsTrue(stream.CanSeek, "stored entry stream should be seekable");
                Assert.AreEqual(storedBytes.Length, stream.Length);
                CollectionAssert.AreEqual(storedBytes, ReadAll(stream));
            }
            // Compressed entry: decompressed, byte-identical, read from beginning to end.
            using (var stream = skyPath.OpenSequentialStream())
                CollectionAssert.AreEqual(skyText, ReadAll(stream));
            // A compressed entry has no contiguous byte range, so it cannot be read out of order.
            AssertEx.ThrowsException<InvalidOperationException>(() => skyPath.OpenRandomAccessStream());

            // Byte range for the SQLite VFS: stored yes (and it points at the right bytes), compressed no.
            Assert.IsTrue(storedPath.TryGetZipByteRange(out var zp, out var ofs, out var len));
            Assert.AreEqual(zipPath, zp);
            Assert.AreEqual(storedBytes.Length, len);
            Assert.IsTrue(ofs > 0);
            using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(ofs, SeekOrigin.Begin);
                var buf = new byte[len];
                RandomAccessZipFile.ReadExactly(fs, buf, 0, (int) len);
                CollectionAssert.AreEqual(storedBytes, buf, "byte range did not point at the entry data");
            }
            Assert.IsFalse(skyPath.TryGetZipByteRange(out _, out _, out _));
        }

        /// <summary>
        /// Tests the decision Skyline makes about a .sky.zip before opening it:
        /// <see cref="SrmDocumentSharing.CanOpenInPlace"/>. Files read sequentially are not checked
        /// (they are read with the same library that would extract them), so what matters is that
        /// nothing unrecognized is present and that every file needing random access really is a
        /// contiguous, unmodified byte range.
        /// </summary>
        [TestMethod]
        public void TestCanOpenInPlace()
        {
            TestContext.EnsureTestResultsDir();
            var bytes = MakeRandomBytes(20000, seed: 3);

            // What File > Share produces: the random-access files stored uncompressed, the rest
            // deflated. ".sky.view" must not be mistaken for ".sky" or for a disallowed ".view".
            Assert.IsTrue(CanOpenInPlace("openable.zip", zf =>
            {
                AddStored(zf, "doc.skyd", bytes);
                AddStored(zf, "lib.blib", bytes);
                AddStored(zf, "prot.protdb", bytes);
                zf.AddEntry("doc.sky", Encoding.UTF8.GetBytes(new string('x', 4000)));
                zf.AddEntry("doc.sky.view", Encoding.UTF8.GetBytes(new string('y', 500)));
                zf.AddEntry("doc.skyl", Encoding.UTF8.GetBytes(new string('z', 500)));
            }), "a shared document with its random-access files stored should open in place");

            // A deflated .skyd is not a contiguous range of the bytes we want, so it must extract.
            Assert.IsFalse(CanOpenInPlace("deflated_skyd.zip", zf =>
            {
                zf.AddEntry("doc.skyd", Encoding.UTF8.GetBytes(new string('y', 40000)));
                AddStored(zf, "lib.blib", bytes);
            }), "a deflated .skyd must not open in place");

            // A file that is not part of a document (e.g. raw data) means extracting everything.
            Assert.IsFalse(CanOpenInPlace("extra_file.zip", zf =>
            {
                zf.AddEntry("doc.sky", Encoding.UTF8.GetBytes(new string('x', 2000)));
                zf.AddEntry("extra.raw", bytes);
            }), "an unrecognized file must force extraction");

            // Encryption is independent of the compression method, so an entry can be both stored
            // and encrypted. Its bytes are ciphertext preceded by a header, so reading it in place
            // would silently produce garbage rather than fail.
            Assert.IsFalse(CanOpenInPlace("encrypted_skyd.zip", zf =>
            {
                zf.Password = "secret";
                zf.Encryption = EncryptionAlgorithm.WinZipAes256;
                AddStored(zf, "doc.skyd", bytes);
            }), "an encrypted .skyd must not open in place");

            // Stored and deflated are the only methods a document opened in place can read, but
            // Ionic can write others, and extracting the .zip would go through Ionic. So a .sky
            // compressed some other way has to extract.
            Assert.IsFalse(CanOpenInPlace("bzip2_sky.zip", zf =>
            {
                AddStored(zf, "doc.skyd", bytes);
                zf.AddEntry("doc.sky", Encoding.UTF8.GetBytes(new string('x', 4000)))
                    .CompressionMethod = CompressionMethod.BZip2;
            }), "a bzip2 .sky must not open in place");

            // An encrypted .sky is still deflated, but our inflater would be handed ciphertext.
            // Only the .sky is encrypted here, so that the .skyd cannot be what fails the check.
            Assert.IsFalse(CanOpenInPlace("encrypted_sky.zip", zf =>
            {
                AddStored(zf, "doc.skyd", bytes);
                var sky = zf.AddEntry("doc.sky", Encoding.UTF8.GetBytes(new string('x', 4000)));
                sky.Password = "secret";
                sky.Encryption = EncryptionAlgorithm.WinZipAes256;
            }), "an encrypted .sky must not open in place");
        }

        private bool CanOpenInPlace(string zipName, Action<ZipFile> addEntries)
        {
            string zipPath = TestContext.GetTestResultsPath(zipName);
            using (var zipFile = new ZipFile(Encoding.UTF8))
            {
                addEntries(zipFile);
                zipFile.Save(zipPath);
            }
            return new SrmDocumentSharing(zipPath).CanOpenInPlace();
        }

        private static void AddStored(ZipFile zipFile, string name, byte[] bytes)
        {
            zipFile.AddEntry(name, bytes).CompressionMethod = CompressionMethod.None;
        }

        private static byte[] MakeRandomBytes(int count, int seed)
        {
            var rnd = new Random(seed);
            var bytes = new byte[count];
            rnd.NextBytes(bytes);
            return bytes;
        }

        private static System.Collections.Generic.IEnumerable<string> Enumerable_Repeat(string value, int count)
        {
            for (int i = 0; i < count; i++)
                yield return value;
        }

        private static byte[] ReadAll(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
