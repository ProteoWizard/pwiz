using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportSpectrumFilterTransitionListTest : AbstractFunctionalTest
    {
        private readonly StringBuilder _diagnostics = new StringBuilder();

        [TestMethod]
        public void TestImportSpectrumFilterTransitionList()
        {
            TestFilesZipPaths = new []
            {
                @"TestFunctional\ImportSpectrumFilterTransitionListTest.data",
                @"TestFunctional\crv_qf_hsp_ms2_opt0.zip"

            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("BlankDocument.sky")));
            RunDlg<ImportTransitionListColumnSelectDlg>(
                () => SkylineWindow.ImportMassList(TestFilesDirs[0].GetTestPath("TransitionList.csv")),
                dlg =>
                {
                    dlg.OkDialog();
                });
            var rawFileName = TestFilesDirs[1].GetTestPath("crv_qf_hsp_ms2_opt0.raw");

            // Get document BEFORE import for blank doc
            var blankDocBeforeImport = SkylineWindow.Document;
            DiagnoseSpectrumFilter("BlankDoc BEFORE import", blankDocBeforeImport);

            ImportResultsFile(rawFileName);
            var blankDocumentLoaded = SkylineWindow.Document;
            DiagnoseSpectrumFilter("BlankDoc AFTER import", blankDocumentLoaded);
            DiagnoseCacheEntries("BlankDoc", blankDocumentLoaded, new MsDataFilePath(rawFileName));

            RunUI(()=>SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("WithTransitions.sky")));

            // Get document BEFORE import for WithTransitions
            var withTransitionsBeforeImport = SkylineWindow.Document;
            Log($"\n=== WithTransitions existing results ===");
            Log($"Has MeasuredResults: {withTransitionsBeforeImport.MeasuredResults != null}");
            if (withTransitionsBeforeImport.MeasuredResults != null)
            {
                Log($"Chromatogram sets: {withTransitionsBeforeImport.MeasuredResults.Chromatograms.Count}");
                Log($"Cache paths: {string.Join(", ", withTransitionsBeforeImport.MeasuredResults.CachePaths)}");
            }

            // Compare document settings
            Log($"\n=== Document Settings Comparison ===");
            Log($"BlankDoc AcquisitionMethod: {blankDocBeforeImport.Settings.TransitionSettings.FullScan.AcquisitionMethod}");
            Log($"WithTransitions AcquisitionMethod: {withTransitionsBeforeImport.Settings.TransitionSettings.FullScan.AcquisitionMethod}");
            Log($"BlankDoc IsolationScheme: {blankDocBeforeImport.Settings.TransitionSettings.FullScan.IsolationScheme?.Name ?? "(null)"}");
            Log($"WithTransitions IsolationScheme: {withTransitionsBeforeImport.Settings.TransitionSettings.FullScan.IsolationScheme?.Name ?? "(null)"}");

            DiagnoseSpectrumFilter("WithTransitions BEFORE import", withTransitionsBeforeImport);

            // Enable diagnostic logging for WithTransitions import via environment variable
            var diagLogPath = TestFilesDirs[0].GetTestPath("extraction_diagnostic.log");
            Environment.SetEnvironmentVariable(@"SKYLINE_CHROM_DIAGNOSTIC_LOG", diagLogPath);
            Log($"Diagnostic logging enabled at: {diagLogPath}");

            ImportResultsFile(rawFileName);

            // Close the diagnostic log using reflection and clear environment variable
            var skylineAssembly = System.Reflection.Assembly.GetAssembly(typeof(SrmDocument));
            var spectraChromDataProviderType = skylineAssembly?.GetType("pwiz.Skyline.Model.Results.SpectraChromDataProvider");
            var closeDiagLogMethod = spectraChromDataProviderType?.GetMethod("CloseDiagnosticLog",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            closeDiagLogMethod?.Invoke(null, null);
            Environment.SetEnvironmentVariable(@"SKYLINE_CHROM_DIAGNOSTIC_LOG", null);

            // Read and log diagnostic output
            if (File.Exists(diagLogPath))
            {
                var diagLines = File.ReadAllLines(diagLogPath);
                Log($"\n=== Extraction Diagnostic Log ({diagLines.Length} lines) ===");
                var processLines = diagLines.Where(l => l.StartsWith("ProcessExtractedSpectrum")).ToList();
                var addLines = diagLines.Where(l => l.StartsWith("AddChromatogramsForFilterPair")).ToList();
                Log($"ProcessExtractedSpectrum calls for HCD CE=15: {processLines.Count}");
                Log($"AddChromatogramsForFilterPair calls for HCD CE=15: {addLines.Count}");

                if (processLines.Count > 0)
                {
                    Log($"First 5 ProcessExtractedSpectrum:");
                    foreach (var line in processLines.Take(5))
                        Log($"  {line}");
                }

                if (addLines.Count > 0)
                {
                    Log($"First 5 AddChromatogramsForFilterPair:");
                    foreach (var line in addLines.Take(5))
                        Log($"  {line}");

                    var skippedLines = addLines.Where(l => l.Contains("Skipped=True")).ToList();
                    Log($"Skipped filter pairs: {skippedLines.Count}");
                    if (skippedLines.Count > 0)
                    {
                        Log($"First 5 Skipped:");
                        foreach (var line in skippedLines.Take(5))
                            Log($"  {line}");
                    }
                }
            }
            else
            {
                Log("WARNING: Diagnostic log file not found!");
            }

            var otherDocument = SkylineWindow.Document;
            DiagnoseSpectrumFilter("WithTransitions AFTER import", otherDocument);
            DiagnoseCacheEntries("WithTransitions", otherDocument, new MsDataFilePath(rawFileName));

            // Compare SpectrumClassFilters between documents
            CompareSpectrumClassFilters(blankDocumentLoaded, otherDocument);

            // Compare SpectrumFilters (extraction configuration) BEFORE import
            CompareSpectrumFilters(blankDocBeforeImport, withTransitionsBeforeImport, rawFileName);

            // Test predicates directly against actual spectrum metadata
            TestPredicatesWithRawFile(rawFileName, blankDocumentLoaded, otherDocument);

            var missingChromatograms =
                FindMissingChromatograms(blankDocumentLoaded, otherDocument, new MsDataFilePath(rawFileName)).ToList();

            // Include diagnostics in the assertion message
            var message = _diagnostics.ToString() + "\n\nMissing chromatograms:\n" +
                TextUtil.LineSeparate(missingChromatograms.Take(10).Select(tuple =>
                    TextUtil.SpaceSeparate(tuple.Item1.ModifiedSequence + " " + tuple.Item2.SpectrumClassFilter)));
            Assert.AreEqual(0, missingChromatograms.Count, message);
        }

        private void Log(string text)
        {
            _diagnostics.AppendLine(text);
        }

        private void DiagnoseSpectrumFilter(string label, SrmDocument document)
        {
            var spectrumFilter = new SpectrumFilter(document);
            var filterPairs = spectrumFilter.FilterPairs.ToList();

            var hcdPairs = filterPairs.Where(fp => fp.SpectrumClassFilter.ToString().Contains("HCD")).ToList();
            var cidPairs = filterPairs.Where(fp => fp.SpectrumClassFilter.ToString().Contains("CID")).ToList();
            var emptyPairs = filterPairs.Where(fp => fp.SpectrumClassFilter.IsEmpty).ToList();

            Log($"\n=== {label} ===");
            Log($"Total filter pairs: {filterPairs.Count}");
            Log($"HCD filter pairs: {hcdPairs.Count}");
            Log($"CID filter pairs: {cidPairs.Count}");
            Log($"Empty filter pairs: {emptyPairs.Count}");

            // Show first few HCD filter pairs with their IDs
            if (hcdPairs.Any())
            {
                Log($"First 5 HCD filter pairs:");
                foreach (var fp in hcdPairs.Take(5))
                {
                    Log($"  Id={fp.Id}, Q1={fp.Q1}, Filter={fp.SpectrumClassFilter}");
                }
            }

            // Check document transition groups
            var hcdGroups = document.MoleculeTransitionGroups
                .Where(g => g.SpectrumClassFilter.ToString().Contains("HCD")).ToList();
            var cidGroups = document.MoleculeTransitionGroups
                .Where(g => g.SpectrumClassFilter.ToString().Contains("CID")).ToList();

            Log($"HCD transition groups in document: {hcdGroups.Count}");
            Log($"CID transition groups in document: {cidGroups.Count}");

            // Show first few HCD transition groups with their filters
            if (hcdGroups.Any())
            {
                Log($"First 3 HCD transition groups:");
                foreach (var g in hcdGroups.Take(3))
                {
                    Log($"  Mz={g.PrecursorMz:F4}, Filter={g.SpectrumClassFilter}");
                }
            }
        }

        private void DiagnoseCacheEntries(string label, SrmDocument document, MsDataFileUri filePath)
        {
            if (document.MeasuredResults == null)
            {
                Log($"\n=== {label} CACHE: No measured results ===");
                return;
            }

            var chromSetFileMatch = document.MeasuredResults.FindMatchingMSDataFile(filePath);
            if (chromSetFileMatch.Chromatograms == null)
            {
                Log($"\n=== {label} CACHE: No chromatograms ===");
                return;
            }

            Log($"\n=== {label} CACHE ===");

            // Try to load chromatograms for HCD transition groups
            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            int hcdLoaded = 0;
            int hcdMissing = 0;
            int cidLoaded = 0;
            int cidMissing = 0;

            string firstHcdMissing = null;
            string firstHcdLoaded = null;

            foreach (var molecule in document.Molecules)
            {
                foreach (var group in molecule.TransitionGroups)
                {
                    bool isHcd = group.SpectrumClassFilter.ToString().Contains("HCD");
                    bool isCid = group.SpectrumClassFilter.ToString().Contains("CID");

                    bool loaded = document.MeasuredResults.TryLoadChromatogram(
                        chromSetFileMatch.Chromatograms, molecule, group, tolerance, out var chromatograms);
                    bool hasChromatograms = loaded && chromatograms != null && chromatograms.Length > 0;

                    if (isHcd)
                    {
                        if (hasChromatograms)
                        {
                            hcdLoaded++;
                            if (firstHcdLoaded == null)
                            {
                                var chromGroupId = ChromatogramGroupId.ForPeptide(molecule, group);
                                firstHcdLoaded = $"Mz={group.PrecursorMz:F4}, Filter={group.SpectrumClassFilter}, GroupId.Target={chromGroupId.Target}";
                            }
                        }
                        else
                        {
                            hcdMissing++;
                            if (firstHcdMissing == null)
                            {
                                var chromGroupId = ChromatogramGroupId.ForPeptide(molecule, group);
                                firstHcdMissing = $"Mz={group.PrecursorMz:F4}, Filter={group.SpectrumClassFilter}, GroupId.Target={chromGroupId.Target}";
                            }
                        }
                    }
                    else if (isCid)
                    {
                        if (hasChromatograms) cidLoaded++;
                        else cidMissing++;
                    }
                }
            }

            Log($"HCD chromatograms loaded: {hcdLoaded}, missing: {hcdMissing}");
            Log($"CID chromatograms loaded: {cidLoaded}, missing: {cidMissing}");
            if (firstHcdLoaded != null)
                Log($"First HCD loaded: {firstHcdLoaded}");
            if (firstHcdMissing != null)
                Log($"First HCD missing: {firstHcdMissing}");

            // Also directly inspect cache entries to see what's stored
            DiagnoseCacheEntriesDirect(label, document);
        }

        private void DiagnoseCacheEntriesDirect(string label, SrmDocument document)
        {
            try
            {
                Log($"\n=== {label} CACHE ENTRIES DIRECT - ENTERING METHOD ===");

                if (document.MeasuredResults == null)
                {
                    Log($"  MeasuredResults is null, returning");
                    return;
                }

                Log($"  MeasuredResults exists, CacheFinal is null: {document.MeasuredResults.CacheFinal == null}");

                // If we have CacheFinal, dump its entries
                var cache = document.MeasuredResults.CacheFinal;
                if (cache == null)
                {
                    Log("CacheFinal is null, cannot inspect cache entries directly");
                    return;
                }

                {
                Log($"Cache entry count: {cache.ChromGroupHeaderInfos.Count}");

                int withHcdFilter = 0;
                int withCidFilter = 0;
                int withNullFilter = 0;
                int withEmptyFilter = 0;

                var firstHcdEntry = (ChromGroupHeaderInfo?)null;
                var firstCidEntry = (ChromGroupHeaderInfo?)null;
                var firstNullEntry = (ChromGroupHeaderInfo?)null;

                for (int i = 0; i < cache.ChromGroupHeaderInfos.Count; i++)
                {
                    var headerInfo = cache.ChromGroupHeaderInfos[i];
                    var groupId = cache.GetChromatogramGroupId(headerInfo);

                    if (groupId == null)
                    {
                        withNullFilter++;
                        if (!firstNullEntry.HasValue) firstNullEntry = headerInfo;
                    }
                    else if (groupId.SpectrumClassFilter.IsEmpty)
                    {
                        withEmptyFilter++;
                    }
                    else if (groupId.SpectrumClassFilter.ToString().Contains("HCD"))
                    {
                        withHcdFilter++;
                        if (!firstHcdEntry.HasValue) firstHcdEntry = headerInfo;
                    }
                    else if (groupId.SpectrumClassFilter.ToString().Contains("CID"))
                    {
                        withCidFilter++;
                        if (!firstCidEntry.HasValue) firstCidEntry = headerInfo;
                    }
                }

                Log($"Cache entries with HCD filter: {withHcdFilter}");
                Log($"Cache entries with CID filter: {withCidFilter}");
                Log($"Cache entries with null groupId: {withNullFilter}");
                Log($"Cache entries with empty filter: {withEmptyFilter}");

                if (firstHcdEntry.HasValue)
                {
                    var groupId = cache.GetChromatogramGroupId(firstHcdEntry.Value);
                    Log($"First HCD entry: Precursor={firstHcdEntry.Value.Precursor:F4}, Target={groupId?.Target}, Filter={groupId?.SpectrumClassFilter}");
                }
                if (firstCidEntry.HasValue)
                {
                    var groupId = cache.GetChromatogramGroupId(firstCidEntry.Value);
                    Log($"First CID entry: Precursor={firstCidEntry.Value.Precursor:F4}, Target={groupId?.Target}, Filter={groupId?.SpectrumClassFilter}");
                }
                if (firstNullEntry.HasValue)
                {
                    Log($"First null groupId entry: Precursor={firstNullEntry.Value.Precursor:F4}");
                }

                // Show first 10 entries regardless of filter
                Log($"First 10 cache entries:");
                for (int i = 0; i < Math.Min(10, cache.ChromGroupHeaderInfos.Count); i++)
                {
                    var headerInfo = cache.ChromGroupHeaderInfos[i];
                    var groupId = cache.GetChromatogramGroupId(headerInfo);
                    var filterStr = groupId?.SpectrumClassFilter.IsEmpty == false
                        ? groupId.SpectrumClassFilter.ToString()
                        : (groupId == null ? "(null groupId)" : "(empty filter)");
                    Log($"  [{i}] Precursor={headerInfo.Precursor:F4}, Target={groupId?.Target}, Filter={filterStr}");
                }
            }
            }
            catch (Exception ex)
            {
                Log($"Exception in DiagnoseCacheEntriesDirect: {ex.Message}");
            }
        }

        private void CompareSpectrumFilters(SrmDocument doc1, SrmDocument doc2, string rawFile)
        {
            Log("\n=== COMPARING SpectrumFilters (extraction configuration) ===");

            var filter1 = new SpectrumFilter(doc1);
            var filter2 = new SpectrumFilter(doc2);

            // Check MaxTime for filter pairs
            var pairs1 = filter1.FilterPairs.ToList();
            var pairs2 = filter2.FilterPairs.ToList();

            var hcdPairs1 = pairs1.Where(p => p.SpectrumClassFilter.ToString().Contains("HCD") &&
                                              p.SpectrumClassFilter.ToString().Contains("15")).ToList();
            var hcdPairs2 = pairs2.Where(p => p.SpectrumClassFilter.ToString().Contains("HCD") &&
                                              p.SpectrumClassFilter.ToString().Contains("15")).ToList();
            var cidPairs1 = pairs1.Where(p => p.SpectrumClassFilter.ToString().Contains("CID") &&
                                              p.SpectrumClassFilter.ToString().Contains("20")).ToList();
            var cidPairs2 = pairs2.Where(p => p.SpectrumClassFilter.ToString().Contains("CID") &&
                                              p.SpectrumClassFilter.ToString().Contains("20")).ToList();

            Log($"Doc1 filter pairs: {pairs1.Count}");
            Log($"Doc2 filter pairs: {pairs2.Count}");
            Log($"hcdPairs1 count: {hcdPairs1.Count}, cidPairs1 count: {cidPairs1.Count}");
            Log($"hcdPairs2 count: {hcdPairs2.Count}, cidPairs2 count: {cidPairs2.Count}");

            if (hcdPairs1.Count > 0)
                Log($"Doc1 HCD CE=15 MaxTime (first 3): {string.Join(", ", hcdPairs1.Take(3).Select(p => p.MaxTime?.ToString() ?? "null"))}");
            if (cidPairs1.Count > 0)
                Log($"Doc1 CID CE=20 MaxTime (first 3): {string.Join(", ", cidPairs1.Take(3).Select(p => p.MaxTime?.ToString() ?? "null"))}");
            if (hcdPairs2.Count > 0)
                Log($"Doc2 HCD CE=15 MaxTime (first 3): {string.Join(", ", hcdPairs2.Take(3).Select(p => p.MaxTime?.ToString() ?? "null"))}");
            if (cidPairs2.Count > 0)
                Log($"Doc2 CID CE=20 MaxTime (first 3): {string.Join(", ", cidPairs2.Take(3).Select(p => p.MaxTime?.ToString() ?? "null"))}");

            // Group by Q1 and check HCD filter pair ordering
            var hcd1ByQ1 = pairs1.Where(p => p.SpectrumClassFilter.ToString().Contains("HCD") &&
                                              p.SpectrumClassFilter.ToString().Contains("Energy=15"))
                                 .GroupBy(p => p.Q1.Value)
                                 .OrderBy(g => g.Key)
                                 .ToList();
            var hcd2ByQ1 = pairs2.Where(p => p.SpectrumClassFilter.ToString().Contains("HCD") &&
                                              p.SpectrumClassFilter.ToString().Contains("Energy=15"))
                                 .GroupBy(p => p.Q1.Value)
                                 .OrderBy(g => g.Key)
                                 .ToList();

            Log($"Doc1 Q1 values with HCD CE=15: {hcd1ByQ1.Count}");
            Log($"Doc2 Q1 values with HCD CE=15: {hcd2ByQ1.Count}");

            if (hcd1ByQ1.Any() && hcd2ByQ1.Any())
            {
                var first1 = hcd1ByQ1.First().First();
                var first2 = hcd2ByQ1.First().First();

                Log($"Doc1 first HCD CE=15: Q1={first1.Q1}, Id={first1.Id}, ChromGroupId={first1.ChromatogramGroupId}");
                Log($"Doc2 first HCD CE=15: Q1={first2.Q1}, Id={first2.Id}, ChromGroupId={first2.ChromatogramGroupId}");

                // Check if ChromatogramGroupIds are equal
                Log($"ChromatogramGroupIds equal: {Equals(first1.ChromatogramGroupId, first2.ChromatogramGroupId)}");

                // Check SpectrumClassFilters
                Log($"Doc1 SpectrumClassFilter: {first1.SpectrumClassFilter}");
                Log($"Doc2 SpectrumClassFilter: {first2.SpectrumClassFilter}");
                Log($"SpectrumClassFilters equal: {Equals(first1.SpectrumClassFilter, first2.SpectrumClassFilter)}");
            }

            // Show distribution of filter pair IDs for HCD CE=15
            var hcd1Ids = pairs1.Where(p => p.SpectrumClassFilter.ToString().Contains("HCD") &&
                                            p.SpectrumClassFilter.ToString().Contains("Energy=15"))
                                .Select(p => p.Id)
                                .OrderBy(id => id)
                                .ToList();
            var hcd2Ids = pairs2.Where(p => p.SpectrumClassFilter.ToString().Contains("HCD") &&
                                            p.SpectrumClassFilter.ToString().Contains("Energy=15"))
                                .Select(p => p.Id)
                                .OrderBy(id => id)
                                .ToList();

            if (hcd1Ids.Count >= 5)
            {
                var idGaps1 = Enumerable.Range(1, 4).Select(i => hcd1Ids[i] - hcd1Ids[i - 1]).ToList();
                Log($"Doc1 HCD CE=15 ID gaps (first 4): {string.Join(", ", idGaps1)}");
            }
            if (hcd2Ids.Count >= 5)
            {
                var idGaps2 = Enumerable.Range(1, 4).Select(i => hcd2Ids[i] - hcd2Ids[i - 1]).ToList();
                Log($"Doc2 HCD CE=15 ID gaps (first 4): {string.Join(", ", idGaps2)}");
            }

            // Show raw array ordering around the different positions WITH IDs
            Log($"\nDoc1 array entries around position 56:");
            for (int i = Math.Max(0, 50); i < Math.Min(pairs1.Count, 62); i++)
            {
                var p = pairs1[i];
                Log($"  [{i}] Id={p.Id}, Q1={p.Q1:F4}, Filter={p.SpectrumClassFilter}");
            }
            Log($"\nDoc2 array entries around position 56:");
            for (int i = Math.Max(0, 50); i < Math.Min(pairs2.Count, 62); i++)
            {
                var p = pairs2[i];
                Log($"  [{i}] Id={p.Id}, Q1={p.Q1:F4}, Filter={p.SpectrumClassFilter}");
            }
            Log($"\nDoc1 array entries around position 565:");
            for (int i = Math.Max(0, 560); i < Math.Min(pairs1.Count, 572); i++)
            {
                var p = pairs1[i];
                Log($"  [{i}] Id={p.Id}, Q1={p.Q1:F4}, Filter={p.SpectrumClassFilter}");
            }

            // Show HCD CE=15 IDs
            Log($"\nDoc1 HCD CE=15 IDs (first 5): {string.Join(", ", hcd1Ids.Take(5))}");
            Log($"Doc2 HCD CE=15 IDs (first 5): {string.Join(", ", hcd2Ids.Take(5))}");
        }

        private void CompareSpectrumClassFilters(SrmDocument doc1, SrmDocument doc2)
        {
            Log("\n=== COMPARING SpectrumClassFilters ===");

            var groups1 = doc1.MoleculeTransitionGroups.ToList();
            var groups2 = doc2.MoleculeTransitionGroups.ToList();

            // Group by modified sequence
            var lookup1 = groups1.ToLookup(g => g.TransitionGroup.Peptide.Target.Sequence);
            var lookup2 = groups2.ToLookup(g => g.TransitionGroup.Peptide.Target.Sequence);

            int matches = 0;
            int filterDifferences = 0;

            // Check first few HCD groups in detail
            var hcdGroups1 = groups1.Where(g => g.SpectrumClassFilter.ToString().Contains("HCD")).Take(5).ToList();
            foreach (var g1 in hcdGroups1)
            {
                var seq = g1.TransitionGroup.Peptide.Target.Sequence;
                var g2Match = lookup2[seq]
                    .FirstOrDefault(g2 => Math.Abs(g1.PrecursorMz - g2.PrecursorMz) < 0.001 &&
                                          g1.SpectrumClassFilter.ToString() == g2.SpectrumClassFilter.ToString());

                if (g2Match != null)
                {
                    // Check if filters are actually equal
                    bool filtersEqual = Equals(g1.SpectrumClassFilter, g2Match.SpectrumClassFilter);
                    Log($"Sequence: {seq}");
                    Log($"  Doc1 filter: {g1.SpectrumClassFilter} (hashCode={g1.SpectrumClassFilter.GetHashCode()})");
                    Log($"  Doc2 filter: {g2Match.SpectrumClassFilter} (hashCode={g2Match.SpectrumClassFilter.GetHashCode()})");
                    Log($"  Filters equal: {filtersEqual}");

                    // Examine the filter clauses in detail
                    var clauses1 = g1.SpectrumClassFilter.Clauses.ToList();
                    var clauses2 = g2Match.SpectrumClassFilter.Clauses.ToList();
                    Log($"  Clause count: doc1={clauses1.Count}, doc2={clauses2.Count}");

                    for (int i = 0; i < Math.Max(clauses1.Count, clauses2.Count); i++)
                    {
                        var c1 = i < clauses1.Count ? clauses1[i] : null;
                        var c2 = i < clauses2.Count ? clauses2[i] : null;
                        Log($"  Clause[{i}]: doc1={c1}, doc2={c2}, equal={Equals(c1, c2)}");

                        if (c1 != null && c2 != null)
                        {
                            var specs1 = c1.FilterSpecs.ToList();
                            var specs2 = c2.FilterSpecs.ToList();
                            for (int j = 0; j < Math.Max(specs1.Count, specs2.Count); j++)
                            {
                                var s1 = j < specs1.Count ? specs1[j] : null;
                                var s2 = j < specs2.Count ? specs2[j] : null;
                                Log($"    FilterSpec[{j}]: column1={s1?.Column}, column2={s2?.Column}");
                                Log($"      op1={s1?.Operation}, op2={s2?.Operation}");
                                Log($"      operand1={s1?.Predicate?.InvariantOperandText}, operand2={s2?.Predicate?.InvariantOperandText}");
                            }
                        }
                    }

                    if (filtersEqual) matches++;
                    else filterDifferences++;
                }
            }

            Log($"Total matches: {matches}, differences: {filterDifferences}");
        }

        private void TestPredicatesWithRawFile(string rawFilePath, SrmDocument doc1, SrmDocument doc2)
        {
            Log("\n=== TESTING PREDICATES ===");

            // Get HCD filter from both documents
            var hcdGroup1 = doc1.MoleculeTransitionGroups
                .FirstOrDefault(g => g.SpectrumClassFilter.ToString().Contains("HCD") &&
                                     g.SpectrumClassFilter.ToString().Contains("15"));
            var hcdGroup2 = doc2.MoleculeTransitionGroups
                .FirstOrDefault(g => g.SpectrumClassFilter.ToString().Contains("HCD") &&
                                     g.SpectrumClassFilter.ToString().Contains("15"));

            if (hcdGroup1 == null || hcdGroup2 == null)
            {
                Log("Could not find HCD CE=15 transition groups");
                return;
            }

            var predicate1 = hcdGroup1.SpectrumClassFilter.MakePredicate();
            var predicate2 = hcdGroup2.SpectrumClassFilter.MakePredicate();

            // Create a mock HCD CE=15 spectrum metadata
            var hcdPrecursor = new SpectrumPrecursor(new SignedMz(500.0))
                .ChangeDissociationMethod("HCD")
                .ChangeCollisionEnergy(15.0);
            var hcdMetadata = new SpectrumMetadata("test_hcd", 1.0)
                .ChangePrecursors(new[] { new[] { hcdPrecursor } });

            // Create a mock CID CE=20 spectrum metadata
            var cidPrecursor = new SpectrumPrecursor(new SignedMz(500.0))
                .ChangeDissociationMethod("CID")
                .ChangeCollisionEnergy(20.0);
            var cidMetadata = new SpectrumMetadata("test_cid", 1.0)
                .ChangePrecursors(new[] { new[] { cidPrecursor } });

            Log($"Testing HCD CE=15 metadata against predicates:");
            Log($"  HCD MsLevel from metadata: {hcdMetadata.MsLevel}");
            Log($"  HCD Precursor DissociationMethod: {hcdPrecursor.DissociationMethod}");
            Log($"  HCD Precursor CollisionEnergy: {hcdPrecursor.CollisionEnergy}");

            bool match1Hcd = predicate1(hcdMetadata);
            bool match2Hcd = predicate2(hcdMetadata);
            Log($"  Doc1 predicate matches HCD: {match1Hcd}");
            Log($"  Doc2 predicate matches HCD: {match2Hcd}");

            Log($"Testing CID CE=20 metadata against predicates:");
            bool match1Cid = predicate1(cidMetadata);
            bool match2Cid = predicate2(cidMetadata);
            Log($"  Doc1 predicate matches CID: {match1Cid}");
            Log($"  Doc2 predicate matches CID: {match2Cid}");

            // Also get and test CID filter
            var cidGroup1 = doc1.MoleculeTransitionGroups
                .FirstOrDefault(g => g.SpectrumClassFilter.ToString().Contains("CID"));
            var cidGroup2 = doc2.MoleculeTransitionGroups
                .FirstOrDefault(g => g.SpectrumClassFilter.ToString().Contains("CID"));

            if (cidGroup1 != null && cidGroup2 != null)
            {
                var cidPredicate1 = cidGroup1.SpectrumClassFilter.MakePredicate();
                var cidPredicate2 = cidGroup2.SpectrumClassFilter.MakePredicate();

                Log($"Testing CID predicates:");
                Log($"  Doc1 CID filter: {cidGroup1.SpectrumClassFilter}");
                Log($"  Doc2 CID filter: {cidGroup2.SpectrumClassFilter}");
                Log($"  Doc1 CID predicate matches CID: {cidPredicate1(cidMetadata)}");
                Log($"  Doc2 CID predicate matches CID: {cidPredicate2(cidMetadata)}");
            }
        }

        private IEnumerable<(PeptideDocNode, TransitionGroupDocNode)> FindMissingChromatograms(SrmDocument expected, SrmDocument actual, MsDataFileUri filePath)
        {
            var chromSetFileMatchExpected = expected.MeasuredResults.FindMatchingMSDataFile(filePath);
            var chromSetFileMatchActual = actual.MeasuredResults.FindMatchingMSDataFile(filePath);
            float tolerance = (float) expected.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var actualMolecules = actual.Molecules.ToLookup(molecule => molecule.ModifiedSequence);
            foreach (var expectedMolecule in expected.Molecules)
            {
                foreach (var expectedTransitionGroup in expectedMolecule.TransitionGroups)
                {
                    Assert.IsTrue(FindMatchingTransitionGroup(expectedTransitionGroup, actualMolecules[expectedMolecule.ModifiedSequence], out var actualMolecule, out var actualTransitionGroup));
                    if (!expected.MeasuredResults.TryLoadChromatogram(
                            chromSetFileMatchExpected.Chromatograms, expectedMolecule,
                            expectedTransitionGroup, tolerance, out var expectedChromatograms) ||
                        expectedChromatograms.Length == 0)
                    {
                        continue;
                    }

                    if (!actual.MeasuredResults.TryLoadChromatogram(chromSetFileMatchActual.Chromatograms,
                            actualMolecule, actualTransitionGroup, tolerance, out var actualChromatograms) || actualChromatograms.Length == 0)
                    {
                        yield return (actualMolecule, actualTransitionGroup);
                    }
                }
            }
        }

        private bool FindMatchingTransitionGroup(TransitionGroupDocNode transitionGroupDocNode,
            IEnumerable<PeptideDocNode> candidateMolecules, out PeptideDocNode matchingMolecule,
            out TransitionGroupDocNode matchingTransitionGroup)
        {
            foreach (var candidateMolecule in candidateMolecules)
            {
                foreach (var candidateTransitionGroup in candidateMolecule.TransitionGroups)
                {
                    if (Equals(candidateTransitionGroup.SpectrumClassFilter,
                            transitionGroupDocNode.SpectrumClassFilter))
                    {
                        matchingMolecule = candidateMolecule;
                        matchingTransitionGroup = candidateTransitionGroup;
                        return true;
                    }
                }
            }
            matchingMolecule = null;
            matchingTransitionGroup = null;
            return false;
        }

        
    }
}
