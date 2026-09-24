/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear
 *   (generate_spectral_library_parquet, the TSV branch)
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
using System.Globalization;
using System.IO;
using System.Text;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.IO
{
    /// <summary>
    /// Writes Carafe's library TSV (<c>carafe_spectral_library.tsv</c>), one row per fragment:
    /// ModifiedPeptide, StrippedPeptide, PrecursorMz (Java <c>Double.toString</c>),
    /// PrecursorCharge, Tr_recalibrated (<c>%.2f</c>), ProteinID, Decoy, FragmentMz (Java
    /// <c>Float.toString</c> of the float32 m/z), RelativeIntensity (<c>%.4f</c>),
    /// FragmentType, FragmentNumber, FragmentCharge and FragmentLossType. Lines end in LF and
    /// the file is UTF-8 without a byte-order mark, as Carafe writes it.
    /// </summary>
    public sealed class CarafeLibraryTsvWriter : IDisposable
    {
        public const string FILE_NAME = @"carafe_spectral_library.tsv";

        public const string HEADER = "ModifiedPeptide\tStrippedPeptide\tPrecursorMz\tPrecursorCharge\tTr_recalibrated\t" +
                                     "ProteinID\tDecoy\tFragmentMz\tRelativeIntensity\tFragmentType\tFragmentNumber\t" +
                                     "FragmentCharge\tFragmentLossType";

        private readonly StreamWriter _writer;

        public CarafeLibraryTsvWriter(string path)
        {
            _writer = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 20) { NewLine = "\n" };
            _writer.Write(HEADER + "\n");
        }

        /// <summary>
        /// The TSV rows of one precursor. Pure, so batches can be formatted in parallel and
        /// written in order with <see cref="WriteRows"/>.
        /// </summary>
        public static string FormatRows(LibrarySpectrum spectrum)
        {
            string precursor = spectrum.ModifiedPeptide + "\t" + spectrum.Sequence + "\t" +
                               JavaNumberFormat.ToString(spectrum.PrecursorMz) + "\t" +
                               spectrum.Charge.ToString(CultureInfo.InvariantCulture) + "\t" +
                               JavaNumberFormat.FormatFixed(spectrum.RetentionTime, 2) + "\t" +
                               spectrum.ProteinId + "\t" +
                               spectrum.Decoy.ToString(CultureInfo.InvariantCulture) + "\t";
            var rows = new StringBuilder((precursor.Length + 40) * spectrum.Fragments.Count);
            foreach (var fragment in spectrum.Fragments)
            {
                rows.Append(precursor)
                    .Append(JavaNumberFormat.ToString(fragment.Mz)).Append('\t')
                    .Append(JavaNumberFormat.FormatFixed(fragment.RelativeIntensity, 4)).Append('\t')
                    .Append(fragment.IonType).Append('\t')
                    .Append(fragment.Ordinal.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(fragment.Charge.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(fragment.LossType).Append('\n');
            }
            return rows.ToString();
        }

        public void Write(LibrarySpectrum spectrum)
        {
            WriteRows(FormatRows(spectrum));
        }

        /// <summary>Writes text from <see cref="FormatRows"/>.</summary>
        public void WriteRows(string rows)
        {
            _writer.Write(rows);
        }

        public void Dispose()
        {
            _writer.Dispose();
        }
    }
}
