using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            var rawFileName = TestFilesDirs[1].GetTestPath("crv_qf_hsp_ms2_opt0.raw");

            // ONLY import WithTransitions.sky - test if it extracts HCD chromatograms correctly
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("WithTransitions.sky")));

            // Diagnostic: Check transition groups in WithTransitions BEFORE import
            var docBeforeImport = SkylineWindow.Document;
            int hcdTransitionGroupsBefore = docBeforeImport.MoleculeTransitionGroups
                .Count(tg => tg.SpectrumClassFilter.ToString().Contains("HCD"));
            int emptyFilterGroupsBefore = docBeforeImport.MoleculeTransitionGroups
                .Count(tg => tg.SpectrumClassFilter.IsEmpty);

            ImportResultsFile(rawFileName);
            var docAfterImport = SkylineWindow.Document;

            // Diagnostic: Check transition groups AFTER import
            int hcdTransitionGroupsAfter = docAfterImport.MoleculeTransitionGroups
                .Count(tg => tg.SpectrumClassFilter.ToString().Contains("HCD"));
            int emptyFilterGroupsAfter = docAfterImport.MoleculeTransitionGroups
                .Count(tg => tg.SpectrumClassFilter.IsEmpty);

            // Diagnostic output
            var diagnosticLines = new List<string>();

            var cache = docAfterImport.MeasuredResults.CacheFinal;

            // Count chromatogram entries with HCD-related ChromatogramGroupIds
            int hcdCount = 0;
            int emptyFilterCount = 0;

            // Collect distinct SpectrumClassFilter values in cache
            var cacheFilters = new HashSet<string>();

            foreach (var entry in cache.ChromGroupHeaderInfos)
            {
                var chromGroupId = cache.GetChromatogramGroupId(entry);
                var filterStr = chromGroupId?.SpectrumClassFilter.ToString() ?? "(null)";
                cacheFilters.Add(filterStr);
                if (chromGroupId?.SpectrumClassFilter.ToString().Contains("HCD") == true)
                    hcdCount++;
                if (chromGroupId?.SpectrumClassFilter.IsEmpty == true || chromGroupId == null)
                    emptyFilterCount++;
            }

            // Count HCD ChromatogramGroupIds in document
            var hcdChromIds = new HashSet<string>();
            foreach (var mol in docAfterImport.Molecules)
            {
                foreach (var tg in mol.TransitionGroups)
                {
                    var chromId = ChromatogramGroupId.ForPeptide(mol, tg);
                    if (chromId?.SpectrumClassFilter.ToString().Contains("HCD") == true)
                        hcdChromIds.Add($"{chromId.Target}|{chromId.SpectrumClassFilter}");
                }
            }

            // Create SpectrumFilter to see filter pairs
            var spectrumFilter = new SpectrumFilter(docAfterImport);
            var filterPairs = spectrumFilter.FilterPairs.ToList();

            int hcdFilterPairs = filterPairs.Count(fp => !fp.SpectrumClassFilter.IsEmpty &&
                fp.SpectrumClassFilter.ToString().Contains("HCD"));
            int emptyFilterPairs = filterPairs.Count(fp => fp.SpectrumClassFilter.IsEmpty);

            // Check if HCD filter pairs have CollisionEnergy set (which could cause mismatch)
            var hcdFilterPairsWithCE = filterPairs
                .Where(fp => fp.SpectrumClassFilter.ToString().Contains("HCD"))
                .ToList();
            int hcdWithOptStep = hcdFilterPairsWithCE.Count(fp => fp.OptStep.HasValue);

            // Get unique OptStep values for HCD filter pairs
            var hcdOptSteps = hcdFilterPairsWithCE.Select(fp => fp.OptStep?.ToString() ?? "null").Distinct().ToList();

            diagnosticLines.Add("=== SPECTRUM FILTER PAIRS ===");
            diagnosticLines.Add($"Total filter pairs: {filterPairs.Count}");
            diagnosticLines.Add($"HCD filter pairs: {hcdFilterPairs}");
            diagnosticLines.Add($"Empty filter pairs: {emptyFilterPairs}");
            diagnosticLines.Add($"HCD filter pairs with OptStep: {hcdWithOptStep}");
            diagnosticLines.Add($"HCD OptStep values: {string.Join(", ", hcdOptSteps)}");
            diagnosticLines.Add("");
            diagnosticLines.Add("=== CACHE ENTRIES ===");
            diagnosticLines.Add($"Total cache entries: {cache.ChromGroupHeaderInfos.Count}");
            diagnosticLines.Add($"HCD cache entries: {hcdCount}");
            diagnosticLines.Add($"Empty filter cache entries: {emptyFilterCount}");
            diagnosticLines.Add("");
            diagnosticLines.Add("=== DISTINCT SPECTRUM FILTERS IN CACHE ===");
            diagnosticLines.Add($"Cache distinct filters: {string.Join(", ", cacheFilters.OrderBy(s => s))}");
            diagnosticLines.Add("");
            diagnosticLines.Add("=== DOCUMENT ===");
            diagnosticLines.Add($"Unique HCD ChromatogramGroupIds: {hcdChromIds.Count}");
            diagnosticLines.Add($"HCD transition groups BEFORE import: {hcdTransitionGroupsBefore}");
            diagnosticLines.Add($"Empty filter groups BEFORE import: {emptyFilterGroupsBefore}");
            diagnosticLines.Add($"HCD transition groups AFTER import: {hcdTransitionGroupsAfter}");
            diagnosticLines.Add($"Empty filter groups AFTER import: {emptyFilterGroupsAfter}");

            var message = TextUtil.LineSeparate(
                $"Expected HCD cache entries, found {hcdCount}",
                "",
                TextUtil.LineSeparate(diagnosticLines));

            // The raw file should have 99 peptides with HCD data, so we expect at least 99 HCD cache entries
            Assert.IsTrue(hcdCount > 0, message);
        }
    }
}
