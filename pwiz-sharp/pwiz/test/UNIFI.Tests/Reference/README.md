pwiz-sharp UNIFI reference override
====================================

The `Reader_UNIFI_HarnessAgainstReferenceUrls` test prefers the mzML files in this
directory over the canonical cpp fixtures at
`pwiz/data/vendor_readers/UNIFI/Reader_UNIFI_Test.data/`.

**Why a separate directory?** Editing files in the cpp tree retriggers every cpp
vendor TeamCity config. Pinning pwiz-sharp-specific reference output here keeps the
TC blast radius limited to pwiz-sharp's own configs while we ratchet the C# port
toward bit-identical parity with cpp's output.

**When to add a file here**: when you've changed the pwiz-sharp reader output in a
way that's correct against cpp's *current* behavior but doesn't match the cpp
reference mzML on disk (most often: cpp reference mzML predates a cpp code change
and the C# port matches the newer code, not the older reference).

**When to remove a file**: when the cpp reference mzML in the cpp tree is regenerated
to match — this directory should empty out as the alignment work completes.

**To regenerate**: flip the local `IsRecordMode` bool inside the
`Reader_UNIFI_HarnessAgainstReferenceUrls` test, run the test, then revert before
committing. Files are always written here; the cpp tree is never touched.

Two safety nets catch a forgotten revert: an `Assert.IsFalse(IsRecordMode, …)` at
the end of the harness test itself, and `Util.Tests/CodeInspectionTests`, which
greps every `.cs` file in the pwiz-sharp tree (the `IsRecordMode_NeverCommittedTrue`
rule). The latter runs in CI on every build, so accidental record-mode commits fail
the build regardless of which other tests ran.
