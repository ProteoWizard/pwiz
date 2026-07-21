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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Database.FileSystems
{
    /// <summary>
    /// Reads a .zip file's directory in order to read its entries without extracting the whole
    /// archive. An entry that is stored UNCOMPRESSED (compression method 0) forms a contiguous
    /// range of bytes inside the .zip file and can therefore be read in place with a random-access
    /// stream (<see cref="SliceStream"/>); any entry can be read sequentially (decompressing as it
    /// goes). This is what lets Skyline open a document directly from a .sky.zip: the files that
    /// need random access (.skyd, .blib) are stored uncompressed, and the rest are read
    /// sequentially.
    ///
    /// ZIP64 is supported, because .skyd and .blib files can exceed the 4 GB limit of
    /// the classic zip format (Skyline shares with <c>Zip64Option.AsNecessary</c>).
    ///
    /// This is a read-only view: it never modifies the .zip file.
    /// </summary>
    public sealed class ZipFileReader
    {
        // ReSharper disable InconsistentNaming
        private const uint SIG_LOCAL_HEADER = 0x04034b50;
        private const uint SIG_CENTRAL_HEADER = 0x02014b50;
        private const uint SIG_END_OF_CENTRAL = 0x06054b50;
        private const uint SIG_ZIP64_END_OF_CENTRAL = 0x06064b50;
        private const uint SIG_ZIP64_LOCATOR = 0x07064b50;
        private const ushort ZIP64_EXTRA_ID = 0x0001;
        // ReSharper restore InconsistentNaming

        public const ushort COMPRESSION_STORED = 0;
        public const ushort COMPRESSION_DEFLATE = 8;

        public ZipFileReader(string zipPath)
        {
            ZipPath = zipPath;
            using (var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Entries = ReadCentralDirectory(stream);
            }
        }

        public string ZipPath { get; }

        /// <summary>
        /// Every entry in the zip, in central-directory order.
        /// </summary>
        public IList<Entry> Entries { get; }

        /// <summary>
        /// Finds the entry whose name matches <paramref name="fileName"/>. The comparison
        /// is on the file name only (ignoring any directory prefix stored in the zip) and
        /// is case-sensitive (zip entry names are always case-sensitive).
        /// Returns null if no such entry exists.
        /// </summary>
        public Entry FindEntryByFileName(string fileName)
        {
            var name = Path.GetFileName(fileName);
            foreach (var entry in Entries)
            {
                // Zip entry names are case-sensitive.
                if (string.Equals(Path.GetFileName(entry.FileName), name, StringComparison.Ordinal))
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// Finds the entry whose in-zip path matches <paramref name="entryPath"/>, comparing on the
        /// full path with separators normalized to '/', case-sensitively. Returns null if none.
        /// </summary>
        public Entry FindEntry(string entryPath)
        {
            var normalized = NormalizeEntryPath(entryPath);
            foreach (var entry in Entries)
            {
                // Zip entry names are case-sensitive.
                if (string.Equals(NormalizeEntryPath(entry.FileName), normalized, StringComparison.Ordinal))
                    return entry;
            }
            return null;
        }

        private static string NormalizeEntryPath(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }

        /// <summary>
        /// Opens the .zip file for reading. Each call opens its own file handle, so multiple entry
        /// streams can be read concurrently.
        /// </summary>
        private FileStream OpenZipFile()
        {
            return new FileStream(ZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <summary>
        /// A forward-only stream over a decompressed zip entry which knows its own length, since
        /// the length is in the zip's directory even though the DeflateStream cannot report it.
        /// </summary>
        private sealed class DeflatedEntryStream : Stream
        {
            private readonly Stream _deflate;
            private long _position;

            public DeflatedEntryStream(Stream deflate, long length)
            {
                _deflate = deflate;
                Length = length;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length { get; }

            public override long Position
            {
                get { return _position; }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = _deflate.Read(buffer, offset, count);
                _position += read;
                return read;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _deflate.Dispose();
                base.Dispose(disposing);
            }
        }

        private IEnumerable<Entry> ReadCentralDirectory(Stream stream)
        {
            long centralDirOffset;
            long centralDirCount;
            ReadEndOfCentralDirectory(stream, out centralDirOffset, out centralDirCount);

            stream.Seek(centralDirOffset, SeekOrigin.Begin);
            var reader = new BinaryReader(stream);
            for (long i = 0; i < centralDirCount; i++)
            {
                uint signature = reader.ReadUInt32();
                if (signature != SIG_CENTRAL_HEADER)
                    throw new InvalidDataException(
                        $@"Expected central-directory header at entry {i} but found signature 0x{signature:X8}.");

                reader.ReadUInt16(); // version made by
                reader.ReadUInt16(); // version needed
                reader.ReadUInt16(); // general purpose flags
                ushort compressionMethod = reader.ReadUInt16();
                reader.ReadUInt16(); // last mod time
                reader.ReadUInt16(); // last mod date
                reader.ReadUInt32(); // crc32
                long compressedSize = reader.ReadUInt32();
                long uncompressedSize = reader.ReadUInt32();
                int fileNameLength = reader.ReadUInt16();
                int extraFieldLength = reader.ReadUInt16();
                int commentLength = reader.ReadUInt16();
                reader.ReadUInt16(); // disk number start
                reader.ReadUInt16(); // internal attributes
                reader.ReadUInt32(); // external attributes
                long localHeaderOffset = reader.ReadUInt32();

                byte[] nameBytes = reader.ReadBytes(fileNameLength);
                byte[] extra = reader.ReadBytes(extraFieldLength);
                if (commentLength > 0)
                    reader.ReadBytes(commentLength);

                // ZIP64: any field set to its sentinel is really stored 64-bit in the extra field.
                ApplyZip64Extra(extra, ref uncompressedSize, ref compressedSize, ref localHeaderOffset);

                yield return new Entry(this, localHeaderOffset)
                {
                    FileName = ZipNameEncoding.GetString(nameBytes),
                    CompressionMethod = compressionMethod,
                    CompressedSize = compressedSize,
                    UncompressedSize = uncompressedSize,
                };
            }
        }

        /// <summary>
        /// Applies the ZIP64 extended-information extra field to the sizes and offset read from a
        /// central-directory header. In the classic zip format each of those three values is only 32
        /// bits; when a value does not fit, the header stores the sentinel 0xFFFFFFFF and the real
        /// 64-bit value is carried in the ZIP64 extra field (id 0x0001) inside the header's variable
        /// extra-field block. This finds that field and overwrites the sentinels with the real values.
        /// </summary>
        /// <param name="extra">The raw bytes of the header's extra-field block, which is a sequence of
        /// (id, size, data) records; the ZIP64 record is one of them (or absent).</param>
        /// <param name="uncompressedSize">On entry, the 32-bit uncompressed size from the header;
        /// overwritten with the 64-bit value if it was the sentinel.</param>
        /// <param name="compressedSize">On entry, the 32-bit compressed size from the header;
        /// overwritten with the 64-bit value if it was the sentinel.</param>
        /// <param name="localHeaderOffset">On entry, the 32-bit local-header offset from the header;
        /// overwritten with the 64-bit value if it was the sentinel.</param>
        private static void ApplyZip64Extra(byte[] extra, ref long uncompressedSize, ref long compressedSize,
            ref long localHeaderOffset)
        {
            // A value that overflowed 32 bits was written as the sentinel, so its real value is the
            // one carried 64-bit in the ZIP64 field.
            bool needUncompressed = uncompressedSize == uint.MaxValue;
            bool needCompressed = compressedSize == uint.MaxValue;
            bool needOffset = localHeaderOffset == uint.MaxValue;
            if (!(needUncompressed || needCompressed || needOffset))
                return;
            int pos = 0;
            while (pos + 4 <= extra.Length)
            {
                ushort id = BitConverter.ToUInt16(extra, pos);
                ushort size = BitConverter.ToUInt16(extra, pos + 2);
                int dataPos = pos + 4;
                if (id == ZIP64_EXTRA_ID)
                {
                    // Only read within this extra field's own declared size (and never past the
                    // buffer), so a malformed size can't pull bytes from a following extra field.
                    int fieldEnd = Math.Min(dataPos + size, extra.Length);
                    // Fields appear in this fixed order, but only for those that were the sentinel.
                    if (needUncompressed && dataPos + 8 <= fieldEnd)
                    {
                        uncompressedSize = BitConverter.ToInt64(extra, dataPos);
                        dataPos += 8;
                    }
                    if (needCompressed && dataPos + 8 <= fieldEnd)
                    {
                        compressedSize = BitConverter.ToInt64(extra, dataPos);
                        dataPos += 8;
                    }
                    if (needOffset && dataPos + 8 <= fieldEnd)
                    {
                        localHeaderOffset = BitConverter.ToInt64(extra, dataPos);
                    }
                    return;
                }
                pos = dataPos + size;
            }
        }

        /// <summary>
        /// Reads the local file header at <paramref name="localHeaderOffset"/> and returns the
        /// absolute offset in the .zip file where this entry's file data begins. The local
        /// header's name and extra-field lengths can differ from the central directory's, so
        /// they must be read from the local header itself.
        /// </summary>
        private static long ReadLocalDataOffset(Stream stream, long localHeaderOffset)
        {
            stream.Seek(localHeaderOffset, SeekOrigin.Begin);
            var reader = new BinaryReader(stream);
            uint signature = reader.ReadUInt32();
            if (signature != SIG_LOCAL_HEADER)
                throw new InvalidDataException(
                    $@"Expected local file header at offset {localHeaderOffset} but found signature 0x{signature:X8}.");
            stream.Seek(localHeaderOffset + 26, SeekOrigin.Begin);
            int fileNameLength = reader.ReadUInt16();
            int extraFieldLength = reader.ReadUInt16();
            return localHeaderOffset + 30 + fileNameLength + extraFieldLength;
        }

        private static void ReadEndOfCentralDirectory(Stream stream, out long centralDirOffset, out long centralDirCount)
        {
            long eocdPos = FindSignatureFromEnd(stream, SIG_END_OF_CENTRAL, 22);
            if (eocdPos < 0)
                throw new InvalidDataException(@"Not a valid zip file: end-of-central-directory record not found.");

            stream.Seek(eocdPos + 10, SeekOrigin.Begin);
            var reader = new BinaryReader(stream);
            ushort count16 = reader.ReadUInt16();       // total entries this disk
            reader.ReadUInt32();                        // central directory size
            uint offset32 = reader.ReadUInt32();        // central directory offset

            centralDirCount = count16;
            centralDirOffset = offset32;

            // If either field is the ZIP64 sentinel, read the real values from the ZIP64 EOCD.
            if (count16 == ushort.MaxValue || offset32 == uint.MaxValue)
            {
                long zip64Eocd = ReadZip64Locator(stream, eocdPos);
                stream.Seek(zip64Eocd, SeekOrigin.Begin);
                uint sig = reader.ReadUInt32();
                if (sig != SIG_ZIP64_END_OF_CENTRAL)
                    throw new InvalidDataException(
                        $@"Expected ZIP64 end-of-central-directory record but found signature 0x{sig:X8}.");
                stream.Seek(zip64Eocd + 32, SeekOrigin.Begin);
                centralDirCount = reader.ReadInt64();   // total entries
                reader.ReadInt64();                     // central directory size
                centralDirOffset = reader.ReadInt64();  // central directory offset
            }
        }

        private static long ReadZip64Locator(Stream stream, long eocdPos)
        {
            // The ZIP64 end-of-central-directory locator is the 20 bytes immediately before the EOCD.
            long locatorPos = eocdPos - 20;
            if (locatorPos < 0)
                throw new InvalidDataException(@"Zip file claims ZIP64 but the ZIP64 locator is missing.");
            stream.Seek(locatorPos, SeekOrigin.Begin);
            var reader = new BinaryReader(stream);
            uint sig = reader.ReadUInt32();
            if (sig != SIG_ZIP64_LOCATOR)
                throw new InvalidDataException(
                    $@"Expected ZIP64 locator before end-of-central-directory but found signature 0x{sig:X8}.");
            reader.ReadUInt32(); // disk with the ZIP64 EOCD
            return reader.ReadInt64(); // offset of the ZIP64 EOCD record
        }

        /// <summary>
        /// Scans backward from the end of the stream for a 4-byte little-endian signature.
        /// <paramref name="minRecordSize"/> is how many bytes the record occupies at minimum,
        /// so the search does not run past the start of a too-short file. Returns the absolute
        /// offset of the signature, or -1 if not found.
        /// </summary>
        private static long FindSignatureFromEnd(Stream stream, uint signature, int minRecordSize)
        {
            const int maxComment = 0xFFFF;
            long fileLength = stream.Length;
            int searchLength = (int) Math.Min(fileLength, minRecordSize + maxComment);
            if (searchLength < minRecordSize)
                return -1;
            long start = fileLength - searchLength;
            stream.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[searchLength];
            ReadExactly(stream, buffer, 0, searchLength);
            for (int i = searchLength - minRecordSize; i >= 0; i--)
            {
                if (BitConverter.ToUInt32(buffer, i) == signature)
                    return start + i;
            }
            return -1;
        }

        public static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, offset + total, count - total);
                if (n <= 0)
                    throw new EndOfStreamException();
                total += n;
            }
        }

        private static readonly System.Text.Encoding ZipNameEncoding = System.Text.Encoding.UTF8;

        /// <summary>
        /// One entry in a <see cref="ZipFileReader"/>. It keeps a reference to the reader it came
        /// from and knows its own path inside the .zip, so it is a self-contained handle: it can be
        /// opened, and its <see cref="LogicalPath"/> computed, without the caller holding on to the
        /// reader separately.
        /// </summary>
        public sealed class Entry
        {
            public Entry(ZipFileReader zipFileReader, long localHeaderOffset)
            {
                ZipFileReader = zipFileReader;
                LocalHeaderOffset = localHeaderOffset;
            }

            /// <summary>
            /// The reader (and thus the .zip file) this entry belongs to.
            /// </summary>
            public ZipFileReader ZipFileReader { get; }

            /// <summary>
            /// The entry's path inside the .zip.
            /// </summary>
            public string FileName { get; internal set; }
            public ushort CompressionMethod { get; internal set; }
            public long CompressedSize { get; internal set; }
            public long UncompressedSize { get; internal set; }
            /// <summary>
            /// Where this entry's local file header starts in the .zip. The entry's data follows the
            /// header, so this is what the stream-opening methods here seek to.
            /// </summary>
            private long LocalHeaderOffset { get; }

            /// <summary>
            /// True if the entry is stored uncompressed, so its bytes can be read in place.
            /// </summary>
            public bool IsStored => CompressionMethod == COMPRESSION_STORED;

            /// <summary>
            /// The path this entry would have if it were extracted next to its .zip: the .zip's
            /// path followed by the entry's path inside the zip (e.g.
            /// <c>C:\Doc.sky.zip\Library.blib</c>). This is the logical path a <see cref="FilePath"/>
            /// uses to refer to the entry, and is what pool identity and logging key off.
            /// </summary>
            public string LogicalPath => ZipFileReader.ZipPath + Path.DirectorySeparatorChar + FileName;

            /// <summary>
            /// The absolute offset in the .zip file where this stored (uncompressed) entry's data
            /// begins. Together with the containing .zip path (<see cref="ZipFileReader"/>'s
            /// <see cref="FileSystems.ZipFileReader.ZipPath"/>) and <see cref="UncompressedSize"/>,
            /// this is the byte range that code reading the entry in place (e.g. the SQLite VFS)
            /// needs. Reads the local header, so it does file I/O. Throws for a compressed entry,
            /// which has no contiguous byte range.
            /// </summary>
            public long GetStoredDataOffset()
            {
                if (!IsStored)
                    throw new InvalidOperationException(
                        $@"Zip entry '{FileName}' is compressed and has no contiguous byte range.");
                using (var stream = ZipFileReader.OpenZipFile())
                    return ReadLocalDataOffset(stream, LocalHeaderOffset);
            }

            /// <summary>
            /// Opens a random-access (seekable) read-only stream over this entry, reading its bytes
            /// in place from the .zip file. Each call opens its own file handle so multiple entry
            /// streams can be read concurrently.
            /// <br/>
            /// Only a stored entry can be read this way: it is the only kind whose bytes are a
            /// contiguous range of the .zip. Use <see cref="OpenSequentialStream"/> to read any
            /// entry from beginning to end.
            /// </summary>
            public SliceStream OpenRandomAccessStream()
            {
                if (!IsStored)
                    throw new InvalidOperationException(
                        $@"Zip entry '{FileName}' is compressed (method {CompressionMethod}) and cannot be read in place.");

                var stream = ZipFileReader.OpenZipFile();
                try
                {
                    long dataOffset = ReadLocalDataOffset(stream, LocalHeaderOffset);
                    return new SliceStream(stream, dataOffset, UncompressedSize);
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }

            /// <summary>
            /// Opens a read-only stream which reads this entry from beginning to end, decompressing
            /// it as it is read so that a big entry never has to be held in memory. A stored entry is
            /// read in place (and the stream happens to be seekable); a deflated entry is inflated.
            /// <br/>
            /// Stored and deflated are the only methods this reads. Ionic can write others (e.g.
            /// bzip2), so callers which might see an arbitrary .zip must be prepared for the
            /// exception - Skyline only ever reads a .zip whose entries it has already checked.
            /// <br/>
            /// This uses System.IO.Compression rather than DotNetZip because its DeflateStream is
            /// native-backed and DotNetZip's inflater is managed: on a 5 GB .sky, DotNetZip took
            /// 26.5 s against 9.7 s, and only 5 s of that difference was its CRC checking. It does
            /// not use System.IO.Compression's ZipArchive, which cannot open the entries of the
            /// large .zip files Share produces ("A local file header is corrupt"), only its raw
            /// inflater, which never looks at a zip header at all.
            /// </summary>
            public Stream OpenSequentialStream()
            {
                if (IsStored)
                    return OpenRandomAccessStream();
                if (CompressionMethod != COMPRESSION_DEFLATE)
                    throw new InvalidOperationException(
                        $@"Zip entry '{FileName}' uses compression method {CompressionMethod}, which cannot be read.");

                var stream = ZipFileReader.OpenZipFile();
                try
                {
                    long dataOffset = ReadLocalDataOffset(stream, LocalHeaderOffset);
                    // A zip entry holds a raw Deflate stream, which is what DeflateStream reads.
                    var compressed = new SliceStream(stream, dataOffset, CompressedSize);
                    var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                    return new DeflatedEntryStream(deflate, UncompressedSize);
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }

            public override string ToString()
            {
                return $@"{FileName} ({(IsStored ? @"stored" : @"method " + CompressionMethod)}, {UncompressedSize} bytes)";
            }
        }
    }
}
