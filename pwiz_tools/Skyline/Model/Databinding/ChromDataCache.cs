using System;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding
{
    public class ChromDataCache
    {
        private Tuple<Key, ChromatogramGroupInfo> _withoutPoints;
        private Tuple<Key, ChromatogramGroupInfo> _withPoints;
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

        public ChromatogramGroupInfo GetChromatogramGroupInfo(SrmDocument document, 
            ChromatogramSet chromatogramSet, MsDataFileUri filePath,
            IdentityPath precursorIdentityPath, bool loadPoints)
        {
            var key = new Key(document, chromatogramSet, filePath, precursorIdentityPath);
            var cacheSlot = loadPoints ? _withPoints : _withoutPoints;
            if (cacheSlot != null && Equals(key, cacheSlot.Item1))
            {
                return cacheSlot.Item2;
            }
            var chromatogramGroupInfo = LoadChromatogramInfo(document, chromatogramSet, filePath, precursorIdentityPath);
            if (!loadPoints)
            {
                chromatogramGroupInfo?.DiscardData();
            }
            cacheSlot = Tuple.Create(key, chromatogramGroupInfo);
            if (loadPoints)
            {
                _withPoints = cacheSlot;
            }
            else
            {
                _withoutPoints = cacheSlot;
            }
            return chromatogramGroupInfo;
        }

        private ChromatogramGroupInfo LoadChromatogramInfo(SrmDocument document, ChromatogramSet chromatogramSet, MsDataFileUri filePath,
            IdentityPath precursorIdentityPath)
        {
            var peptideDocNode = (PeptideDocNode) document.FindNode(precursorIdentityPath.Parent);
            var precursorDocNode = (TransitionGroupDocNode) peptideDocNode.FindNode(precursorIdentityPath.Child);
            ChromatogramGroupInfo[] chromatogramGroupInfos;
            var measuredResults = document.Settings.MeasuredResults;
            var tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            if (!measuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, precursorDocNode, tolerance, out chromatogramGroupInfos))
            {
                return null;
            }
            foreach (var chromatogramGroupInfo in chromatogramGroupInfos)
            {
                if (Equals(chromatogramGroupInfo.FilePath, filePath))
                {
                    return chromatogramGroupInfo;
                }
            }
            return null;
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
