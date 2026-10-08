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
    /// PrecursorCharge, Tr_recalibrated (<c>%.2f</c>), with <c>-ccs</c> IonMobility (1/K0,
    /// <c>%.4f</c>), ProteinID, Decoy, FragmentMz (Java
    /// <c>Float.toString</c> of the float32 m/z), RelativeIntensity (<c>%.4f</c>),
    /// FragmentType, FragmentNumber, FragmentCharge and FragmentLossType. Lines end in LF and
    /// the file is UTF-8 without a byte-order mark, as Carafe writes it. The rows go to a
    /// <see cref="PartialFile"/> that <see cref="Complete"/> moves over the final name.
    /// </summary>
    public sealed class CarafeLibraryTsvWriter : IDisposable
    {
        public const string FILE_NAME = @"carafe_spectral_library.tsv";

        private const string PRECURSOR_COLUMNS = "ModifiedPeptide\tStrippedPeptide\tPrecursorMz\tPrecursorCharge\tTr_recalibrated\t";
        private const string FRAGMENT_COLUMNS = "ProteinID\tDecoy\tFragmentMz\tRelativeIntensity\tFragmentType\tFragmentNumber\t" +
                                                "FragmentCharge\tFragmentLossType";

        public const string HEADER = PRECURSOR_COLUMNS + FRAGMENT_COLUMNS;

        /// <summary>Carafe's header with <c>-ccs</c>: IonMobility after Tr_recalibrated.</summary>
        public const string HEADER_WITH_ION_MOBILITY = PRECURSOR_COLUMNS + "IonMobility\t" + FRAGMENT_COLUMNS;

        private readonly PartialFile _file;
        private readonly StreamWriter _writer;

        /// <summary>
        /// Starts the TSV that <see cref="Complete"/> writes to <paramref name="path"/>; until
        /// then any file already there is left as it is.
        /// </summary>
        /// <param name="path">The final TSV.</param>
        /// <param name="ionMobility">Write Carafe's IonMobility column (<c>-ccs</c>).</param>
        public CarafeLibraryTsvWriter(string path, bool ionMobility = false)
        {
            WritesIonMobility = ionMobility;
            _file = new PartialFile(path);
            _writer = new StreamWriter(_file.PartialPath, false, new UTF8Encoding(false), 1 << 20) { NewLine = "\n" };
            _writer.Write((ionMobility ? HEADER_WITH_ION_MOBILITY : HEADER) + "\n");
        }

        /// <summary>The TSV has the IonMobility column, which <see cref="FormatRows"/> must then be asked for.</summary>
        public bool WritesIonMobility { get; }

        /// <summary>
        /// The TSV rows of one precursor. Pure, so batches can be formatted in parallel and
        /// written in order with <see cref="WriteRows"/>.
        /// </summary>
        /// <param name="spectrum">The precursor.</param>
        /// <param name="ionMobility">
        /// Include the IonMobility column, which the spectrum must then have: the
        /// <see cref="WritesIonMobility"/> of the writer the rows are for.
        /// </param>
        public static string FormatRows(LibrarySpectrum spectrum, bool ionMobility)
        {
            string mobility = string.Empty;
            if (ionMobility)
            {
                if (!spectrum.IonMobility.HasValue)
                    throw new InvalidOperationException(@"No ion mobility was predicted for " + spectrum);
                mobility = JavaNumberFormat.FormatFixed(spectrum.IonMobility.Value, 4) + "\t";
            }
            string precursor = spectrum.ModifiedPeptide + "\t" + spectrum.Sequence + "\t" +
                               JavaNumberFormat.ToString(spectrum.PrecursorMz) + "\t" +
                               spectrum.Charge.ToString(CultureInfo.InvariantCulture) + "\t" +
                               JavaNumberFormat.FormatFixed(spectrum.RetentionTime, 2) + "\t" +
                               mobility +
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
            WriteRows(FormatRows(spectrum, WritesIonMobility));
        }

        /// <summary>Writes text from <see cref="FormatRows"/>.</summary>
        public void WriteRows(string rows)
        {
            _writer.Write(rows);
        }

        /// <summary>Closes the TSV and moves it over the final name, replacing any file there.</summary>
        public void Complete()
        {
            _writer.Dispose();
            _file.Commit();
        }

        /// <summary>Closes the TSV; without <see cref="Complete"/>, deletes it and leaves the final name as it was.</summary>
        public void Dispose()
        {
            _writer.Dispose();
            _file.Discard();
        }
    }
}
