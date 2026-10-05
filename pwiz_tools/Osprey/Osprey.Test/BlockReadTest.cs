/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using Parquet.Schema;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The block-read stream and the library identity lookup behind the first-pass FDR read path.
    /// Parquet reads through <see cref="BlockReadStream"/> in its planned mode are covered by every
    /// parquet round trip in the suite, since all score-parquet reads open through it.
    /// </summary>
    [TestClass]
    public class BlockReadTest
    {
        private const int COLUMNS = 4;
        private const int ROW_GROUPS = 3;
        private const int ROWS_PER_GROUP = 40000;

        [TestMethod]
        public void TestBlockReadStreamAndLibraryIdentity()
        {
            VerifyLibraryIdentity();
            VerifyBlockBytesClamp();
            string path = Path.Combine(Path.GetTempPath(), @"osprey_blockread_" + Path.GetRandomFileName());
            string parquetPath = path + @".parquet";
            try
            {
                var bytes = new byte[3 * 1024 * 1024 + 12345];
                new Random(4765).NextBytes(bytes);
                File.WriteAllBytes(path, bytes);
                foreach (bool gate in new[] { false, true })
                {
                    VerifyReadsMatch(path, bytes, 64 * 1024, gate);
                    VerifyReadsMatch(path, bytes, 1, gate);
                    VerifyReadsMatch(path, bytes, bytes.Length * 2, gate);
                    VerifySequentialReads(path, bytes, 64 * 1024, gate);
                    VerifySequentialReads(path, bytes, bytes.Length * 2, gate);
                }
                VerifyBadBlockSizeHoldsNoFile(path);

                WriteParquet(parquetPath);
                VerifyParquetProbesReadExactly(parquetPath);
                VerifyParquetRowGroupReads(parquetPath);
            }
            finally
            {
                File.Delete(path);
                File.Delete(parquetPath);
            }
        }

        // OSPREY_BLOCK_READ_MB values that overflowed int arithmetic (2048 gave a negative block,
        // multiples of 4096 a zero one) clamp to the largest block; zero and below are off.
        private static void VerifyBlockBytesClamp()
        {
            const int maxBytes = OspreyEnvironment.MAX_BLOCK_READ_MB << 20;
            Assert.AreEqual(4 << 20, BlockReadStream.BlockBytes(4));
            Assert.AreEqual(1 << 20, BlockReadStream.BlockBytes(1));
            Assert.AreEqual(0, BlockReadStream.BlockBytes(0));
            Assert.AreEqual(0, BlockReadStream.BlockBytes(-5));
            Assert.AreEqual(maxBytes, BlockReadStream.BlockBytes(OspreyEnvironment.MAX_BLOCK_READ_MB));
            Assert.AreEqual(maxBytes, BlockReadStream.BlockBytes(2048));
            Assert.AreEqual(maxBytes, BlockReadStream.BlockBytes(4096));
            Assert.AreEqual(maxBytes, BlockReadStream.BlockBytes(int.MaxValue));
            Assert.AreEqual(0, OspreyEnvironment.ClampBlockReadMb(int.MinValue));
        }

        // A rejected block size must not leave the file open behind the exception.
        private static void VerifyBadBlockSizeHoldsNoFile(string path)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => BlockReadStream.Open(path, 0, false));
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Opening exclusively proves no handle survived the failed open.
            }
        }

        // A sidecar-style walk - a header, then many small record reads in order - reads the header
        // exactly and then reads ahead in whole blocks, delivering exactly the file's bytes.
        private static void VerifySequentialReads(string path, byte[] expected, int blockBytes, bool gate)
        {
            const int headerLength = 32;
            const int recordLength = 36;
            using (var stream = BlockReadStream.Open(path, blockBytes, gate))
            {
                var header = new byte[headerLength];
                Assert.AreEqual(headerLength, stream.Read(header, 0, headerLength));
                Assert.AreEqual(1, stream.DiskReadCount);
                Assert.AreEqual(headerLength, stream.DiskReadBytes);
                AssertBytesMatch(expected, 0, header, headerLength);

                var record = new byte[recordLength];
                long position = headerLength;
                while (position < expected.Length)
                {
                    int read = stream.Read(record, 0, recordLength);
                    Assert.AreEqual((int)Math.Min(recordLength, expected.Length - position), read);
                    AssertBytesMatch(expected, position, record, read);
                    position += read;
                }
                long payload = expected.Length - headerLength;
                Assert.AreEqual(1 + (payload + blockBytes - 1) / blockBytes, stream.DiskReadCount);
                Assert.AreEqual(expected.Length, stream.DiskReadBytes);
            }
        }

        // A deterministic mix of forward, backward and block-spanning reads, small and larger than
        // the block, through all three read entry points, must return exactly the file's bytes.
        private static void VerifyReadsMatch(string path, byte[] expected, int blockBytes, bool gate)
        {
            var random = new Random(blockBytes);
            using (var stream = BlockReadStream.Open(path, blockBytes, gate))
            {
                Assert.AreEqual(expected.Length, stream.Length);
                for (int i = 0; i < 200; i++)
                {
                    long position = random.Next(expected.Length);
                    int count = random.Next(4) == 0 ? random.Next(256 * 1024) : random.Next(4096);
                    stream.Seek(position, SeekOrigin.Begin);
                    var actual = new byte[count];
                    int read;
                    switch (i % 3)
                    {
                        case 0:
                            read = stream.Read(actual, 0, count);
                            break;
                        case 1:
                            read = stream.Read(actual.AsSpan());
                            break;
                        default:
                            read = stream.ReadAsync(actual.AsMemory()).AsTask().Result;
                            break;
                    }
                    int available = (int)Math.Min(count, expected.Length - position);
                    Assert.AreEqual(available, read);
                    AssertBytesMatch(expected, position, actual, read);
                    Assert.AreEqual(position + read, stream.Position);
                }
            }
        }

        // Before its first row group, opening a parquet - Parquet.Net's head magic, tail magic and
        // footer - reads what it asks, not a block from the start of the file; so do explicit
        // small reads at the head and the tail.
        private static void VerifyParquetProbesReadExactly(string path)
        {
            long length = new FileInfo(path).Length;
            const int blockBytes = 4 << 20;
            Assert.IsTrue(length > blockBytes / 2, @"the parquet must dwarf its footer");
            using (var stream = BlockReadStream.Open(path, blockBytes, false))
            {
                var reader = ParquetReader.CreateAsync(stream).GetAwaiter().GetResult();
                Assert.AreEqual(ROW_GROUPS, reader.RowGroupCount);
                Assert.IsTrue(stream.DiskReadBytes < 64 * 1024, @"opened with {0} bytes", stream.DiskReadBytes);
                reader.DisposeAsync().GetAwaiter().GetResult();
            }
            var expected = File.ReadAllBytes(path);
            using (var stream = BlockReadStream.Open(path, blockBytes, false))
            {
                var head = new byte[4];
                Assert.AreEqual(4, stream.Read(head, 0, 4));
                AssertBytesMatch(expected, 0, head, 4);
                var tail = new byte[8];
                stream.Seek(-8, SeekOrigin.End);
                Assert.AreEqual(8, stream.Read(tail, 0, 8));
                AssertBytesMatch(expected, length - 8, tail, 8);
                Assert.AreEqual(2, stream.DiskReadCount);
                Assert.AreEqual(12, stream.DiskReadBytes);
            }
        }

        // A reader decoding every column of each row group, in file order. In the first row
        // group each chunk is read once - not again with each later neighbor - and from the
        // second on the stream knows the touched columns and reads the row group in one read.
        // The bytes delivered are the file's bytes throughout.
        private static void VerifyParquetRowGroupReads(string path)
        {
            var expected = File.ReadAllBytes(path);
            using (var stream = BlockReadStream.Open(path, 4 << 20, false))
            {
                var reader = ParquetReader.CreateAsync(stream).GetAwaiter().GetResult();
                Assert.IsNotNull(reader.Metadata);
                for (int rowGroup = 0; rowGroup < ROW_GROUPS; rowGroup++)
                {
                    var columns = reader.Metadata.RowGroups[rowGroup].Columns;
                    stream.BeginRowGroup(columns);
                    var extents = new List<Tuple<long, long>>();
                    foreach (var column in columns)
                    {
                        var meta = column.MetaData;
                        Assert.IsNotNull(meta);
                        long start = meta.DataPageOffset;
                        if (meta.DictionaryPageOffset.HasValue && meta.DictionaryPageOffset.Value > 0)
                            start = Math.Min(start, meta.DictionaryPageOffset.Value);
                        extents.Add(Tuple.Create(start, start + meta.TotalCompressedSize));
                    }
                    extents.Sort();
                    int readsBefore = stream.DiskReadCount;
                    long bytesBefore = stream.DiskReadBytes;
                    var piece = new byte[8 * 1024];
                    foreach (var extent in extents)
                    {
                        stream.Seek(extent.Item1, SeekOrigin.Begin);
                        for (long position = extent.Item1; position < extent.Item2;)
                        {
                            int count = (int)Math.Min(piece.Length, extent.Item2 - position);
                            Assert.AreEqual(count, stream.Read(piece, 0, count));
                            AssertBytesMatch(expected, position, piece, count);
                            position += count;
                        }
                    }
                    long span = extents[extents.Count - 1].Item2 - extents[0].Item1;
                    if (rowGroup == 0)
                    {
                        long chunkBytes = 0;
                        foreach (var extent in extents)
                            chunkBytes += extent.Item2 - extent.Item1;
                        Assert.AreEqual(COLUMNS, stream.DiskReadCount - readsBefore);
                        Assert.AreEqual(chunkBytes, stream.DiskReadBytes - bytesBefore);
                    }
                    else
                    {
                        Assert.AreEqual(1, stream.DiskReadCount - readsBefore);
                        Assert.AreEqual(span, stream.DiskReadBytes - bytesBefore);
                    }
                }
                reader.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        // COLUMNS columns of random doubles, which do not compress, in ROW_GROUPS row groups: a
        // parquet of about 4 MB whose footer is a few KB.
        private static void WriteParquet(string path)
        {
            var fields = new DataField<double>[COLUMNS];
            for (int c = 0; c < COLUMNS; c++)
                fields[c] = new DataField<double>(@"c" + c);
            var schema = new ParquetSchema(new List<Field>(fields));
            var random = new Random(4765);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var writer = ParquetWriter.CreateAsync(schema, stream).GetAwaiter().GetResult();
                for (int rowGroup = 0; rowGroup < ROW_GROUPS; rowGroup++)
                {
                    using (var group = writer.CreateRowGroup())
                    {
                        foreach (var field in fields)
                        {
                            var values = new double[ROWS_PER_GROUP];
                            for (int i = 0; i < values.Length; i++)
                                values[i] = random.NextDouble();
                            group.WriteAsync(field, new ReadOnlyMemory<double>(values)).GetAwaiter().GetResult();
                        }
                    }
                }
                // Writes the footer
                writer.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        private static void AssertBytesMatch(byte[] expected, long position, byte[] actual, int count)
        {
            for (int b = 0; b < count; b++)
            {
                if (actual[b] != expected[position + b])
                    Assert.Fail(@"byte {0} differs", position + b);
            }
        }

        private static void VerifyLibraryIdentity()
        {
            var library = new List<LibraryEntry>
            {
                new LibraryEntry(0, @"PEPTIDEK", @"PEPTIDEK", 2, 450.0, 10.0),
                new LibraryEntry(1, @"PEPTIDER", @"PEPTIDER", 3, 320.0, 12.0),
                new LibraryEntry(0 | LibraryEntry.DECOY_ID_BIT, @"KEDITPEP", @"KEDITPEP", 2, 450.0, 10.0),
                new LibraryEntry(5, @"SAMPLER", @"SAMPLER", 2, 400.0, 15.0),
            };
            var identity = new LibraryIdentity(library);
            Assert.AreSame(library[0], identity.Find(0));
            Assert.AreSame(library[1], identity.Find(1));
            Assert.AreSame(library[2], identity.Find(0 | LibraryEntry.DECOY_ID_BIT));
            Assert.AreSame(library[3], identity.Find(5));
            // Ids the library does not hold: a missing decoy, a gap, and past the end.
            Assert.IsNull(identity.Find(1 | LibraryEntry.DECOY_ID_BIT));
            Assert.IsNull(identity.Find(3));
            Assert.IsNull(identity.Find(99));
        }
    }
}
