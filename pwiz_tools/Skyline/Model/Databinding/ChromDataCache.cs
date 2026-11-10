using System;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding
{
    public class ChromDataCache
    {
        private readonly object _lock = new object();
        private PrecursorChromatograms _withoutPoints;
        private PrecursorChromatograms _withPoints;
        private Tuple<Key, MsDataFileScanIds> _scanIds;

        public MsDataFileScanIds GetScanIds(SrmDocument document, MsDataFileUri msDataFileUri)
        {
            var key = new Key(document, null, msDataFileUri, IdentityPath.ROOT);
            if (_scanIds != null && Equals(key, _scanIds.Item1))
            {
                return _scanIds.Item2;
            }

            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                return null;
            }

            var msDataFileScanIds = measuredResults.LoadMSDataFileScanIds(msDataFileUri, out _);
            _scanIds = Tuple.Create(key, msDataFileScanIds);
            return msDataFileScanIds;
        }

        /// <summary>
        /// Returns the chromatogram group of a precursor in one file of a replicate. The chromatogram groups of a single
        /// precursor are kept for every replicate and file that has been asked for, so going back and forth between the
        /// files of the same precursor does not read them again.
        /// </summary>
        public ChromatogramGroupInfo GetChromatogramGroupInfo(SrmDocument document,
            ChromatogramSet chromatogramSet, MsDataFileUri filePath,
            IdentityPath precursorIdentityPath, bool loadPoints)
        {
            // Report exporters can calculate rows on several threads at once
            lock (_lock)
            {
                var precursorChromatograms = loadPoints ? _withPoints : _withoutPoints;
                if (precursorChromatograms == null || !precursorChromatograms.IsFor(document, precursorIdentityPath))
                {
                    precursorChromatograms = new PrecursorChromatograms(document, precursorIdentityPath, loadPoints);
                    if (loadPoints)
                    {
                        _withPoints = precursorChromatograms;
                    }
                    else
                    {
                        _withoutPoints = precursorChromatograms;
                    }
                }
                return precursorChromatograms.GetChromatogramGroupInfo(document, chromatogramSet, filePath);
            }
        }

        /// <summary>
        /// The chromatogram groups of one precursor, by replicate and then by file.
        /// </summary>
        private class PrecursorChromatograms
        {
            private readonly object _documentReference;
            private readonly IdentityPath _precursorIdentityPath;
            private readonly bool _loadPoints;
            private readonly Dictionary<ChromatogramSet, Dictionary<MsDataFileUri, ChromatogramGroupInfo>> _byReplicate
                = new Dictionary<ChromatogramSet, Dictionary<MsDataFileUri, ChromatogramGroupInfo>>(ReferenceValue.EQUALITY_COMPARER);

            public PrecursorChromatograms(SrmDocument document, IdentityPath precursorIdentityPath, bool loadPoints)
            {
                _documentReference = document.ReferenceId;
                _precursorIdentityPath = precursorIdentityPath;
                _loadPoints = loadPoints;
            }

            public bool IsFor(SrmDocument document, IdentityPath precursorIdentityPath)
            {
                return ReferenceEquals(_documentReference, document.ReferenceId) &&
                       _precursorIdentityPath.Equals(precursorIdentityPath);
            }

            public ChromatogramGroupInfo GetChromatogramGroupInfo(SrmDocument document, ChromatogramSet chromatogramSet,
                MsDataFileUri filePath)
            {
                if (!_byReplicate.TryGetValue(chromatogramSet, out var byFile))
                {
                    byFile = LoadChromatogramInfos(document, chromatogramSet);
                    _byReplicate.Add(chromatogramSet, byFile);
                }
                byFile.TryGetValue(filePath, out var chromatogramGroupInfo);
                return chromatogramGroupInfo;
            }

            private Dictionary<MsDataFileUri, ChromatogramGroupInfo> LoadChromatogramInfos(SrmDocument document,
                ChromatogramSet chromatogramSet)
            {
                var byFile = new Dictionary<MsDataFileUri, ChromatogramGroupInfo>();
                var peptideDocNode = (PeptideDocNode) document.FindNode(_precursorIdentityPath.Parent);
                var precursorDocNode = (TransitionGroupDocNode) peptideDocNode.FindNode(_precursorIdentityPath.Child);
                var measuredResults = document.Settings.MeasuredResults;
                var tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                if (!measuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, precursorDocNode, tolerance,
                        out var chromatogramGroupInfos))
                {
                    return byFile;
                }
                foreach (var chromatogramGroupInfo in chromatogramGroupInfos)
                {
                    // When a file has more than one chromatogram group, the first one is used
                    if (byFile.ContainsKey(chromatogramGroupInfo.FilePath))
                    {
                        continue;
                    }
                    if (!_loadPoints)
                    {
                        chromatogramGroupInfo.DiscardData();
                    }
                    byFile.Add(chromatogramGroupInfo.FilePath, chromatogramGroupInfo);
                }
                return byFile;
            }
        }

        private class Key
        {
            private object _documentReference;
            private ChromatogramSet _chromatogramSet;
            private MsDataFileUri _filePath;
            private IdentityPath _precursorIdentityPath;

            public Key(SrmDocument document, ChromatogramSet chromatogramSet, MsDataFileUri filePath, IdentityPath precursorIdentityPath)
            {
                _documentReference = document.ReferenceId;
                _chromatogramSet = chromatogramSet;
                _filePath = filePath;
                _precursorIdentityPath = precursorIdentityPath;
            }

            private bool Equals(Key other)
            {
                return ReferenceEquals(_documentReference, other._documentReference) && ReferenceEquals(_chromatogramSet, other._chromatogramSet)
                    && _filePath.Equals(other._filePath) && _precursorIdentityPath.Equals(other._precursorIdentityPath);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Key) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = _filePath.GetHashCode();
                    hashCode = (hashCode * 397) ^ _precursorIdentityPath.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}
