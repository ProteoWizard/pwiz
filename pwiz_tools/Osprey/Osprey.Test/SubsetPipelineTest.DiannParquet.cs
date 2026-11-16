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
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using Parquet.Schema;
using pwiz.BiblioSpec.IO;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    public partial class SubsetPipelineTest
    {
        private const string DIANN_PARQUET_ZIP = @"DiannParquetSubset.zip";

        /// <summary>
        /// A DIA-NN 2.7 library <c>.parquet</c> loads as the library its <c>.tsv</c> conversion
        /// loads (<c>TestData\DiannParquetSubset.zip</c>; provenance in its README.md): the
        /// 20-protein predicted library and its variable-modification twin (Ox(M), UniMod:35, and
        /// the N-terminal acetyl, UniMod:1, written before the first residue), entry by entry and
        /// field by field as <see cref="TestLibraryLoadsIdenticallyFromTsvAndBlib"/> compares a
        /// .tsv with its .blib. The .tsv was written by pyarrow from the parquet's float32 values,
        /// whose shortest text reads back as the same doubles, so the comparison is exact. Gene
        /// names are the one field the conversion dropped. Then <see cref="LibraryLoader"/>: the
        /// derived library <c>&lt;library&gt;.parquet.blib</c> is written and read back as the same
        /// library; the empty library a search that identifies nothing writes loads as no entries
        /// with a line saying why; a <c>.speclib</c> is refused with the DIA-NN conversion command.
        /// </summary>
        [TestMethod]
        public void TestDiannParquetLibraryLoadsAsItsTsv()
        {
            string dir = Path.Combine(_testDir, @"diann-parquet");
            ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, @"TestData", DIANN_PARQUET_ZIP), dir);
            var tolerance = FragmentToleranceConfig.Hram(10);
            var report = new List<string>();

            var predicted = AssertParquetLoadsAsTsv(dir, @"lib", tolerance, 2637, report);
            Assert.IsTrue(predicted.SelectMany(e => e.Modifications).All(m => m.UnimodId == 4));
            var varmods = AssertParquetLoadsAsTsv(dir, @"lib-varmods", tolerance, 3342, report);
            var oxidation = varmods.SelectMany(e => e.Modifications).Where(m => m.UnimodId == 35).ToList();
            Assert.IsTrue(oxidation.Count > 0);
            Assert.AreEqual(UniMod.Find(35).Mass, oxidation[0].MassDelta);
            var acetylated = varmods.Where(e => e.Modifications.Any(m => m.UnimodId == 1)).ToList();
            Assert.IsTrue(acetylated.Count > 0);
            // On the first residue, whatever it is (the FASTA has an X-initial protein too).
            foreach (var entry in acetylated)
                Assert.AreEqual(0, entry.Modifications.First(m => m.UnimodId == 1).Position, entry.ModifiedSequence);

            report.AddRange(DerivedParquetLibraryDifferences(dir, tolerance));
            Assert.AreEqual(0, report.Count, string.Join(Environment.NewLine, report));

            AssertEmptyParquetLibrary(Path.Combine(dir, @"report-lib-empty.parquet"), tolerance);
            AssertInvalidParquetLibraries(dir, tolerance);

            // A .speclib is DIA-NN's undocumented binary: refused, with the command that converts it.
            string speclib = Path.Combine(dir, @"lib.predicted.speclib");
            var refusal = Assert.ThrowsException<NotSupportedException>(() => LibrarySource.FromPath(speclib));
            Assert.AreEqual(string.Format(
                OspreyCoreResources.LibrarySource_FromPath_A_DIA_NN__0__library_cannot_be_read__convert_it_to_the_DIA_NN__1__library_format_with_,
                LibrarySource.EXT_SPECLIB, LibrarySource.EXT_PARQUET, LibrarySource.SpeclibConversionCommand(speclib)),
                refusal.Message);
            StringAssert.Contains(refusal.Message, Path.ChangeExtension(speclib, LibrarySource.EXT_PARQUET));
            Assert.AreEqual(LibraryFormat.DiannParquet, LibrarySource.FromPath(@"report-lib.parquet").Format);

            // The validity key's reader term carries both readers' versions.
            Assert.AreEqual(OspreyTask.TSV_READER_TERM + @";parquetreader=" + DiannParquetLoader.READER_VERSION,
                OspreyTask.PARQUET_READER_TERM);
        }

        /// <summary>
        /// The library <paramref name="name"/><c>.parquet</c> in <paramref name="dir"/> loaded,
        /// after its differences from <paramref name="name"/><c>.tsv</c> are added to
        /// <paramref name="report"/>; it must hold <paramref name="expectedCount"/> precursors and
        /// carry DIA-NN's gene names, which the .tsv conversion does not.
        /// </summary>
        private static List<LibraryEntry> AssertParquetLoadsAsTsv(string dir, string name, FragmentToleranceConfig tolerance,
            int expectedCount, List<string> report)
        {
            var fromTsv = new DiannTsvLoader(tolerance).Load(Path.Combine(dir, name + TextUtil.EXT_TSV));
            var fromParquet = new DiannParquetLoader(tolerance).Load(Path.Combine(dir, name + LibrarySource.EXT_PARQUET));
            Assert.AreEqual(expectedCount, fromParquet.Count, name);
            var differences = LibraryDifferences(fromTsv, fromParquet)
                .Where(line => !line.Contains(@": GeneNames"))
                .ToList();
            report.AddRange(DescribeDifferences(name + @" (.parquet vs .tsv)", differences, fromTsv.Count));
            Assert.IsTrue(fromTsv.All(e => e.GeneNames.Count == 0), name);
            Assert.IsTrue(fromParquet.Count(e => e.GeneNames.Count > 0) > fromParquet.Count / 2, name);
            return fromParquet;
        }

        /// <summary>
        /// The derived library of a .parquet source, as <see cref="DerivedLibraryDifferences"/>
        /// tests a .tsv's: written by the first <see cref="LibraryLoader"/> load at
        /// <c>&lt;library&gt;.parquet.blib</c>, read by the second, which gives the same library
        /// but for the gene names a .blib does not hold.
        /// </summary>
        private List<string> DerivedParquetLibraryDifferences(string dir, FragmentToleranceConfig tolerance)
        {
            string parquet = Path.Combine(dir, @"lib" + LibrarySource.EXT_PARQUET);
            string derived = Path.Combine(ArtifactPaths.ResolveCacheDir(parquet),
                Path.GetFileName(parquet) + LibraryLoader.DERIVED_LIBRARY_EXT);
            var firstLog = new StringBuilder();
            var first = LoadParquetLibrary(parquet, tolerance, firstLog);
            StringAssert.Contains(firstLog.ToString(), string.Format(
                OspreyIOResources.LibraryLoader_Load_Saved_the_derived_library___0__precursors__to___1__, first.Count, derived));
            Assert.IsTrue(File.Exists(derived), derived);
            var secondLog = new StringBuilder();
            var second = LoadParquetLibrary(parquet, tolerance, secondLog);
            StringAssert.Contains(secondLog.ToString(), string.Format(
                OspreyIOResources.LibraryLoader_Load_Loaded__0__library_precursors_from_the_derived_library___1__, first.Count, derived));
            // Gene names are not in a .blib, and no output reads them (LibraryLoader's derived write).
            var differences = LibraryDifferences(first, second).Where(line => !line.Contains(@": GeneNames")).ToList();
            return DescribeDifferences(@"lib.parquet (LibraryLoader, derived library)", differences, first.Count);
        }

        /// <summary>
        /// The empty library DIA-NN writes when a search identifies nothing (the full schema, no
        /// rows, one empty row group): no entries and no exception, from the loader and through
        /// <see cref="LibraryLoader"/>, with the log saying the file has no rows.
        /// </summary>
        private static void AssertEmptyParquetLibrary(string empty, FragmentToleranceConfig tolerance)
        {
            using (var reader = DiannLibraryParquetReader.Open(empty, DiannParquetLoader.REQUIRED_COLUMNS))
            {
                Assert.AreEqual(0, reader.RowCount);
                Assert.AreEqual(1, reader.RowGroupCount);
            }
            string noRows = string.Format(
                OspreyIOResources.DiannParquetLoader_Load_The_DIA_NN_library__0__has_no_rows__DIA_NN_writes_an_empty_library_when_its_search_identifies_nothing_,
                empty);
            var loaderLog = new StringBuilder();
            Assert.AreEqual(0, new DiannParquetLoader(tolerance).Load(empty, line => loaderLog.AppendLine(line)).Count);
            StringAssert.Contains(loaderLog.ToString(), noRows);
            var log = new StringBuilder();
            var entries = LoadParquetLibrary(empty, tolerance, log);
            Assert.AreEqual(0, entries.Count);
            StringAssert.Contains(log.ToString(), noRows);
        }

        /// <summary>
        /// Libraries the loader refuses, written here: one whose rows hold a precursor charge of
        /// 0 and an ion type that is no ion, refused whole with each row named, as a .tsv with
        /// such lines is (its RT written as double, a width the reader converts); and one
        /// without a required column.
        /// </summary>
        private static void AssertInvalidParquetLibraries(string dir, FragmentToleranceConfig tolerance)
        {
            string invalid = Path.Combine(dir, @"invalid-rows.parquet");
            WriteSmallLibrary(invalid, true);
            var refusal = Assert.ThrowsException<InvalidDataException>(() => new DiannParquetLoader(tolerance).Load(invalid));
            StringAssert.StartsWith(refusal.Message, string.Format(
                OspreyIOResources.DiannParquetLoader_Load__0__library_rows_have_errors__Fix_the_library_and_load_it_again_, 2));
            StringAssert.Contains(refusal.Message, string.Format(OspreyIOResources.DiannParquetLoader_AddRow___row__0___Invalid__1____2__,
                2, @"Precursor.Charge", 0));
            StringAssert.Contains(refusal.Message, string.Format(OspreyIOResources.DiannParquetLoader_AddRow___row__0___Invalid__1____2__,
                3, @"Fragment.Type", @"q"));

            string missing = Path.Combine(dir, @"missing-column.parquet");
            WriteSmallLibrary(missing, false);
            refusal = Assert.ThrowsException<InvalidDataException>(() => new DiannParquetLoader(tolerance).Load(missing));
            Assert.AreEqual(string.Format(OspreyIOResources.ColumnIndices_RequireColumn_Missing_required_column___0_,
                @"Relative.Intensity"), refusal.Message);
        }

        /// <summary>
        /// A four-row library of one precursor at <paramref name="path"/>, its second row with
        /// charge 0 and its third with ion type "q"; without Relative.Intensity unless
        /// <paramref name="withIntensity"/>.
        /// </summary>
        private static void WriteSmallLibrary(string path, bool withIntensity)
        {
            var modSeq = new DataField<string>(@"Modified.Sequence", false);
            var charge = new DataField<long>(@"Precursor.Charge");
            var precursorMz = new DataField<float>(@"Precursor.Mz");
            var rt = new DataField<double>(@"RT");
            var productMz = new DataField<float>(@"Product.Mz");
            var intensity = new DataField<float>(@"Relative.Intensity");
            var fragmentType = new DataField<string>(@"Fragment.Type", false);
            var fields = new List<Field> { modSeq, charge, precursorMz, rt, productMz, fragmentType };
            if (withIntensity)
                fields.Add(intensity);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var writer = ParquetWriter.CreateAsync(new ParquetSchema(fields), stream).GetAwaiter().GetResult();
                using (var group = writer.CreateRowGroup())
                {
                    group.WriteAsync(modSeq, Chars(@"PEPTIDEK", @"PEPTIDEK", @"PEPTIDEK", @"PEPTIDEK"))
                        .GetAwaiter().GetResult();
                    group.WriteAsync(charge, new ReadOnlyMemory<long>(new[] { 2L, 0L, 2L, 2L })).GetAwaiter().GetResult();
                    group.WriteAsync(precursorMz, new ReadOnlyMemory<float>(new[] { 465.7f, 465.7f, 465.7f, 465.7f }))
                        .GetAwaiter().GetResult();
                    group.WriteAsync(rt, new ReadOnlyMemory<double>(new[] { 10.0, 10.0, 10.0, 10.0 })).GetAwaiter().GetResult();
                    group.WriteAsync(productMz, new ReadOnlyMemory<float>(new[] { 147.1f, 263.1f, 376.2f, 477.3f }))
                        .GetAwaiter().GetResult();
                    group.WriteAsync(fragmentType, Chars(@"y", @"y", @"q", @"y"))
                        .GetAwaiter().GetResult();
                    if (withIntensity)
                    {
                        group.WriteAsync(intensity, new ReadOnlyMemory<float>(new[] { 1f, 0.5f, 0.4f, 0.3f }))
                            .GetAwaiter().GetResult();
                    }
                }
                writer.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>Strings as Parquet.Net writes a non-nullable string column.</summary>
        private static ReadOnlyMemory<ReadOnlyMemory<char>> Chars(params string[] values)
        {
            return values.Select(v => v.AsMemory()).ToArray();
        }

        private static List<LibraryEntry> LoadParquetLibrary(string parquet, FragmentToleranceConfig tolerance, StringBuilder log)
        {
            return LibraryLoader.Load(new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath(parquet),
                FragmentTolerance = tolerance,
            }, LibraryLoadOptions.Default, OspreyLog.FromDelegate(line => log.AppendLine(line)), line => log.AppendLine(line));
        }
    }
}
