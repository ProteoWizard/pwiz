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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Database.FileSystems
{
    /// <summary>
    /// Reads a .zip file's directory in order to locate entries that are stored
    /// UNCOMPRESSED (compression method 0). The bytes of such an entry form a
    /// contiguous range inside the .zip file and can therefore be read in place with
    /// a random-access stream (<see cref="SliceStream"/>) without extracting the
    /// entry. This is what lets Skyline open a document directly from a .sky.zip when
    /// the files that need random access (.skyd, .blib) were stored uncompressed.
    ///
    /// ZIP64 is supported, because .skyd and .blib files can exceed the 4 GB limit of
    /// the classic zip format (Skyline shares with <c>Zip64Option.AsNecessary</c>).
    ///
    /// This is a read-only view: it never modifies the .zip file.
    /// </summary>
    public sealed class RandomAccessZipFile
    {
        // ReSharper disable InconsistentNaming
        private const uint SIG_LOCAL_HEADER = 0x04034b50;
        private const uint SIG_CENTRAL_HEADER = 0x02014b50;
        private const uint SIG_END_OF_CENTRAL = 0x06054b50;
        private const uint SIG_ZIP64_END_OF_CENTRAL = 0x06064b50;
        private const uint SIG_ZIP64_LOCATOR = 0x07064b50;
        private const ushort ZIP64_EXTRA_ID = 0x0001;
        private const uint MASK32 = 0xFFFFFFFF;
        private const ushort MASK16 = 0xFFFF;
        // ReSharper restore InconsistentNaming

        public const ushort COMPRESSION_STORED = 0;
        public const ushort COMPRESSION_DEFLATE = 8;

        public RandomAccessZipFile(string zipPath)
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
        public IList<ZipEntryInfo> Entries { get; }

        /// <summary>
        /// Finds the entry whose name matches <paramref name="fileName"/>. The comparison
        /// is on the file name only (ignoring any directory prefix stored in the zip) and
        /// is case-sensitive (zip entry names are always case-sensitive).
        /// Returns null if no such entry exists.
        /// </summary>
        public ZipEntryInfo FindEntryByFileName(string fileName)
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
        /// True if every entry's name ends with one of the given suffixes (case-insensitive). Used
        /// to recognize a .sky.zip that contains only the expected document files (so it is safe to
        /// open in place). Suffixes may be compound, e.g. ".sky.view".
        /// </summary>
        public bool ContainsOnlyEntriesWithSuffixes(params string[] suffixes)
        {
            foreach (var entry in Entries)
            {
                bool matched = false;
                foreach (var suffix in suffixes)
                {
                    if (entry.FileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Finds the entry whose in-zip path matches <paramref name="entryPath"/>, comparing on the
        /// full path with separators normalized to '/', case-sensitively. Returns null if none.
        /// </summary>
        public ZipEntryInfo FindEntry(string entryPath)
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
        /// Returns the absolute offset in the .zip file where a stored entry's data begins (reads
        /// the local header). Combined with <see cref="ZipEntryInfo.UncompressedSize"/> this gives
        /// the byte range that can be handed to code (e.g. a SQLite VFS) that reads the file in place.
        /// </summary>
        public long GetStoredEntryDataOffset(ZipEntryInfo entry)
        {
            if (!entry.IsStored)
                throw new InvalidOperationException(
                    $@"Zip entry '{entry.FileName}' is compressed and has no contiguous byte range.");
            using (var stream = new FileStream(ZipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                return ReadLocalDataOffset(stream, entry.LocalHeaderOffset);
        }

        /// <summary>
        /// Returns true if every entry whose extension is one of <paramref name="extensions"/>
        /// (each including the leading dot, e.g. ".skyd") is stored UNCOMPRESSED. This is the test
        /// for whether a document could be opened in place from the .zip: all the files that need
        /// random access must be stored, so they can be read without extraction. Entries with other
        /// extensions (e.g. the linearly-read ".sky") do not matter.
        /// </summary>
        public bool AreEntriesStored(params string[] extensions)
        {
            foreach (var entry in Entries)
            {
                var ext = Path.GetExtension(entry.FileName);
                foreach (var e in extensions)
                {
                    if (string.Equals(ext, e, StringComparison.OrdinalIgnoreCase) && !entry.IsStored)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Opens a random-access read-only stream over a stored (uncompressed) entry,
        /// reading its bytes in place from the .zip file. Each call opens its own file
        /// handle so multiple entry streams can be read concurrently.
        /// </summary>
        public SliceStream OpenStoredEntry(ZipEntryInfo entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (!entry.IsStored)
                throw new InvalidOperationException(
                    $@"Zip entry '{entry.FileName}' is compressed (method {entry.CompressionMethod}) and cannot be read in place.");

            var stream = new FileStream(ZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                long dataOffset = ReadLocalDataOffset(stream, entry.LocalHeaderOffset);
                return new SliceStream(stream, dataOffset, entry.UncompressedSize);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Opens a read-only stream which decompresses a Deflate entry, reading its compressed
        /// bytes in place from the .zip file. The returned stream is forward-only, but it does
        /// report the entry's uncompressed <see cref="Stream.Length"/>, which progress reporting
        /// needs, and which a bare DeflateStream cannot supply.
        /// <br/>
        /// This exists instead of System.IO.Compression.ZipArchive because ZipArchive fails to open
        /// the entries of the large .zip files that Share produces ("A local file header is
        /// corrupt"), whereas the byte range this class works out is correct.
        /// </summary>
        public Stream OpenDeflatedEntry(ZipEntryInfo entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (entry.CompressionMethod != COMPRESSION_DEFLATE)
                throw new InvalidOperationException(
                    $@"Zip entry '{entry.FileName}' is not deflated (method {entry.CompressionMethod}).");

            var stream = new FileStream(ZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                long dataOffset = ReadLocalDataOffset(stream, entry.LocalHeaderOffset);
                // A zip entry holds a raw Deflate stream, which is what DeflateStream reads.
                var compressed = new SliceStream(stream, dataOffset, entry.CompressedSize);
                var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                return new DeflatedEntryStream(deflate, entry.UncompressedSize);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
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

        private static IList<ZipEntryInfo> ReadCentralDirectory(Stream stream)
        {
            long centralDirOffset;
            long centralDirCount;
            ReadEndOfCentralDirectory(stream, out centralDirOffset, out centralDirCount);

            var entries = new List<ZipEntryInfo>();
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
                ApplyZip64Extra(extra, ref uncompressedSize, ref compressedSize, ref localHeaderOffset,
                    uncompressedSize == MASK32, compressedSize == MASK32, localHeaderOffset == MASK32);

                entries.Add(new ZipEntryInfo
                {
                    FileName = ZipNameEncoding.GetString(nameBytes),
                    CompressionMethod = compressionMethod,
                    CompressedSize = compressedSize,
                    UncompressedSize = uncompressedSize,
                    LocalHeaderOffset = localHeaderOffset,
                });
            }
            return entries;
        }

        private static void ApplyZip64Extra(byte[] extra, ref long uncompressedSize, ref long compressedSize,
            ref long localHeaderOffset, bool needUncompressed, bool needCompressed, bool needOffset)
        {
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
            if (count16 == MASK16 || offset32 == MASK32)
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
    }

    /// <summary>
    /// One entry in a <see cref="RandomAccessZipFile"/>.
    /// </summary>
    public sealed class ZipEntryInfo
    {
        public string FileName { get; internal set; }
        public ushort CompressionMethod { get; internal set; }
        public long CompressedSize { get; internal set; }
        public long UncompressedSize { get; internal set; }
        internal long LocalHeaderOffset { get; set; }

        /// <summary>
        /// True if the entry is stored uncompressed, so its bytes can be read in place.
        /// </summary>
        public bool IsStored => CompressionMethod == RandomAccessZipFile.COMPRESSION_STORED;

        public override string ToString()
        {
            return $@"{FileName} ({(IsStored ? @"stored" : @"method " + CompressionMethod)}, {UncompressedSize} bytes)";
        }
    }

}
