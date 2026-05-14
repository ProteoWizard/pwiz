"""Per-module file-level port-status diagrams with hover detail.

For each top-level cpp module (data/common, data/msdata, data/identdata, utility,
analysis), walks the .cpp source files and emits a Graphviz DOT showing each one
colored by port status (full / partial / none / skipped). Files are grouped into
sub-areas — Readers, Writers, Serializers, etc. — via the SUBAREA map below.

Hover the rendered SVG: each node carries a tooltip from DETAIL_FILE describing
what specifically was/wasn't ported, and why. Click a node to jump to the cpp
source on GitHub (the href on the node points at the master-branch file).

Usage:
    python module_detail.py <module>      # e.g. data/msdata
    python module_detail.py all           # emit all five .dot files at once

Sibling to jamdep.py (module-level diagram). Same color palette, same renderer.
"""

from __future__ import annotations
import os
import re
import sys
from pathlib import Path

PWIZ_ROOT = Path("C:/dev/pwiz-msconvert-pr/pwiz")
SHARP_ROOT = Path("C:/dev/pwiz-msconvert-pr/pwiz-sharp/src")
OUT_DIR = Path("C:/dev/pwiz-msconvert-pr/pwiz-sharp/docs")

# ---------------------------------------------------------------------------
# Status table (relpath-from-pwiz/ → status). Explicit. Anything not listed
# defaults to "none" (red) so the diagram surfaces gaps.
#
# Values: "full" / "partial" / "none" / "skipped"
# "skipped" means we deliberately won't port (replaced by BCL / not used by
# msconvert path / cross-platform replacement). Rendered with strikethrough.
# ---------------------------------------------------------------------------

STATUS_FILE: dict[str, str] = {
    # ------------------------------------------------------------------------
    # data/common — CV, ParamContainer, OBO, BinaryIndexStream, Diff, Unimod
    # ------------------------------------------------------------------------
    "data/common/cv": "full",
    "data/common/ParamTypes": "full",          # CVParam/UserParam/ParamContainer
    "data/common/Unimod": "full",
    "data/common/obo": "full",
    "data/common/CVTranslator": "full",
    "data/common/diff_std": "full",
    "data/common/MemoryIndex": "full",
    "data/common/BinaryIndexStream": "full",
    "data/common/Index": "full",                # interface, satisfied by MemoryIndex/BinaryIndexStream
    "data/common/cvgen": "skipped",             # code-gen tool, replaced by python generator
    "data/common/cvgen_common": "skipped",      # ditto

    # ------------------------------------------------------------------------
    # data/msdata — core MSData model + I/O
    # ------------------------------------------------------------------------
    "data/msdata/MSData": "full",
    "data/msdata/MSDataFile": "full",            # CalculateSha1Checksums + Write dispatcher
    "data/msdata/IO": "full",                   # mzML XML element reader/writer
    "data/msdata/Reader": "full",               # IReader interface
    "data/msdata/DefaultReaderList": "full",
    "data/msdata/References": "full",
    "data/msdata/Diff": "full",
    "data/msdata/LegacyAdapter": "none",
    "data/msdata/MSDataMerger": "partial",      # Converter.MergeRun does it inline
    "data/msdata/BinaryDataEncoder": "full",
    "data/msdata/MSNumpress": "full",
    "data/msdata/Index_mzML": "full",
    "data/msdata/MemoryMRUCache": "full",       # ports as LruCache helpers
    "data/msdata/SHA1OutputObserver": "full",   # HashingCountingStream
    "data/msdata/RAMPAdapter": "skipped",       # legacy C API shim; Skyline/mzR users
    "data/msdata/SpectrumInfo": "full",
    "data/msdata/SpectrumIterator": "full",
    "data/msdata/SpectrumListBase": "full",
    "data/msdata/SpectrumListCache": "full",
    "data/msdata/SpectrumListWrapper": "full",  # passes through to Pwiz.Analysis.SpectrumListWrapper
    "data/msdata/SpectrumWorkerThreads": "none",
    "data/msdata/ChromatogramListBase": "full",
    "data/msdata/TextWriter": "none",           # Format_Text output — never wired

    "data/msdata/Serializer_mzML": "full",      # MzmlReader / MzmlWriter
    "data/msdata/Serializer_mzXML": "full",     # MzxmlReader / MzxmlWriter
    "data/msdata/Serializer_MGF": "full",       # MgfReader / MgfSerializer
    "data/msdata/Serializer_MSn": "none",
    "data/msdata/Serializer_mz5": "partial",    # read path ported (Mz5ReaderAdapter); writer not (use cpp msconvert)

    "data/msdata/SpectrumList_mzML": "full",
    "data/msdata/SpectrumList_mzXML": "full",
    "data/msdata/SpectrumList_MGF": "full",
    "data/msdata/SpectrumList_MSn": "none",
    "data/msdata/SpectrumList_mz5": "full",     # Mz5SpectrumList (lazy slicer over global m/z + intensity + IM datasets)
    "data/msdata/SpectrumList_BTDX": "none",    # legacy Bruker BTDX
    "data/msdata/ChromatogramList_mzML": "full",
    "data/msdata/ChromatogramList_mz5": "full", # Mz5ChromatogramList (lazy slicer over global time + intensity datasets)
    "data/msdata/examples": "full",             # InitializeTiny + AddMiapeExampleMetadata

    # mz5 + mzMLb HDF5 sub-modules
    "data/msdata/mz5/Configuration_mz5": "full",      # Mz5Configuration / Mz5Datasets
    "data/msdata/mz5/Connection_mz5": "partial",      # Mz5Connection — read paths ported (file open + ReadFull/ReadDoubles/vlen reclaim); write paths not
    "data/msdata/mz5/Datastructures_mz5": "full",     # Mz5Types — all 25 POD record types + HDF5 compound type registrations match cpp field names
    "data/msdata/mz5/ReferenceRead_mz5": "full",      # Mz5ReferenceRead — walks document-level metadata + CV refs
    "data/msdata/mz5/ReferenceWrite_mz5": "none",     # writer not done (read-only port)
    "data/msdata/mz5/Translator_mz5": "partial",      # delta-mz + log-intensity reverse done on read; forward translation not

    "data/msdata/mzmlb/Connection_mzMLb": "full",   # ported as MzMlbConnection
    # Reader_mzMLb lives in DefaultReaderList in cpp (not a separate file); the
    # Serializer / SpectrumList / ChromatogramList variants below don't exist as
    # standalone cpp files either — they're all collapsed into Connection_mzMLb's
    # streaming API plus the existing mzML reader/writer. Our port mirrors this
    # via MzMlbReaderAdapter + MzMlbWriter, composing MzmlReader / MzmlWriter
    # with an MzMlbConnection-backed external-binary source/sink.

    # ------------------------------------------------------------------------
    # data/identdata — mzIdentML + pepXML readers/writers (recent landing)
    # ------------------------------------------------------------------------
    "data/identdata/IdentData": "full",
    "data/identdata/IdentDataFile": "full",
    "data/identdata/IO": "full",
    "data/identdata/Diff": "full",
    "data/identdata/References": "full",
    "data/identdata/Reader": "full",
    "data/identdata/DefaultReaderList": "full",
    "data/identdata/Serializer_mzid": "full",
    "data/identdata/Serializer_pepXML": "full",
    "data/identdata/Serializer_protXML": "none",
    "data/identdata/Serializer_Text": "none",
    "data/identdata/TextWriter": "none",
    "data/identdata/DelimReader": "none",
    "data/identdata/DelimWriter": "none",
    "data/identdata/MascotReader": "none",
    "data/identdata/Pep2MzIdent": "none",
    "data/identdata/KwCVMap": "none",
    "data/identdata/MzidPredicates": "none",
    "data/identdata/Version": "skipped",
    "data/identdata/examples": "skipped",

    # ------------------------------------------------------------------------
    # utility — chemistry, math, misc, minimxml, proteome
    # ------------------------------------------------------------------------
    "utility/chemistry/Chemistry": "full",     # Element / Formula / Mass calc
    "utility/chemistry/ChemistryData": "full",  # generated table
    "utility/chemistry/Ion": "full",
    "utility/chemistry/IsotopeCalculator": "full",
    "utility/chemistry/IsotopeTable": "full",
    "utility/chemistry/IsotopeEnvelopeEstimator": "none",
    "utility/chemistry/MZTolerance": "full",
    "utility/chemistry/MzMobilityWindow": "none",
    "utility/chemistry/iso": "skipped",         # tiny single-purpose tool
    "utility/chemistry/parse_isotopes": "skipped",

    "utility/math/Stats": "none",
    "utility/math/LinearLeastSquaresRegression": "none",
    "utility/math/Parabola": "full",
    "utility/math/erf": "skipped",              # use System.Math
    "utility/math/round": "skipped",            # use System.Math
    "utility/math/Random": "skipped",           # use System.Random / shared MsvcRand port
    "utility/math/Sort": "skipped",             # use LINQ OrderBy
    "utility/math/SignalToNoiseEstimatorPoissonHomogeneous": "none",
    "utility/math/MatrixInverse": "none",
    "utility/math/OrderedPair": "none",

    "utility/misc/Base64": "skipped",           # System.Convert.ToBase64String
    "utility/misc/SHA1": "skipped",             # System.Security.Cryptography
    "utility/misc/SHA1Calculator": "full",      # Pwiz.Util.Sha1Calculator
    "utility/misc/SHA1_ostream": "skipped",     # HashingCountingStream covers
    "utility/misc/IntegerSet": "full",
    "utility/misc/IterationListener": "full",
    "utility/misc/DateTime": "skipped",         # System.DateTime
    "utility/misc/Filesystem": "skipped",       # System.IO.Path / Directory
    "utility/misc/Environment": "skipped",      # System.Environment
    "utility/misc/Exception": "skipped",
    "utility/misc/Singleton": "skipped",        # use static or DI
    "utility/misc/Std": "skipped",              # C++ std:: alias header
    "utility/misc/String": "skipped",           # System.String
    "utility/misc/Stream": "skipped",
    "utility/misc/Image": "none",
    "utility/misc/Timer": "skipped",            # System.Diagnostics.Stopwatch
    "utility/misc/Container": "skipped",        # std::vector etc.
    "utility/misc/BinaryData": "skipped",       # use byte[] / Span<byte>
    "utility/misc/almost_equal": "full",        # Pwiz.Util.FloatingPoint
    "utility/misc/automation_vector": "skipped",# COM-specific
    "utility/misc/CharIndexedVector": "skipped",
    "utility/misc/ClickwrapPrompter": "skipped",# vendor-license prompt at build time
    "utility/misc/COMInitializer": "skipped",
    "utility/misc/cpp_cli_utilities": "skipped",
    "utility/misc/endian": "skipped",           # BinaryPrimitives in BCL
    "utility/misc/Export": "skipped",
    "utility/misc/mru_list": "full",            # LruCache helpers
    "utility/misc/MSIHandler": "skipped",
    "utility/misc/Once": "skipped",             # lazy init via System
    "utility/misc/optimized_lexical_cast": "skipped",
    "utility/misc/random_access_compressed_ifstream": "none",
    "utility/misc/shared_map": "skipped",
    "utility/misc/sort_together": "skipped",
    "utility/misc/span": "skipped",             # System.Span<T>
    "utility/misc/TabReader": "none",
    "utility/misc/unit": "skipped",             # MSTest replaces unit.hpp
    "utility/misc/VendorReaderTestHarness": "full",  # ports as test/Pwiz.TestHarness
    "utility/misc/virtual_map": "skipped",
    "utility/misc/mzd": "skipped",
    "utility/misc/sha1calc": "skipped",

    "utility/minimxml/SAXParser": "skipped",    # System.Xml.XmlReader
    "utility/minimxml/XMLWriter": "skipped",    # System.Xml.XmlWriter

    "utility/proteome/AminoAcid": "none",
    "utility/proteome/Modification": "none",
    "utility/proteome/ModificationMap": "none",
    "utility/proteome/Peptide": "none",
    "utility/proteome/PeptideDatabase": "none",
    "utility/proteome/Digestion": "none",
    "utility/proteome/Ion": "none",             # different Ion than chemistry's
    "utility/proteome/Chemistry": "skipped",    # duplicate of utility/chemistry

    "utility/findmf": "skipped",                # whole subtree, not used by msconvert

    # ------------------------------------------------------------------------
    # analysis — every per-filter / per-algorithm cpp file. Sub-namespaces:
    # spectrum_processing / chromatogram_processing / peakdetect / demux / etc.
    # ------------------------------------------------------------------------
    "analysis/spectrum_processing/SpectrumListFactory": "full",
    "analysis/spectrum_processing/SpectrumList_Filter": "full",
    "analysis/spectrum_processing/SpectrumList_PeakPicker": "full",
    "analysis/spectrum_processing/SpectrumList_Smoother": "full",
    "analysis/spectrum_processing/SpectrumList_Sorter": "full",
    "analysis/spectrum_processing/SpectrumList_MZWindow": "full",
    "analysis/spectrum_processing/SpectrumList_MetadataFixer": "full",
    "analysis/spectrum_processing/SpectrumList_MZRefiner": "full",
    "analysis/spectrum_processing/SpectrumList_LockmassRefiner": "full",
    "analysis/spectrum_processing/SpectrumList_ChargeStateCalculator": "full",
    "analysis/spectrum_processing/SpectrumList_ChargeFromIsotope": "full",
    "analysis/spectrum_processing/SpectrumList_PrecursorRefine": "full",
    "analysis/spectrum_processing/SpectrumList_TitleMaker": "full",
    "analysis/spectrum_processing/SpectrumList_ZeroSamplesFilter": "full",
    "analysis/spectrum_processing/SpectrumList_PeakFilter": "full",   # framework + Ms2Deisotoper / Ms2NoiseFilter / ETD
    "analysis/spectrum_processing/SpectrumList_ScanSummer": "full",
    "analysis/spectrum_processing/SpectrumList_Demux": "full",
    "analysis/spectrum_processing/SpectrumList_DiaUmpire": "none",
    "analysis/spectrum_processing/SpectrumList_3D": "none",
    "analysis/spectrum_processing/SpectrumList_IonMobility": "none",
    "analysis/spectrum_processing/SpectrumList_PrecursorRecalculator": "none",
    "analysis/spectrum_processing/MS2Deisotoper": "full",
    "analysis/spectrum_processing/MS2NoiseFilter": "full",
    "analysis/spectrum_processing/MzShiftFilter": "full",
    "analysis/spectrum_processing/PrecursorMassFilter": "full",
    "analysis/spectrum_processing/PrecursorRecalculator": "none",
    "analysis/spectrum_processing/PrecursorRecalculatorDefault": "none",
    "analysis/spectrum_processing/ThresholdFilter": "full",

    "analysis/peakdetect/PeakFinder": "full",   # CwtPeakDetector + LocalMaximumPeakDetector
    "analysis/peakdetect/PeakFitter": "none",
    "analysis/peakdetect/PeakExtractor": "none",
    "analysis/peakdetect/FeatureDetector": "none",
    "analysis/peakdetect/FeatureDetectorPeakel": "none",
    "analysis/peakdetect/FeatureDetectorSimple": "none",
    "analysis/peakdetect/FeatureModeler": "none",
    "analysis/peakdetect/MZRTField": "none",
    "analysis/peakdetect/Noise": "none",
    "analysis/peakdetect/PeakFamilyDetector": "none",
    "analysis/peakdetect/PeakFamilyDetectorFT": "none",
    "analysis/peakdetect/PeakelGrower": "none",
    "analysis/peakdetect/PeakelPicker": "none",
    "analysis/peakdetect/msextract": "skipped", # cli driver

    "analysis/demux/DemuxSolver": "full",
    "analysis/demux/DemuxHelpers": "full",
    "analysis/demux/DemuxTypes": "full",
    "analysis/demux/IDemultiplexer": "full",
    "analysis/demux/MSXDemultiplexer": "full",
    "analysis/demux/OverlapDemultiplexer": "full",
    "analysis/demux/PrecursorMaskCodec": "full",
    "analysis/demux/SpectrumPeakExtractor": "full",
    "analysis/demux/IPrecursorMaskCodec": "full",
    "analysis/demux/CubicHermiteSpline": "none",
    "analysis/demux/IInterpolation": "none",
    "analysis/demux/FSDemux": "skipped",
    "analysis/demux/DemuxDebugReader": "skipped",
    "analysis/demux/DemuxDebugWriter": "skipped",
    "analysis/demux/DemuxTestData": "skipped",
    "analysis/demux/MatrixIO": "skipped",
    "analysis/demux/EnumConstantNotPresentException": "skipped",
    "analysis/demux/DemuxDataProcessingStrings": "full",

    # other analysis subdirs — wholesale "none" or "skipped"
}

# ---------------------------------------------------------------------------
# Per-file mouseover detail. Keys match STATUS_FILE. Free-form text — the SVG
# renderer shows it as a tooltip. Keep each entry under ~280 chars so it fits
# in a default browser tooltip without truncation. Use newlines (\n) for
# multi-line tooltips; Graphviz preserves them in the SVG <title>.
# Missing entries fall back to a generic status-based tooltip.
# ---------------------------------------------------------------------------

DETAIL_FILE: dict[str, str] = {
    # =========================================================================
    # data/common
    # =========================================================================
    "data/common/cv": (
        "Auto-generated PSI-MS controlled-vocabulary table.\n"
        "Ported as Common/Cv/CVID.generated.cs (~30K enum entries) plus CvLookup\n"
        "for term name/ID lookup. Generated by Common/Cv/generate_cvid.py from cv.hpp."
    ),
    "data/common/ParamTypes": (
        "ParamContainer / ParamGroup / CVParam / UserParam.\n"
        "Ported as Common/Params/ParamContainer.cs + CvParam.cs + UserParam.cs.\n"
        "Same accessor surface as cpp (Set/Get/HasCVParam/cvParamChild/UserParams)."
    ),
    "data/common/Unimod": (
        "Unimod modification database.\n"
        "Ported as Common/Cv/Unimod.cs with embedded unimod.obo + populated specificity\n"
        "tables (real Δmono/Δavg masses, per-AA-position specificities)."
    ),
    "data/common/obo": (
        "OBO 1.2 parser. Ported as Common/Cv/OboParser.cs.\n"
        "Drives the cv / unit / unimod term tables from embedded .obo resources at\n"
        "Common/Cv/Resources/. Round-trip parity tests live in Common.Tests."
    ),
    "data/common/CVTranslator": (
        "Free-text → CVID heuristic mapper (vendor name strings → MS_LCMS_xxx).\n"
        "Ported as Common/Cv/CvTranslator.cs. Used by every vendor Reader's\n"
        "TranslateInstrumentModel helper."
    ),
    "data/common/diff_std": (
        "Generic diff infrastructure: Diff<T>, DiffConfig, DiffResult<T>.\n"
        "Ported as Common/Diff.cs. Drives MSData/IdentData round-trip parity\n"
        "tests + the msdiff tool replacement."
    ),
    "data/common/MemoryIndex": "In-memory IIndex implementation. Ported as Common/Index/MemoryIndex.cs.",
    "data/common/BinaryIndexStream": (
        "Random-access index serialized to a binary stream (used by mzML offset cache).\n"
        "Ported as Common/Index/BinaryIndexStream.cs."
    ),
    "data/common/Index": "IIndex interface satisfied by both MemoryIndex + BinaryIndexStream.",
    "data/common/cvgen": "Code-gen tool — replaced by Common/Cv/generate_cvid.py (Python).",
    "data/common/cvgen_common": "Code-gen helpers — folded into generate_cvid.py.",

    # =========================================================================
    # data/msdata
    # =========================================================================
    "data/msdata/MSData": (
        "Top-level MSData container model.\n"
        "Ported as MsData/MSData.cs + Run.cs + FileDescription.cs + Software.cs +\n"
        "Scan/Spectrum/Chromatogram property bags. All field names PascalCase, IDisposable\n"
        "for vendor handles."
    ),
    "data/msdata/MSDataFile": (
        "✓ calculateSHA1Checksums   →  MSDataFile.CalculateSha1Checksums()\n"
        "✓ WriteConfig + Format     →  Pwiz.Data.MsData.WriteConfig + WriteFormat\n"
        "✓ Precision / Compression  →  Encoding.BinaryPrecision / BinaryCompression\n"
        "✓ write(msd,path,cfg)      →  MSDataFile.Write — dispatches to MzmlWriter /\n"
        "                              MzxmlWriter / MgfSerializer based on WriteConfig.Format\n"
        "✗ detect(filename)         →  not yet ported; Reader.Identify() does sniff-by-extension"
    ),
    "data/msdata/IO": (
        "Streaming mzML element-by-element reader + writer.\n"
        "Ported as MsData/Mzml/MzmlReader.cs + MzmlWriter.cs (System.Xml.XmlReader/XmlWriter\n"
        "instead of cpp's SAXParser/XMLWriter)."
    ),
    "data/msdata/Reader": "IReader interface + ReaderConfig. Ported as MsData/IReader.cs.",
    "data/msdata/DefaultReaderList": (
        "Reader dispatcher (mzML / mzXML / MGF + vendor extension points).\n"
        "Ported as MsData/Readers/ReaderList.cs. Vendor readers register at startup\n"
        "(see MsConvert/Converter ctor)."
    ),
    "data/msdata/References": (
        "Resolves IDREF cross-references inside an MSData graph (e.g., scan.spectrumRef → spectrum).\n"
        "Ported as MsData/References.cs."
    ),
    "data/msdata/Diff": (
        "MSData-specific diff specialization (knows how to walk Run/Spectrum/CV nesting).\n"
        "Ported as MsData/Diff/MSDataDiff.cs. Drives msconvert-cpp parity tests."
    ),
    "data/msdata/LegacyAdapter": (
        "Pre-2010 adapter that synthesized PSI-MS terms from old free-text fields.\n"
        "Not needed: modern vendor readers set CV terms directly. SKIPPED."
    ),
    "data/msdata/MSDataMerger": (
        "PARTIAL — multi-file merge into a single MSData.\n"
        "Done inline inside MsConvert/Converter.MergeRun (concatenates SourceFiles +\n"
        "Software + spectra). No standalone class yet."
    ),
    "data/msdata/BinaryDataEncoder": (
        "32-/64-bit float + zlib + numpress encoder for mzML binaryDataArrays.\n"
        "Ported as MsData/BinaryDataEncoder.cs with full numpress (Pic/Linear/Slof) +\n"
        "MS-NUMPRESS Apache port."
    ),
    "data/msdata/MSNumpress": "MS-NUMPRESS algorithm. Ported as MsData/Encoding/MsNumpress.cs (line-by-line port).",
    "data/msdata/Index_mzML": (
        "mzML spectrum/chromatogram offset index reader.\n"
        "Ported as MsData/Mzml/MzmlIndexReader.cs. Reads <indexList> trailer to seek to\n"
        "individual spectra without parsing the full file."
    ),
    "data/msdata/MemoryMRUCache": (
        "Bounded LRU cache (least-recently-used eviction).\n"
        "Ported as Util/Collections/LruCache.cs. Used by SpectrumList_ChargeFromIsotope\n"
        "for parent MS1 spectra."
    ),
    "data/msdata/SHA1OutputObserver": (
        "Streaming SHA-1 + byte counter wrapper around an std::ostream.\n"
        "Ported as MsData/Mzml/HashingCountingStream.cs."
    ),
    "data/msdata/RAMPAdapter": (
        "Legacy C-API shim wrapping pwiz to look like ISB RAMP — used by older readers.\n"
        "Not used by msconvert / Skyline. SKIPPED."
    ),
    "data/msdata/SpectrumInfo": "Lightweight per-scan metadata struct. Ported as MsData/Spectra/SpectrumInfo.cs.",
    "data/msdata/SpectrumIterator": "Scan-time spectrum iterator. Ported as MsData/Spectra/SpectrumIterator.cs.",
    "data/msdata/SpectrumListBase": "Base class with shared SpectrumList plumbing. Ported as MsData/Spectra/SpectrumListBase.cs.",
    "data/msdata/SpectrumListCache": "LRU-caching SpectrumList decorator. Ported as MsData/Spectra/SpectrumListCache.cs.",
    "data/msdata/SpectrumListWrapper": "Spectrum-list pass-through base class. Ported as Analysis/SpectrumListWrapper.cs.",
    "data/msdata/SpectrumWorkerThreads": (
        "Multi-threaded spectrum reader (used by mzML writer to overlap read+encode).\n"
        "Not ported: pwiz-sharp is single-threaded for now; the speed gap is small for\n"
        "modern hardware vs. the porting risk."
    ),
    "data/msdata/ChromatogramListBase": "Same as SpectrumListBase but for ChromatogramList. Ported as MsData/Chromatograms/ChromatogramListBase.cs.",
    "data/msdata/TextWriter": (
        "Format_Text output (a pwiz-internal pretty-print).\n"
        "Never wired in any GUI/CLI we care about. NOT PORTED."
    ),

    "data/msdata/Serializer_mzML": "Full mzML reader+writer. Ported as MsData/Mzml/MzmlReader.cs + MzmlWriter.cs.",
    "data/msdata/Serializer_mzXML": "Full mzXML reader+writer. Ported as MsData/MzXml/MzxmlReader.cs + MzxmlWriter.cs.",
    "data/msdata/Serializer_MGF": (
        "MGF writer (Mascot Generic Format — MS2 peak-list export).\n"
        "Ported as MsData/Mgf/MgfSerializer.cs (text-mode writer with TPP-compat title formats)."
    ),
    "data/msdata/Serializer_MSn": "Legacy MS1/MS2/CMS1/CMS2 format. NOT PORTED — narrow user base.",
    "data/msdata/Serializer_mz5": (
        "HDF5-based mz5 serializer. READ ported as Mz5ReaderAdapter + Mz5Connection + "
        "Mz5ReferenceRead + Mz5SpectrumList + Mz5ChromatogramList (under MsData/Mz5/). "
        "Writer not done — round-trip tests shell out to cpp msconvert --mz5 for the write side."
    ),

    "data/msdata/SpectrumList_mzML": "Lazy-load mzML SpectrumList implementation. Ported as MsData/Mzml/SpectrumList_Mzml.cs.",
    "data/msdata/SpectrumList_mzXML": "Lazy-load mzXML SpectrumList. Ported as MsData/MzXml/SpectrumList_Mzxml.cs.",
    "data/msdata/SpectrumList_MGF": "Lazy-load MGF SpectrumList. Ported as MsData/Mgf/SpectrumList_Mgf.cs.",
    "data/msdata/SpectrumList_MSn": "Legacy MS1/MS2 SpectrumList. NOT PORTED — paired with Serializer_MSn.",
    "data/msdata/SpectrumList_mz5": (
        "mz5 SpectrumList. Ported as MsData/Mz5/Mz5SpectrumList.cs — lazy slicer over the "
        "global SpectrumMZ + SpectrumIntensity datasets via SpectrumIndex end-offsets. Reverses "
        "delta-mz encoding via cumulative sum when FileInformation.deltaMZ is set."
    ),
    "data/msdata/SpectrumList_BTDX": "Legacy Bruker BTDX text format. NOT PORTED — superseded by Bruker .d.",
    "data/msdata/ChromatogramList_mzML": "Lazy-load mzML ChromatogramList. Ported as MsData/Mzml/ChromatogramList_Mzml.cs.",
    "data/msdata/ChromatogramList_mz5": (
        "mz5 ChromatogramList. Ported as MsData/Mz5/Mz5ChromatogramList.cs — lazy slicer over "
        "ChromatogramTime + ChromatogramIntensity via ChromatogramIndex. No translator (mz5's "
        "delta encoding only applies to m/z, not time)."
    ),
    "data/msdata/examples": (
        "Canonical example MSData fixture builder. Ported as MsData/Examples.cs.\n"
        "InitializeTiny → 5 spectra (MS1/MS2 CID/no-data/MS2 ETD+CID/MALDI) + 2 chromatograms\n"
        "(TIC + SIC). AddMiapeExampleMetadata layers MIAPE-compliant metadata on top.\n"
        "Used by round-trip / Diff / serializer parity tests."
    ),

    "data/msdata/mz5/Configuration_mz5": (
        "mz5/HDF5 file-level config. Ported as MsData/Mz5/Mz5Configuration.cs + Mz5Datasets.cs "
        "(dataset-name enum + version/length constants matching cpp)."
    ),
    "data/msdata/mz5/Connection_mz5": (
        "mz5 H5File wrapper. Read paths ported as MsData/Mz5/Mz5Connection.cs — file open + "
        "ReadFull/ReadDoubles + vlen-reclaim + refcounted shared ownership for SpectrumList + "
        "ChromatogramList. Write paths not done."
    ),
    "data/msdata/mz5/Datastructures_mz5": (
        "mz5 typed structs. Ported as MsData/Mz5/Mz5Types.cs — all 25 POD record types with "
        "HDF5 compound type registrations whose field names match cpp's exactly (HDF5 matches "
        "compound fields by name on read — mismatches silently leave struct fields at zero)."
    ),
    "data/msdata/mz5/ReferenceRead_mz5": (
        "mz5 ID-resolution on read. Ported as MsData/Mz5/Mz5ReferenceRead.cs — walks document-"
        "level metadata in cpp's dependency order (CVs → CVReference → CVParam/UserParam/RefParam "
        "tables → ParamGroups → SourceFiles / Software / DataProcessings / Samples / ScanSettings "
        "/ InstrumentConfigurations → Run → Spectrum/Chromatogram lists). Memoizes CVID lookups."
    ),
    "data/msdata/mz5/ReferenceWrite_mz5": "mz5 ID-resolution on write. NOT PORTED — read-only port.",
    "data/msdata/mz5/Translator_mz5": (
        "mz5 ↔ MSData translation. Reverse direction (delta-mz cumulative-sum + log-intensity "
        "decode on read) is in Mz5Connection / Mz5SpectrumList; forward translation for write not done."
    ),

    "data/msdata/mzmlb/Connection_mzMLb": "mzMLb (HDF5-backed mzML) connection wrapper. Ported as Pwiz.Data.MsData.MzMlb.MzMlbConnection (HDF.PInvoke-backed, exposes mzML XML as a seekable Stream + typed Append/Read for binary datasets). Reader/Writer adapters at MzMlbReaderAdapter / MzMlbWriter; integration into MzmlReader/MzmlWriter via IExternalBinarySource/Sink. Bidirectional cpp parity verified.",
    "data/msdata/mzmlb/Reader_mzMLb": "mzMLb top-level reader. NOT PORTED.",
    "data/msdata/mzmlb/SpectrumList_mzMLb": "mzMLb lazy SpectrumList. NOT PORTED.",
    "data/msdata/mzmlb/ChromatogramList_mzMLb": "mzMLb lazy ChromatogramList. NOT PORTED.",
    "data/msdata/mzmlb/Serializer_mzMLb": "mzMLb writer. NOT PORTED.",

    # =========================================================================
    # data/identdata
    # =========================================================================
    "data/identdata/IdentData": "Top-level mzIdentML container model. Ported as IdentData/IdentData.cs + per-element classes (Mzid/*).",
    "data/identdata/IdentDataFile": "File-level helpers (open/save). Ported as IdentData/IdentDataFile.cs.",
    "data/identdata/IO": "mzIdentML element-by-element parser. Ported as IdentData/Mzid/MzidReader.cs + MzidWriter.cs.",
    "data/identdata/Diff": "IdentData-specific diff specialization. Ported as IdentData/Diff.cs.",
    "data/identdata/References": "IDREF resolver for the IdentData graph. Ported as IdentData/References.cs.",
    "data/identdata/Reader": "IReader interface for IdentData files. Ported as IdentData/Reader.cs.",
    "data/identdata/DefaultReaderList": "Reader dispatcher (mzid / pepXML). Ported as IdentData/DefaultReaderList.cs.",
    "data/identdata/Serializer_mzid": "mzIdentML reader+writer. Ported as IdentData/Mzid/MzidSerializer.cs.",
    "data/identdata/Serializer_pepXML": "pepXML reader+writer. Ported as IdentData/PepXml/PepXmlSerializer.cs.",
    "data/identdata/Serializer_protXML": "protXML protein-inference format. NOT PORTED — narrow user base.",
    "data/identdata/Serializer_Text": "Pretty-print text dump of IdentData. NOT PORTED.",
    "data/identdata/TextWriter": "Pretty-print walker used by Serializer_Text. NOT PORTED.",
    "data/identdata/DelimReader": "TSV / CSV import (PeptideShaker, MSGF+ outputs). NOT PORTED — re-port when a consumer asks.",
    "data/identdata/DelimWriter": "TSV / CSV export. NOT PORTED.",
    "data/identdata/MascotReader": "Mascot .dat parser. NOT PORTED — niche; users go via pepXML.",
    "data/identdata/Pep2MzIdent": "pepXML → mzIdentML one-shot converter. NOT PORTED.",
    "data/identdata/KwCVMap": "Keyword → CVID heuristic table. NOT PORTED.",
    "data/identdata/MzidPredicates": "LINQ-style predicates over an IdentData graph. NOT PORTED.",

    # =========================================================================
    # utility
    # =========================================================================
    "utility/chemistry/Chemistry": (
        "Element / Formula / Mass arithmetic. Ported as Util/Chemistry/Formula.cs + Element*.cs +\n"
        "PhysicalConstants.cs. Generated 118-element table from cpp via Chemistry/generate_elements.py."
    ),
    "utility/chemistry/ChemistryData": "Generated isotope masses + abundances for all 118 elements. Ported as Util/Chemistry/ChemistryData.generated.cs.",
    "utility/chemistry/Ion": "m/z-from-mass-and-charge helpers. Ported as Util/Chemistry/Ion.cs.",
    "utility/chemistry/IsotopeCalculator": "Isotope-envelope distribution calculator. Ported as Util/Chemistry/IsotopeCalculator.cs.",
    "utility/chemistry/IsotopeTable": "Per-element isotope table. Ported as Util/Chemistry/IsotopeTable.cs.",
    "utility/chemistry/IsotopeEnvelopeEstimator": "Statistical fit of isotope-envelope shapes. NOT PORTED — no current consumer.",
    "utility/chemistry/MZTolerance": "m/z tolerance ppm/Da arithmetic. Ported as Util/Chemistry/MZTolerance.cs.",
    "utility/chemistry/MzMobilityWindow": "(m/z, ion-mobility) window math. NOT PORTED.",
    "utility/chemistry/iso": "Small command-line tool. SKIPPED.",
    "utility/chemistry/parse_isotopes": "Tiny CLI helper. SKIPPED.",

    "utility/math/Stats": "Mean/variance/percentile helpers. NOT PORTED — MathNet.Numerics covers the use cases.",
    "utility/math/LinearLeastSquaresRegression": "Linear least-squares fit. NOT PORTED.",
    "utility/math/Parabola": "3-point parabola fit (peak-centroiding). Ported as Util/Math/Parabola.cs.",
    "utility/math/erf": "Use System.Math.Erf (.NET 5+). SKIPPED.",
    "utility/math/round": "Use System.Math.Round. SKIPPED.",
    "utility/math/Random": "Use System.Random for general; MsvcRand port for cpp-bit-exact parity (Analysis/SpectrumList_ChargeFromIsotope.cs). SKIPPED.",
    "utility/math/Sort": "Use LINQ OrderBy / Array.Sort. SKIPPED.",
    "utility/math/SignalToNoiseEstimatorPoissonHomogeneous": "Poisson-based S/N. NOT PORTED.",
    "utility/math/MatrixInverse": "Use MathNet.Numerics.LinearAlgebra. NOT PORTED.",
    "utility/math/OrderedPair": "Tiny (x,y) struct. NOT PORTED — System.ValueTuple covers.",

    "utility/misc/Base64": "Use System.Convert.ToBase64String / Buffers.Text.Base64. SKIPPED.",
    "utility/misc/SHA1": "Use System.Security.Cryptography.SHA1. SKIPPED.",
    "utility/misc/SHA1Calculator": "Streaming SHA-1 helper. Ported as Util/Sha1Calculator.cs.",
    "utility/misc/SHA1_ostream": "stdout-tee SHA1 stream — covered by HashingCountingStream in MsData. SKIPPED.",
    "utility/misc/IntegerSet": "Sparse integer set with range arithmetic. Ported as Util/IntegerSet.cs.",
    "utility/misc/IterationListener": "Progress listener interface + registry. Ported as Util/Misc/IterationListener.cs (IIterationListener + IterationListenerRegistry).",
    "utility/misc/DateTime": "Use System.DateTime + System.DateTimeOffset. SKIPPED.",
    "utility/misc/Filesystem": "Use System.IO.Path / Directory / File. SKIPPED.",
    "utility/misc/Environment": "Use System.Environment. SKIPPED.",
    "utility/misc/Exception": "Use System exception types. SKIPPED.",
    "utility/misc/Singleton": "Use static / DI / Lazy<T>. SKIPPED.",
    "utility/misc/Std": "C++ namespace alias header (using std::...). N/A in C#. SKIPPED.",
    "utility/misc/String": "Use System.String + System.Text. SKIPPED.",
    "utility/misc/Stream": "Use System.IO streams. SKIPPED.",
    "utility/misc/Image": "Image I/O (BMP/PNG output for peakpicking debug). NOT PORTED.",
    "utility/misc/Timer": "Use System.Diagnostics.Stopwatch. SKIPPED.",
    "utility/misc/Container": "C++ STL container alias header. N/A. SKIPPED.",
    "utility/misc/BinaryData": "Use byte[] / Span<byte>. SKIPPED.",
    "utility/misc/almost_equal": "Floating-point epsilon compare. Ported as Util/Math/FloatingPoint.cs (AlmostEqual).",
    "utility/misc/automation_vector": "COM SAFEARRAY interop helper. N/A in pwiz-sharp. SKIPPED.",
    "utility/misc/CharIndexedVector": "Char-keyed bitmap. SKIPPED — replaced by Dictionary<char,T>.",
    "utility/misc/ClickwrapPrompter": "Vendor-license EULA prompt at build time. SKIPPED — handled by build scripts.",
    "utility/misc/COMInitializer": "COM apartment init. SKIPPED.",
    "utility/misc/cpp_cli_utilities": "C++/CLI marshaling helpers (ToStdString etc.). SKIPPED — pwiz-sharp has no CLI bridge.",
    "utility/misc/endian": "Use System.Buffers.Binary.BinaryPrimitives. SKIPPED.",
    "utility/misc/Export": "DLL_EXPORT/IMPORT macros. N/A. SKIPPED.",
    "utility/misc/mru_list": "Bounded LRU helper (paired with MemoryMRUCache). Ported via Util/Collections/LruCache.cs.",
    "utility/misc/MSIHandler": "Windows Installer detection. SKIPPED.",
    "utility/misc/Once": "Thread-safe one-shot init. Use Lazy<T> / static ctor. SKIPPED.",
    "utility/misc/optimized_lexical_cast": "Boost lexical_cast tuning. SKIPPED — use BCL parse methods.",
    "utility/misc/random_access_compressed_ifstream": "Seekable gzip stream. NOT PORTED — not used by core msconvert path.",
    "utility/misc/shared_map": "Thread-safe map wrapper. SKIPPED — use ConcurrentDictionary.",
    "utility/misc/sort_together": "Parallel-array sort. SKIPPED — use indexed Array.Sort with comparator.",
    "utility/misc/span": "Use System.Span<T>. SKIPPED.",
    "utility/misc/TabReader": "Generic TSV reader. NOT PORTED — call sites use Sciex CSV reader instead.",
    "utility/misc/unit": "C++ unit-test framework. SKIPPED — replaced by MSTest.",
    "utility/misc/VendorReaderTestHarness": (
        "Round-trip harness: reads vendor file, normalizes, diffs against reference mzML.\n"
        "Ported as test/Pwiz.TestHarness/VendorReaderTestHarness.cs. Drives every vendor\n"
        "fixture test (Reader_Bruker_*, Reader_Thermo_*, …)."
    ),
    "utility/misc/virtual_map": "Polymorphic map. SKIPPED.",
    "utility/misc/mzd": "mzd-format helper. SKIPPED.",
    "utility/misc/sha1calc": "CLI wrapper around SHA1Calculator. SKIPPED.",

    "utility/minimxml/SAXParser": "Use System.Xml.XmlReader. SKIPPED.",
    "utility/minimxml/XMLWriter": "Use System.Xml.XmlWriter. SKIPPED.",

    "utility/proteome/AminoAcid": "Per-AA residue masses + properties. NOT PORTED.",
    "utility/proteome/Modification": "Protein-level PTM record. NOT PORTED.",
    "utility/proteome/ModificationMap": "Position → Modification map. NOT PORTED.",
    "utility/proteome/Peptide": "Peptide sequence + mass model. NOT PORTED.",
    "utility/proteome/PeptideDatabase": "Indexed peptide store. NOT PORTED.",
    "utility/proteome/Digestion": "Enzyme cleavage rules. NOT PORTED.",
    "utility/proteome/Ion": "Charge-state product-ion math. NOT PORTED — different from utility/chemistry/Ion.",
    "utility/proteome/Chemistry": "Duplicate of utility/chemistry/Chemistry inside proteome. SKIPPED.",

    "utility/findmf": "findmf (Random-Forest peakelfit) — whole subtree. SKIPPED — separate research tool, not on msconvert path.",

    # =========================================================================
    # analysis/spectrum_processing
    # =========================================================================
    "analysis/spectrum_processing/SpectrumListFactory": (
        "Filter-spec parser + dispatcher (\"peakPicking true 1-\", \"msLevel 1-2\", etc.).\n"
        "Ported as Analysis/SpectrumListFactory.cs with the full msconvert filter grammar."
    ),
    "analysis/spectrum_processing/SpectrumList_Filter": "Generic index/scanTime/msLevel/polarity/scanNumber/id predicate filter. Ported as Analysis/SpectrumListFilter.cs.",
    "analysis/spectrum_processing/SpectrumList_PeakPicker": "Vendor + CWT + local-maximum centroider. Ported as Analysis/SpectrumList_PeakPicker.cs + Analysis/PeakPicking/CwtPeakDetector.cs.",
    "analysis/spectrum_processing/SpectrumList_Smoother": "Savitzky-Golay smoother. Ported as Analysis/SpectrumList_Smoother.cs.",
    "analysis/spectrum_processing/SpectrumList_Sorter": "Re-sort spectra by scanTime / scanNumber / ID. Ported as Analysis/SpectrumListSorter.cs.",
    "analysis/spectrum_processing/SpectrumList_MZWindow": "m/z range window filter. Ported as Analysis/SpectrumListMzWindow.cs.",
    "analysis/spectrum_processing/SpectrumList_MetadataFixer": "Re-derive BPI / TIC / scanWindow from peaks. Ported as Analysis/SpectrumListMetadataFixer.cs.",
    "analysis/spectrum_processing/SpectrumList_MZRefiner": (
        "External-search-result-driven m/z recalibration (mzRefiner filter).\n"
        "Ported as Analysis/SpectrumList_MZRefiner.cs with full cpp parity (7+ stat passes,\n"
        "polynomial / shift-only / global / per-scan refinement modes)."
    ),
    "analysis/spectrum_processing/SpectrumList_LockmassRefiner": (
        "Lock-mass m/z correction (Waters Q-TOF use case).\n"
        "Ported as Analysis/SpectrumList_LockmassRefiner.cs."
    ),
    "analysis/spectrum_processing/SpectrumList_ChargeStateCalculator": (
        "Heuristic precursor-charge predictor (single-vs-multi, MS1-window based).\n"
        "Ported as Analysis/SpectrumList_ChargeStateCalculator.cs."
    ),
    "analysis/spectrum_processing/SpectrumList_ChargeFromIsotope": (
        "Turbocharger — isotope-pattern precursor-charge predictor.\n"
        "Ported as Analysis/SpectrumList_ChargeFromIsotope.cs with cpp-bit-exact MsvcRand\n"
        "for pre-simulated distributions + LRU parent-spectrum cache."
    ),
    "analysis/spectrum_processing/SpectrumList_PrecursorRefine": "Re-derive precursor m/z from peak-fitted MS1. Ported as Analysis/SpectrumList_PrecursorRefine.cs.",
    "analysis/spectrum_processing/SpectrumList_TitleMaker": "TPP-compatible spectrum-title templater. Ported as Analysis/SpectrumListTitleMaker.cs.",
    "analysis/spectrum_processing/SpectrumList_ZeroSamplesFilter": "Insert / remove zero-intensity samples around profile peaks. Ported as Analysis/SpectrumListZeroSamplesFilter.cs.",
    "analysis/spectrum_processing/SpectrumList_PeakFilter": "Framework for per-spectrum peak filters (ETD precursor / charge-reduced / neutral-loss). Ported as Analysis/PeakFilters/.",
    "analysis/spectrum_processing/SpectrumList_ScanSummer": "Sum adjacent scans by retention time. Ported as Analysis/SpectrumListScanSummer.cs.",
    "analysis/spectrum_processing/SpectrumList_Demux": "DIA/MSX demultiplexer wrapper. Ported as Analysis/SpectrumListDemux.cs.",
    "analysis/spectrum_processing/SpectrumList_DiaUmpire": "DIA-Umpire MS2 deconvolution. NOT PORTED — ~10k LOC, separate research tool, no current ask.",
    "analysis/spectrum_processing/SpectrumList_3D": "3D (m/z, drift-time, intensity) reshaper. NOT PORTED — niche.",
    "analysis/spectrum_processing/SpectrumList_IonMobility": "Ion-mobility CCS calculator filter. NOT PORTED — needs ion-mobility model classes first.",
    "analysis/spectrum_processing/SpectrumList_PrecursorRecalculator": "Re-derive precursor m/z from MS1 (older approach than PrecursorRefine). NOT PORTED — superseded.",
    "analysis/spectrum_processing/MS2Deisotoper": "MS2 isotope-cluster deisotoper. Ported as Analysis/PeakFilters/Ms2Deisotoper.cs.",
    "analysis/spectrum_processing/MS2NoiseFilter": "MS2 noise-floor trimmer. Ported as Analysis/PeakFilters/Ms2NoiseFilter.cs.",
    "analysis/spectrum_processing/MzShiftFilter": "Apply a constant m/z shift. Ported as Analysis/SpectrumListMzShift.cs.",
    "analysis/spectrum_processing/PrecursorMassFilter": "ETD precursor / charge-reduced / neutral-loss removal. Ported as Analysis/PeakFilters/EtdPrecursorMassFilter.cs.",
    "analysis/spectrum_processing/PrecursorRecalculator": "Helper for SpectrumList_PrecursorRecalculator. NOT PORTED.",
    "analysis/spectrum_processing/PrecursorRecalculatorDefault": "Default precursor-recalc impl. NOT PORTED.",
    "analysis/spectrum_processing/ThresholdFilter": (
        "Per-spectrum intensity thresholder (count / count-after-ties / absolute / bpi-relative /\n"
        "tic-relative / tic-cutoff). Ported as Analysis/ThresholdFilter.cs."
    ),

    # =========================================================================
    # analysis/peakdetect
    # =========================================================================
    "analysis/peakdetect/PeakFinder": (
        "IPeakDetector + CwtPeakDetector + LocalMaximumPeakDetector.\n"
        "Ported as Analysis/PeakPicking/IPeakDetector.cs + CwtPeakDetector.cs +\n"
        "LocalMaximumPeakDetector.cs. Reference test fixtures (CwtPeakDetectorReferenceTests)\n"
        "run cpp-generated peak data through the C# port for parity."
    ),
    "analysis/peakdetect/PeakFitter": "Per-peak Gaussian/parabola fit. NOT PORTED — only feature-detector consumers use it.",
    "analysis/peakdetect/PeakExtractor": "Extract peaks above a noise floor (feature-detector helper). NOT PORTED.",
    "analysis/peakdetect/FeatureDetector": "MS1 feature (peakel) detector. NOT PORTED — research module.",
    "analysis/peakdetect/FeatureDetectorPeakel": "Peakel-based feature detector. NOT PORTED.",
    "analysis/peakdetect/FeatureDetectorSimple": "Greedy feature detector. NOT PORTED.",
    "analysis/peakdetect/FeatureModeler": "Gaussian-mixture model fitter for features. NOT PORTED.",
    "analysis/peakdetect/MZRTField": "(m/z, rt) coordinate type. NOT PORTED.",
    "analysis/peakdetect/Noise": "Noise estimation utilities. NOT PORTED.",
    "analysis/peakdetect/PeakFamilyDetector": "Charge-state-aware peak family grouping. NOT PORTED.",
    "analysis/peakdetect/PeakFamilyDetectorFT": "FT-MS-specific peak family detector. NOT PORTED.",
    "analysis/peakdetect/PeakelGrower": "Time-extension of MS1 peaks across scans. NOT PORTED.",
    "analysis/peakdetect/PeakelPicker": "Peakel selection from candidate set. NOT PORTED.",
    "analysis/peakdetect/msextract": "CLI driver. SKIPPED.",

    # =========================================================================
    # analysis/demux
    # =========================================================================
    "analysis/demux/DemuxSolver": (
        "Non-negative least-squares (NNLS) solver for DIA demultiplexing.\n"
        "Ported as Analysis/Demux/DemuxSolver.cs + Nnls.cs (~200 LOC; cpp-bit-equivalent\n"
        "active-set solver). Optimized with span-based memory access for 4-8x speedup vs\n"
        "naïve port."
    ),
    "analysis/demux/DemuxHelpers": "Helper functions for demux scoring/sorting. Ported as Analysis/Demux/DemuxHelpers.cs.",
    "analysis/demux/DemuxTypes": "Common demux types (DemuxWindow, DemuxIsolationWindow). Folded into Analysis/Demux/PrecursorMaskCodec.cs.",
    "analysis/demux/IDemultiplexer": "IDemultiplexer interface. Ported as Analysis/Demux/IDemultiplexer.cs.",
    "analysis/demux/MSXDemultiplexer": "MSX demultiplexer (Thermo MS2 multiplexing). Ported as Analysis/Demux/MsxDemultiplexer.cs.",
    "analysis/demux/OverlapDemultiplexer": "Overlapping-window DIA demultiplexer. Ported as Analysis/Demux/OverlapDemultiplexer.cs.",
    "analysis/demux/PrecursorMaskCodec": "Precursor-window indexing codec. Ported as Analysis/Demux/PrecursorMaskCodec.cs.",
    "analysis/demux/SpectrumPeakExtractor": "Per-scan peak extractor for demux input. Ported as Analysis/Demux/SpectrumPeakExtractor.cs.",
    "analysis/demux/IPrecursorMaskCodec": "Header-only interface. Subsumed by PrecursorMaskCodec.cs.",
    "analysis/demux/CubicHermiteSpline": "Cubic Hermite interpolation. NOT PORTED — no current consumer in the demux path.",
    "analysis/demux/IInterpolation": "Generic interpolator interface. NOT PORTED.",
    "analysis/demux/FSDemux": "Filesystem-backed demux debug mode. SKIPPED.",
    "analysis/demux/DemuxDebugReader": "Debug snapshot reader. SKIPPED.",
    "analysis/demux/DemuxDebugWriter": "Debug snapshot writer. SKIPPED.",
    "analysis/demux/DemuxTestData": "Hard-coded test fixtures. SKIPPED — replaced by file-based fixtures.",
    "analysis/demux/MatrixIO": "Eigen matrix I/O for debug snapshots. SKIPPED.",
    "analysis/demux/EnumConstantNotPresentException": "Trivial exception type. SKIPPED.",
    "analysis/demux/DemuxDataProcessingStrings": "String constants for DataProcessing entries. Ported inline into DemuxHelpers.cs.",
}

# ---------------------------------------------------------------------------
# Sub-area grouping. For each module, a list of (cluster_label, prefix_or_predicate).
# Files matching the predicate group together under that cluster. Files that
# don't match any explicit cluster go into a fallback "core" / "misc" cluster.
# ---------------------------------------------------------------------------

def _starts(prefixes):
    return lambda stem: any(stem.startswith(p) for p in prefixes)

SUBAREA = {
    "data/common": [
        ("CV / OBO",                  _starts(["cv", "CVTranslator", "obo", "Unimod"])),
        ("Param / Diff",              _starts(["ParamTypes", "diff_std"])),
        ("Index",                     _starts(["Index", "MemoryIndex", "BinaryIndex"])),
    ],
    "data/msdata": [
        ("Core model",                _starts(["MSData", "References", "Diff", "LegacyAdapter",
                                                "SpectrumInfo", "SpectrumIterator", "SpectrumListBase",
                                                "SpectrumListCache", "SpectrumListWrapper",
                                                "ChromatogramListBase"])),
        ("Encoding",                  _starts(["BinaryDataEncoder", "MSNumpress", "SHA1OutputObserver"])),
        ("Serializers / writers",     _starts(["Serializer_", "TextWriter"])),
        ("mzML stream parser",        _starts(["IO", "Index_mzML"])),
        ("SpectrumList_*",            _starts(["SpectrumList_"])),
        ("ChromatogramList_*",        _starts(["ChromatogramList_"])),
        ("Reader dispatch",           _starts(["Reader", "DefaultReaderList", "MSDataMerger",
                                                "MSDataFile"])),
        ("Misc",                      _starts(["RAMPAdapter", "MemoryMRUCache",
                                                "SpectrumWorkerThreads", "examples"])),
        ("HDF5 (mz5)",                lambda s: s.startswith("mz5/")),
        ("HDF5 (mzMLb)",              lambda s: s.startswith("mzmlb/")),
    ],
    "data/identdata": [
        ("Core model",                _starts(["IdentData", "References", "Diff"])),
        ("Reader dispatch",           _starts(["Reader", "DefaultReaderList", "MascotReader"])),
        ("Serializers",               _starts(["Serializer_", "DelimReader", "DelimWriter",
                                                "TextWriter"])),
        ("IO",                        _starts(["IO"])),
        ("Predicates / CV",           _starts(["MzidPredicates", "KwCVMap"])),
        ("Conversion / examples",     _starts(["Pep2MzIdent", "examples", "Version"])),
    ],
    "utility": [
        ("chemistry",                 lambda s: s.startswith("chemistry/")),
        ("math",                      lambda s: s.startswith("math/")),
        ("misc — BCL replacements",   lambda s: s.startswith("misc/") and STATUS_FILE.get(f"utility/{s}", "none") == "skipped"),
        ("misc — ported",             lambda s: s.startswith("misc/") and STATUS_FILE.get(f"utility/{s}", "none") != "skipped"),
        ("minimxml",                  lambda s: s.startswith("minimxml/")),
        ("proteome",                  lambda s: s.startswith("proteome/")),
        ("findmf",                    lambda s: s.startswith("findmf/")),
    ],
    "analysis": [
        ("spectrum_processing",       lambda s: s.startswith("spectrum_processing/")),
        ("peakdetect",                lambda s: s.startswith("peakdetect/")),
        ("demux",                     lambda s: s.startswith("demux/")),
        ("chromatogram_processing",   lambda s: s.startswith("chromatogram_processing/")),
        ("calibration",               lambda s: s.startswith("calibration/")),
        ("dia_umpire",                lambda s: s.startswith("dia_umpire/")),
        ("passive",                   lambda s: s.startswith("passive/")),
        ("frequency",                 lambda s: s.startswith("frequency/")),
        ("peptideid",                 lambda s: s.startswith("peptideid/")),
        ("proteome_processing",       lambda s: s.startswith("proteome_processing/")),
        ("findmf",                    lambda s: s.startswith("findmf/")),
        ("eharmony",                  lambda s: s.startswith("eharmony/")),
        ("common",                    lambda s: s.startswith("common/")),
    ],
}

# ---------------------------------------------------------------------------
# File discovery
# ---------------------------------------------------------------------------

TEST_RX = re.compile(r"(_?[Tt]est|TestData)$")
SKIP_BASENAMES = {"Version"}  # auto-generated boilerplate

def collect_source_files(module: str) -> list[str]:
    """Return stems (e.g. 'MzML', 'mz5/Configuration_mz5') of every .cpp/.hpp pair
    under PWIZ_ROOT/<module>/ that isn't a test fixture."""
    root = PWIZ_ROOT / module
    if not root.is_dir():
        return []
    stems: set[str] = set()
    for path in root.rglob("*.cpp"):
        rel = path.relative_to(root).with_suffix("")
        stem = str(rel).replace("\\", "/")
        if TEST_RX.search(stem.split("/")[-1]):
            continue
        if Path(stem).name in SKIP_BASENAMES:
            continue
        stems.add(stem)
    # Header-only files (no .cpp partner) — common for IPrecursorMaskCodec etc.
    for path in root.rglob("*.hpp"):
        rel = path.relative_to(root).with_suffix("")
        stem = str(rel).replace("\\", "/")
        if TEST_RX.search(stem.split("/")[-1]):
            continue
        if Path(stem).name in SKIP_BASENAMES:
            continue
        stems.add(stem)
    return sorted(stems)

# ---------------------------------------------------------------------------
# DOT emission
# ---------------------------------------------------------------------------

STATUS_COLORS = {
    "full":    ("#9ee5a3", "#2c7a32", "#0a3d10"),
    "partial": ("#ffe48a", "#a87000", "#4a3000"),
    "none":    ("#f5a8a8", "#a02020", "#3d0a0a"),
    "skipped": ("#dcdcdc", "#888888", "#555555"),
}

# Each node's href links to the cpp source on GitHub. Master is the long-lived
# pwiz development branch.
GITHUB_BLOB_BASE = "https://github.com/ProteoWizard/pwiz/blob/master/pwiz/"

# Fallback tooltips by status when no per-file detail is registered.
DEFAULT_TOOLTIP = {
    "full":    "Ported. No per-file detail recorded yet.",
    "partial": "Partially ported. No per-file detail recorded yet.",
    "none":    "Not yet ported. No specific reason recorded.",
    "skipped": "Won't port. No specific replacement recorded.",
}

def _dot_escape_string(s: str) -> str:
    """Escape a string for a DOT double-quoted attribute value.
    Newlines render as line breaks in SVG tooltips with the &#10; escape;
    raw \\n inside a DOT string would terminate the attribute."""
    return (
        s.replace("\\", "\\\\")
         .replace('"', '\\"')
         .replace("\n", "&#10;")
    )

def detail_for(module: str, stem: str, status: str) -> str:
    key = f"{module}/{stem}"
    return DETAIL_FILE.get(key, DEFAULT_TOOLTIP[status])

def github_url(module: str, stem: str) -> str:
    # Prefer .cpp if it exists, otherwise .hpp (some stems are header-only).
    cpp = PWIZ_ROOT / module / f"{stem}.cpp"
    hpp = PWIZ_ROOT / module / f"{stem}.hpp"
    suffix = "cpp" if cpp.exists() else ("hpp" if hpp.exists() else "cpp")
    return f"{GITHUB_BLOB_BASE}{module}/{stem}.{suffix}"

def node_id(module: str, stem: str) -> str:
    return "n_" + (module + "/" + stem).replace("/", "__").replace("-", "_").replace(".", "_")

def cluster_id(label: str) -> str:
    return "cluster_" + re.sub(r"[^A-Za-z0-9]+", "_", label)

def emit_module_dot(module: str, out_path: Path) -> None:
    stems = collect_source_files(module)
    if not stems:
        print(f"warning: no source files found under pwiz/{module}", file=sys.stderr)
        return

    # Bucket each stem into its sub-area cluster.
    rules = SUBAREA.get(module, [])
    buckets: dict[str, list[str]] = {}
    fallback_label = "Other"
    for stem in stems:
        matched = None
        for label, pred in rules:
            if pred(stem):
                matched = label
                break
        buckets.setdefault(matched or fallback_label, []).append(stem)

    # Count what we know about each bucket for ordering (full > partial > none).
    def status_of(stem: str) -> str:
        key = f"{module}/{stem}"
        return STATUS_FILE.get(key, "none")

    # Stable cluster ordering: walk SUBAREA's declared order first so the page
    # reads top-to-bottom in a sensible curated order, then any "Other" cluster
    # at the end.
    declared_order = [label for label, _ in rules if label in buckets]
    if "Other" in buckets and "Other" not in declared_order:
        declared_order.append("Other")
    for label in buckets:
        if label not in declared_order:
            declared_order.append(label)

    lines: list[str] = []
    lines.append("digraph pwiz_module_detail {")
    lines.append('  rankdir=TB;')
    lines.append('  labelloc=t;')
    lines.append(f'  label=<<b>pwiz/{module} — file-level port status</b><br/>'
                 '<font point-size="10">one node per .cpp/.hpp pair (test fixtures omitted); '
                 'each group is its own row, top-to-bottom</font>>;')
    lines.append('  fontname="Segoe UI";')
    lines.append('  node [shape=box, style="filled,rounded", fontname="Segoe UI", fontsize=10];')
    lines.append('  edge [color="#444"];')
    lines.append('  ranksep=0.6; nodesep=0.2;')
    lines.append('  compound=true;')
    lines.append('  newrank=true;')        # required: lets cluster-level rank=same constrain cross-cluster layout
    lines.append('  splines=false;')

    # Emit clusters in declared order. Within each cluster:
    #   - all nodes get the SAME rank (one horizontal row)
    #   - invisible edges between consecutive nodes pin left-to-right order
    # Between clusters: an invisible edge from the FIRST node of cluster N to
    # the FIRST node of cluster N+1 pushes cluster N+1 onto its own row below.
    first_node_per_cluster: list[str] = []
    for label in declared_order:
        members = buckets.get(label, [])
        if not members:
            continue
        cid = cluster_id(f"{module}_{label}")
        lines.append(f'  subgraph {cid} {{')
        lines.append(f'    label=<<b>{label}</b>>;')
        lines.append('    style="rounded,dashed";')
        lines.append('    color="#888";')
        lines.append('    fontname="Segoe UI";')
        lines.append('    fontsize=11;')
        lines.append('    margin=10;')
        lines.append('    rank=same;')
        nids = []
        for stem in members:
            status = status_of(stem)
            fill, stroke, text = STATUS_COLORS[status]
            display = stem.split("/")[-1]
            label_attr = f"<<s>{display}</s>>" if status == "skipped" else f'"{display}"'
            nid = node_id(module, stem)
            tip = _dot_escape_string(detail_for(module, stem, status))
            url = github_url(module, stem)
            # Graphviz writes tooltip into the SVG <title> child of the node's <g>
            # element, which browsers surface on mouseover. href + target=_top
            # makes the box a click-through to cpp source. Tooltip attaches to
            # both the cluster-internal <a> (URL) and the rect (tooltip).
            lines.append(
                f'    {nid} [label={label_attr}, '
                f'fillcolor="{fill}", color="{stroke}", fontcolor="{text}", '
                f'tooltip="{tip}", href="{url}", target="_blank"];'
            )
            nids.append(nid)
        # Chain invisible edges left-to-right within the cluster so order is stable.
        # constraint=false on these so they don't push other clusters around vertically.
        if len(nids) > 1:
            chain = " -> ".join(nids)
            lines.append(f'    {chain} [style=invis, constraint=false];')
        lines.append('  }')
        first_node_per_cluster.append(nids[0])

    # Vertical chain between clusters: invisible edge from each cluster's first
    # node to the next cluster's first node. minlen=1 keeps the gap tight.
    for prev, nxt in zip(first_node_per_cluster, first_node_per_cluster[1:]):
        lines.append(f'  {prev} -> {nxt} [style=invis, minlen=1];')

    # Legend (own row at bottom). Same single-row treatment.
    lines.append('  subgraph cluster_legend {')
    lines.append('    label=<<b>Status</b>>;')
    lines.append('    style="rounded";')
    lines.append('    color="#444";')
    lines.append('    fontname="Segoe UI";')
    lines.append('    fontsize=11;')
    lines.append('    margin=8;')
    lines.append('    rank=same;')
    legend_nids = []
    for s, display in [("full", "ported"), ("partial", "partial"),
                       ("none", "not yet ported"), ("skipped", "won't port (BCL/legacy)")]:
        fill, stroke, text = STATUS_COLORS[s]
        lbl = f"<<s>{display}</s>>" if s == "skipped" else f'"{display}"'
        lines.append(f'    legend_{s} [label={lbl}, fillcolor="{fill}", color="{stroke}", fontcolor="{text}"];')
        legend_nids.append(f"legend_{s}")
    if len(legend_nids) > 1:
        lines.append(f'    {" -> ".join(legend_nids)} [style=invis, constraint=false];')
    lines.append('  }')
    # Pin the legend below the last content cluster.
    if first_node_per_cluster:
        lines.append(f'  {first_node_per_cluster[-1]} -> legend_full [style=invis, minlen=1];')

    lines.append("}")
    out_path.write_text("\n".join(lines) + "\n", encoding="utf-8")

# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

MODULES = ["data/common", "data/msdata", "data/identdata", "utility", "analysis"]

def safe_name(module: str) -> str:
    return module.replace("/", "_").replace("-", "_")

def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        sys.exit(2)
    targets = MODULES if args[0] == "all" else [args[0]]
    for module in targets:
        out = OUT_DIR / f"module-detail-{safe_name(module)}.dot"
        emit_module_dot(module, out)
        print(f"wrote {out.relative_to(OUT_DIR.parent)}")

if __name__ == "__main__":
    main()
