# TODO: Compress vendor test data directories

**Status:** Backlog - **RECONSIDER BEFORE IMPLEMENTING**
**Priority:** Low
**Estimated Effort:** Small to Medium (1-2 days)

## ⚠️ Important Feedback from Matt Chambers (PR #3651)

**Historical Context:**
- Project previously used compressed test data
- Found it "annoying to update, maintain, and diff"
- Intentionally moved away from that approach

**Matt's Concerns:**
1. **Reference mzML files MUST stay uncompressed** - Need plaintext for diffing
2. **Vendor `.d` directories** - Maybe OK to compress, but questions if it's worth it
3. **Rare use case** - Only helps with "modify everything in repo" scripts (very unusual)
4. **Maintenance burden** - Historical experience shows compressed test data is harder to work with

**Key Quote:** "I would definitely want to keep doing that for reference mzMLs. The vendor data itself probably would be ok to put back in tarballs, but I'm not sure it's really necessary or useful except for 'modify everything in the repository' scripts like you've done here, which are exceedingly unusual."

**Recommendation:** Before implementing this TODO, carefully weigh:
- Historical problems with compressed test data
- Limited benefit (only helps rare bulk-modification scripts)
- Matt's strong preference to keep reference mzMLs as plaintext
- Alternative: Use `.gitattributes` to mark test data as binary (already done)

## Problem

Vendor data format test directories (`.d`, `.raw`, etc.) contain XML and other text files that have binary integrity requirements. These files are:
- Sensitive to encoding changes (BOM addition/removal changes SHA-1 hashes)
- Sensitive to line ending changes (\n vs \r\n changes byte offsets)
- Treated as "binary files masquerading as text" by developers
- Large and take up significant repository space

Currently, some test data like the Agilent `.d` directories contain XML files with UTF-8 BOMs that represent real instrument output and must not be modified. However, Git treats them as text files and they're vulnerable to inadvertent modification during bulk operations (like BOM removal).

## Current State

Test data is stored uncompressed in directories like:
- `pwiz/data/vendor_readers/Agilent/Reader_Agilent_Test.data/*.d/`
- `pwiz/data/vendor_readers/Waters/Reader_Waters_Test.data/*.raw/`
- Similar directories for other vendors

## Proposed Solution (IF Reconsidered)

**SCOPE CLARIFICATION based on Matt's feedback:**
- ✅ **Could compress**: Vendor `.d`/`.raw` directories (instrument data)
- ❌ **DO NOT compress**: Reference mzML files (need plaintext for diffing)
- ⚠️ **Question value**: Is preventing rare bulk-modification accidents worth the maintenance burden?

If proceeding, store vendor test data directories in compressed archives (.zip, .7z, or .gz):

1. **Compress test data**
   - One-time compression of existing `.d` and other vendor directories
   - Use .zip (widely supported) or .7z (better compression)
   - Expected compression: 80-90% size reduction for XML-heavy formats

2. **Update test harness**
   - Modify `VendorReaderTestHarness.cpp` to:
     - Detect if test data is compressed (check for .zip/.7z extension)
     - Extract to temp directory before running test
     - Run test on extracted data
     - Clean up temp directory after test
   - Handle extraction failures gracefully with clear error messages

3. **Update build system**
   - Ensure extraction tools available in CI environment
   - Add extraction step if needed for build-time test data generation

## Benefits

- **Binary integrity**: Compressed archives are explicitly binary, preventing accidental text modifications
- **Repository size**: Significant space savings (XML compresses extremely well)
- **Consistency**: Matches how vendor reader DLLs are stored in `pwiz_aux/msrc/utility`
- **Protection**: Prevents inadvertent BOM removal, line ending changes, or other text transformations
- **Git performance**: Fewer objects to track, faster clones/pulls

## Risks & Mitigation

| Risk | Mitigation |
|------|-----------|
| Slower tests (extraction overhead) | Extract once per test run, reuse for multiple tests |
| Extraction failure in CI | Clear error messages, verify extraction tools in CI setup |
| Developer workflow disruption | Document how to add/update test data |
| Debugging difficulty | Keep some uncompressed data for quick inspection, or add extraction utility |

## Implementation Plan

### Phase 1: Proof of Concept (4 hours)
1. Compress one `.d` directory (e.g., TOFsulfasMS4GHzDualMode test data)
2. Update VendorReaderTestHarness.cpp to handle extraction for that one test
3. Verify test passes with compressed data
4. Measure compression ratio and test time impact

### Phase 2: Full Implementation (1 day)
1. Compress all vendor test data directories
2. Update all vendor reader tests
3. Update documentation for adding new test data
4. Verify all tests pass on local machine

### Phase 3: CI Integration (2-4 hours)
1. Ensure CI environments have extraction tools
2. Run full test suite on CI
3. Monitor for any extraction-related failures
4. Document any CI-specific requirements

## Related

- Similar to how vendor reader DLLs are stored in `pwiz_aux/msrc/utility`
- Related to BOM standardization work (TODO-20251019_utf8_no_bom.md)
- May want to exclude compressed archives from certain Git operations

## Future Considerations

- Could use this approach for other large test data
- Could add utility script to easily extract/re-compress for debugging
- Could use Git LFS for very large compressed archives if needed
