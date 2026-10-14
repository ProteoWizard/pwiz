/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on jfasta 2.2.0 (net.sf.jfasta.impl.FASTAElementIterator, FASTAElementHeaderReader and
 *   FASTASequenceReader), the FASTA reader Carafe (https://github.com/maccoss/carafe) uses
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
using System.Text;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Reads FASTA the way jfasta does for Carafe, quirks included, so the same file yields
    /// the same records:
    /// <list type="bullet">
    /// <item>The file is UTF-8 with no byte-order-mark handling, as Java's decoder has none.</item>
    /// <item>A header is a whole line, Java-trimmed, that must start with '&gt;'; anything else
    /// where a header is expected (a blank line or comment before the first entry, a BOM) is a
    /// format error.</item>
    /// <item>A sequence is every non-whitespace character up to the next '&gt;' anywhere, even
    /// mid-line.</item>
    /// <item>An entry with no sequence ends the file: jfasta reports it and stops, so later
    /// entries are never read.</item>
    /// </list>
    /// </summary>
    public static class FastaReader
    {
        /// <summary>jfasta's warning for an entry without a sequence, formatted with its header.</summary>
        public const string INVALID_ELEMENT_FORMAT = @"invalid fasta element [{0}]";

        /// <summary>jfasta's read chunk; a chunk of nothing but whitespace also ends a sequence.</summary>
        private const int CHUNK_SIZE = 8192;

        /// <summary>
        /// The records of <paramref name="path"/>, read lazily. <paramref name="warn"/>, when
        /// given, receives the message for an entry without a sequence.
        /// </summary>
        public static IEnumerable<FastaRecord> ReadFile(string path, Action<string> warn = null)
        {
            using (var reader = new StreamReader(path, new UTF8Encoding(false), false, 1 << 16))
            {
                foreach (var record in Read(reader, warn))
                    yield return record;
            }
        }

        /// <summary>The records of an open reader, read lazily.</summary>
        public static IEnumerable<FastaRecord> Read(TextReader reader, Action<string> warn = null)
        {
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                    yield break;
                line = JavaText.Trim(line);
                if (!line.StartsWith(@">", StringComparison.Ordinal))
                    throw new InvalidDataException(@"failed to get header from" + line);
                string header = line.Substring(1);
                string sequence = ReadSequence(reader);
                if (sequence == null)
                {
                    warn?.Invoke(string.Format(INVALID_ELEMENT_FORMAT, header));
                    yield break;
                }
                yield return new FastaRecord(header, sequence);
            }
        }

        private static string ReadSequence(TextReader reader)
        {
            var sequence = new StringBuilder();
            while (true)
            {
                int appended = 0;
                for (int i = 0; i < CHUNK_SIZE; i++)
                {
                    int next = reader.Peek();
                    if (next < 0 || next == '>')
                        break;
                    reader.Read();
                    char c = (char)next;
                    if (JavaText.IsWhitespace(c))
                        continue;
                    sequence.Append(c);
                    appended++;
                }
                if (appended == 0)
                    break;
            }
            return sequence.Length == 0 ? null : sequence.ToString();
        }
    }
}
