/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentFastaGear.java
 *   (ProteinRecord, parseHeader, cleanSeq)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// A source protein of the entrapment FASTA: the <c>db|accession|entry</c> parts of its
    /// UniProt-style header and its cleaned sequence.
    /// </summary>
    public sealed class ProteinRecord
    {
        /// <summary>Database tag used when a header has no '|' fields.</summary>
        public const string DEFAULT_DB = @"sp";

        public ProteinRecord(string accession, string entryName, string db, string sequence)
        {
            Accession = accession;
            EntryName = entryName;
            Db = db;
            Sequence = sequence;
        }

        public string Accession { get; }
        public string EntryName { get; }
        public string Db { get; }
        public string Sequence { get; }

        /// <summary>
        /// Carafe's <c>cleanSeq</c> then <c>parseHeader</c>. The identifier is the header's first
        /// whitespace-delimited token: three or more '|' fields give db, accession and entry;
        /// two give db and accession, with the accession as entry; otherwise db is "sp" and
        /// the whole identifier is both accession and entry. Null when the cleaned sequence is
        /// empty, and Carafe then skips the entry.
        /// </summary>
        public static ProteinRecord Parse(string header, string rawSequence)
        {
            string sequence = CleanSequence(rawSequence);
            if (sequence.Length == 0)
                return null;
            if (header.StartsWith(@">", StringComparison.Ordinal))
                header = header.Substring(1);
            string identifier = JavaText.FirstToken(JavaText.Trim(header));
            string[] fields = JavaText.Split(identifier, '|');
            if (fields.Length >= 3)
                return new ProteinRecord(fields[1], fields[2], fields[0], sequence);
            if (fields.Length == 2)
                return new ProteinRecord(fields[1], fields[1], fields[0], sequence);
            return new ProteinRecord(identifier, identifier, DEFAULT_DB, sequence);
        }

        /// <summary>One leading and one trailing asterisk removed, then upper-cased.</summary>
        public static string CleanSequence(string sequence)
        {
            return sequence == null ? string.Empty : JavaText.ToUpper(JavaText.StripTerminalAsterisks(sequence));
        }

        public override string ToString()
        {
            return Db + @"|" + Accession + @"|" + EntryName;
        }
    }
}
