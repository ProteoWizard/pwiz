pwiz-sharp Bruker reference override
====================================

`VendorReaderTestHarness` prefers the mzML files in this directory over the cpp-tree
fixtures extracted from
`pwiz/data/vendor_readers/Bruker/Reader_Bruker_Test.data.tar.bz2`
(it checks `<bin>/Reference/<filename>` first, then falls back to the cpp tree).

**Why these files are here.** The Bruker reader emits the canonical C++ combined-IMS
native id `merged=N frame=F scanStart=S scanEnd=E` (`SpectrumList_Bruker.cpp:785`,
restored in the net8 port). The *profile* `combineIMS` Hela_QC_PASEF reference mzMLs
in the shared `.tar.bz2` archive still carry the old plain `merged=N` (the `-centroid`
siblings in the archive were already updated to the 4-field form). Rather than edit the
shared archive — which is consumed by the cpp vendor TeamCity configs too — we pin the
corrected profile references here so the TC blast radius stays limited to pwiz-sharp's
own config.

Files:
- `Hela_QC_PASEF_Slot1-first-6-frames-combineIMS.mzML`      (15 combined spectra)
- `Hela_QC_PASEF_Slot1-first-6-frames-combineIMS-ms1.mzML`  (1 MS1 combined frame)
- `Hela_QC_PASEF_Slot1-first-6-frames-combineIMS-ms2.mzML`  (14 MS2-window combined spectra)

Each is byte-identical to the archive copy except the combined-spectrum `id` attributes,
which were promoted to the 4-field form (values copied 1:1 from the already-correct
`-centroid` siblings).

**When to remove.** Delete these once `Reader_Bruker_Test.data.tar.bz2` is regenerated so
its profile `combineIMS` references carry the 4-field ids — then the cpp-tree fallback
matches and this override is redundant.
