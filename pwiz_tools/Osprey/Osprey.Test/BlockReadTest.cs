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
        [TestMethod]
        public void TestBlockReadStreamAndLibraryIdentity()
        {
            VerifyLibraryIdentity();
            string path = Path.Combine(Path.GetTempPath(), @"osprey_blockread_" + Path.GetRandomFileName());
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
                }
            }
            finally
            {
                File.Delete(path);
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
                    for (int b = 0; b < read; b++)
                    {
                        if (actual[b] != expected[position + b])
                            Assert.Fail(@"block {0}, gate {1}: byte {2} differs", blockBytes, gate, position + b);
                    }
                    Assert.AreEqual(position + read, stream.Position);
                }
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
