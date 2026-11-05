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
using System.Globalization;
using System.IO;
using pwiz.BiblioSpec.IO;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Loads a DIA-NN 2.x spectral library <c>.parquet</c>: a predicted library converted with
    /// <c>diann --lib x.speclib --gen-spec-lib --out-lib x.parquet</c>, or the empirical
    /// <c>report-lib.parquet</c> of a search. The columns are read through BiblioSpec.IO's
    /// <see cref="DiannLibraryParquetReader"/>, the reader BiblioSpec's DIA-NN import uses, and
    /// each row goes through the same assembly as a <see cref="DiannTsvLoader"/> line
    /// (<see cref="DiannTsvLoader.AddFragmentRow"/>, <see cref="DiannTsvLoader.BuildEntries"/>),
    /// so a library reads the same from either format. The columns map as DIA-NN's .tsv names
    /// map: <c>RT</c> is the retention time a .tsv's <c>NormalizedRetentionTime</c> gives (DIA-NN's
    /// library scale in both predicted and empirical libraries), <c>Protein.Ids</c> the proteins,
    /// <c>Genes</c> the gene names, <c>Decoy</c> 1 a library decoy. Every other column, the
    /// empirical library's trailing <c>Trace</c> among them, is not read.
    /// </summary>
    public class DiannParquetLoader
    {
        /// <summary>
        /// The version of this column mapping, in the library composition hash beside the
        /// row assembly's own <see cref="DiannTsvLoader.READER_VERSION"/>. 1: the first.
        /// </summary>
        public const int READER_VERSION = 1;

        /// <summary>
        /// The library composition term of a .parquet read by this loader: both versions and the
        /// tolerance it types within, as <see cref="DiannTsvLoader.CacheTerm"/> is for a .tsv.
        /// </summary>
        public static string CacheTerm(FragmentToleranceConfig fragmentTolerance)
        {
            // ReSharper disable LocalizableElement
            return string.Format(CultureInfo.InvariantCulture, "parquet_reader:{0},{1},{2},{3}\n",
                READER_VERSION, DiannTsvLoader.READER_VERSION, fragmentTolerance.Tolerance, fragmentTolerance.Unit);
            // ReSharper restore LocalizableElement
        }

        /// <summary>
        /// The columns this loader requires: those a .tsv must have, and the retention time,
        /// which DIA-NN always writes.
        /// </summary>
        internal const DiannLibraryColumns REQUIRED_COLUMNS = DiannLibraryColumns.modified_sequence |
            DiannLibraryColumns.precursor_charge | DiannLibraryColumns.precursor_mz | DiannLibraryColumns.rt |
            DiannLibraryColumns.product_mz | DiannLibraryColumns.relative_intensity;

        /// <summary>The columns read when present, as the .tsv reads its optional columns.</summary>
        internal const DiannLibraryColumns OPTIONAL_COLUMNS = DiannLibraryColumns.stripped_sequence |
            DiannLibraryColumns.fragment_type | DiannLibraryColumns.fragment_charge |
            DiannLibraryColumns.fragment_series_number | DiannLibraryColumns.fragment_loss_type |
            DiannLibraryColumns.protein_ids | DiannLibraryColumns.genes | DiannLibraryColumns.decoy;

        private readonly DiannTsvLoader _assembly;

        public DiannParquetLoader() : this(null)
        {
        }

        /// <summary>
        /// A loader that also compares the fragment types the library's columns state with the
        /// types Osprey computes from m/z within <paramref name="fragmentTolerance"/>, as
        /// <see cref="DiannTsvLoader(FragmentToleranceConfig)"/> does.
        /// </summary>
        public DiannParquetLoader(FragmentToleranceConfig fragmentTolerance)
        {
            _assembly = new DiannTsvLoader(fragmentTolerance);
        }

        /// <summary>The library's stated fragment types compared with Osprey's typing.</summary>
        public FragmentTypeCheck TypeCheck => _assembly.TypeCheck;

        /// <summary>
        /// Load library entries from a DIA-NN library <c>.parquet</c>, row group by row group.
        /// An empty library - DIA-NN writes one, with the full schema and no rows, when a search
        /// identifies nothing - loads as no entries, with a line in the log saying why.
        /// </summary>
        public List<LibraryEntry> Load(string path, Action<string> logInfo = null)
        {
            DiannLibraryParquetReader reader;
            try
            {
                reader = DiannLibraryParquetReader.Open(path, REQUIRED_COLUMNS | OPTIONAL_COLUMNS, OPTIONAL_COLUMNS);
            }
            catch (DiannLibraryMissingColumnException ex)
            {
                throw new InvalidDataException(string.Format(
                    OspreyIOResources.ColumnIndices_RequireColumn_Missing_required_column___0_, ex.ColumnName));
            }
            using (reader)
            {
                if (reader.RowCount == 0)
                {
                    logInfo?.Invoke(string.Format(
                        OspreyIOResources.DiannParquetLoader_Load_The_DIA_NN_library__0__has_no_rows__DIA_NN_writes_an_empty_library_when_its_search_identifies_nothing_,
                        path));
                    return new List<LibraryEntry>();
                }

                var precursorMap = new Dictionary<string, DiannTsvLoader.PrecursorData>();
                var errors = new DiannTsvLoader.LibraryLineErrors();
                // Progress over the file's rows, one report per row group: a human library is
                // tens of millions of fragment rows in a handful of groups.
                ProgressReporter.Run(
                    string.Format(OspreyIOResources.DiannTsvLoader_Load_Parsing__0_, Path.GetFileName(path)), reader.RowCount, readProgress =>
                {
                    long rowsRead = 0;
                    // Every row group, none taken as representative: DIA-NN 2.7 writes an empty one
                    // beside the rows.
                    for (int g = 0; g < reader.RowGroupCount; g++)
                    {
                        var group = reader.ReadRowGroup(g);
                        for (int i = 0; i < group.RowCount; i++)
                            AddRow(group, i, rowsRead + i + 1, precursorMap, errors);
                        rowsRead += group.RowCount;
                        readProgress.Report(rowsRead);
                    }
                }, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS);

                // A library with any invalid row is refused whole, as a .tsv with an invalid line is.
                if (errors.Any)
                    throw errors.ToException(OspreyIOResources.DiannParquetLoader_Load__0__library_rows_have_errors__Fix_the_library_and_load_it_again_);

                return _assembly.BuildEntries(precursorMap, logInfo);
            }
        }

        /// <summary>
        /// Adds row <paramref name="i"/> of <paramref name="group"/> (row <paramref name="rowNum"/>
        /// of the file, 1-based) to <paramref name="precursorMap"/>, checking every value as
        /// <see cref="DiannTsvLoader"/> checks the same value in a .tsv: a charge or ion ordinal
        /// from 1 to 255, an ion type letter, a loss <see cref="NeutralLoss.Parse"/> reads, a decoy
        /// flag of 0 or 1, a modified sequence that is not empty. Each failure is added to
        /// <paramref name="errors"/>, all of them, so the load can name every row to fix.
        /// </summary>
        private static void AddRow(DiannLibraryRowGroup group, int i, long rowNum,
            Dictionary<string, DiannTsvLoader.PrecursorData> precursorMap, DiannTsvLoader.LibraryLineErrors errors)
        {
            bool valid = true;
            string modifiedPeptide = group.ModifiedSequence[i];
            if (string.IsNullOrEmpty(modifiedPeptide))
            {
                AddError(rowNum, errors, string.Format(OspreyIOResources.DiannParquetLoader_AddRow___row__0___Missing__1_,
                    rowNum, DiannLibraryParquetReader.ColumnName(DiannLibraryColumns.modified_sequence)));
                valid = false;
            }
            byte charge = PositiveByte(group.PrecursorCharge[i], DiannLibraryColumns.precursor_charge, rowNum, errors, ref valid);

            IonType ionType = IonType.Unknown;
            if (group.FragmentType != null)
            {
                string type = group.FragmentType[i];
                ionType = type.Length == 1 ? IonTypeExtensions.FromChar(type[0]) : IonType.Unknown;
                if (ionType == IonType.Unknown)
                    AddInvalid(DiannLibraryColumns.fragment_type, type, rowNum, errors, ref valid);
            }
            byte ordinal = 0;
            if (group.FragmentSeriesNumber != null)
            {
                ordinal = PositiveByte(group.FragmentSeriesNumber[i], DiannLibraryColumns.fragment_series_number, rowNum,
                    errors, ref valid);
            }
            byte fragmentCharge = 1;
            if (group.FragmentCharge != null)
                fragmentCharge = PositiveByte(group.FragmentCharge[i], DiannLibraryColumns.fragment_charge, rowNum, errors, ref valid);
            NeutralLossCode lossCode = NeutralLossCode.None;
            double lossMass = 0.0;
            if (group.FragmentLossType != null)
            {
                var loss = NeutralLoss.Parse(group.FragmentLossType[i]);
                if (loss.HasValue)
                    (lossCode, lossMass) = loss.Value;
                else
                    AddInvalid(DiannLibraryColumns.fragment_loss_type, group.FragmentLossType[i], rowNum, errors, ref valid);
            }
            bool isDecoy = false;
            if (group.Decoy != null)
            {
                long decoy = group.Decoy[i];
                if (decoy == 0 || decoy == 1)
                    isDecoy = decoy == 1;
                else
                    AddInvalid(DiannLibraryColumns.decoy, decoy.ToString(CultureInfo.InvariantCulture), rowNum, errors, ref valid);
            }
            if (!valid)
                return;

            var annotation = new FragmentAnnotation
            {
                IonType = ionType,
                Ordinal = ordinal,
                Charge = fragmentCharge,
                NeutralLoss = lossCode,
                CustomLossMass = lossMass
            };
            // The float32 values widened exactly, as a .tsv written from this file prints them.
            var unrecognized = DiannTsvLoader.AddFragmentRow(precursorMap, modifiedPeptide, group.StrippedSequence?[i],
                charge, group.PrecursorMz[i], group.RT[i], group.ProductMz[i], group.RelativeIntensity[i], annotation,
                isDecoy, group.ProteinIds?[i], group.Genes?[i]);
            if (unrecognized != null)
            {
                foreach (string modStr in unrecognized)
                    AddError(rowNum, errors, InvalidValueMessage(DiannLibraryColumns.modified_sequence, modStr, rowNum));
            }
        }

        /// <summary>A count from 1 to 255 - a charge or an ion ordinal; anything else is invalid.</summary>
        private static byte PositiveByte(long value, DiannLibraryColumns column, long rowNum,
            DiannTsvLoader.LibraryLineErrors errors, ref bool valid)
        {
            if (value >= 1 && value <= byte.MaxValue)
                return (byte)value;
            AddInvalid(column, value.ToString(CultureInfo.InvariantCulture), rowNum, errors, ref valid);
            return 0;
        }

        private static void AddInvalid(DiannLibraryColumns column, string value, long rowNum,
            DiannTsvLoader.LibraryLineErrors errors, ref bool valid)
        {
            valid = false;
            AddError(rowNum, errors, InvalidValueMessage(column, value, rowNum));
        }

        private static string InvalidValueMessage(DiannLibraryColumns column, string value, long rowNum)
        {
            return string.Format(OspreyIOResources.DiannParquetLoader_AddRow___row__0___Invalid__1____2__,
                rowNum, DiannLibraryParquetReader.ColumnName(column), value);
        }

        /// <summary>
        /// Adds <paramref name="message"/> for row <paramref name="rowNum"/>; the error list counts
        /// rows by an int, which a library's row count stays far below.
        /// </summary>
        private static void AddError(long rowNum, DiannTsvLoader.LibraryLineErrors errors, string message)
        {
            errors.Add((int)Math.Min(rowNum, int.MaxValue), message);
        }
    }
}
