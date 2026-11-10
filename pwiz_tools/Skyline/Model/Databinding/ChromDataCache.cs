using System.Collections.Generic;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Remembers the chromatogram groups of one precursor, in every replicate and file that has been asked
    /// for, and the scan ids of one file, so that consecutive report cells for the same precursor do not
    /// read them again.
    /// </summary>
    public class ChromDataCache
    {
        private readonly object _lock = new object();
        private object _documentReference;
        private IdentityPath _precursorIdentityPath;
        private readonly Dictionary<(string Replicate, MsDataFileUri File, bool LoadPoints), ChromatogramGroupInfo> _groups =
            new Dictionary<(string Replicate, MsDataFileUri File, bool LoadPoints), ChromatogramGroupInfo>();
        private object _scanIdsDocumentReference;
        private MsDataFileUri _scanIdsFile;
        private MsDataFileScanIds _scanIds;

        public MsDataFileScanIds GetScanIds(SrmDocument document, MsDataFileUri msDataFileUri)
        {
            lock (_lock)
            {
                if (ReferenceEquals(_scanIdsDocumentReference, document.ReferenceId) && Equals(_scanIdsFile, msDataFileUri))
                {
                    return _scanIds;
                }

                var measuredResults = document.MeasuredResults;
                if (measuredResults == null)
                {
                    return null;
                }

                _scanIds = measuredResults.LoadMSDataFileScanIds(msDataFileUri, out _);
                _scanIdsDocumentReference = document.ReferenceId;
                _scanIdsFile = msDataFileUri;
                return _scanIds;
            }
        }

        /// <summary>
        /// Returns the chromatogram group of a precursor in one file of a replicate. Asking for a different
        /// precursor, or a different document, forgets the groups of the previous one.
        /// </summary>
        /// <param name="loadPoints">False to get a group whose time and intensity points have been discarded,
        /// which is cheaper to hold on to.</param>
        public ChromatogramGroupInfo GetChromatogramGroupInfo(SrmDocument document,
            ChromatogramSet chromatogramSet, MsDataFileUri filePath,
            IdentityPath precursorIdentityPath, bool loadPoints)
        {
            // Report exporters can calculate rows on several threads at once
            lock (_lock)
            {
                if (!ReferenceEquals(_documentReference, document.ReferenceId) ||
                    !precursorIdentityPath.Equals(_precursorIdentityPath))
                {
                    _documentReference = document.ReferenceId;
                    _precursorIdentityPath = precursorIdentityPath;
                    _groups.Clear();
                }
                var key = (chromatogramSet.Name, filePath, loadPoints);
                if (!_groups.TryGetValue(key, out var chromatogramGroupInfo))
                {
                    LoadReplicate(document, chromatogramSet, loadPoints);
                    if (!_groups.TryGetValue(key, out chromatogramGroupInfo))
                    {
                        // Remember that this file has no chromatogram group, so it is not looked for again
                        _groups.Add(key, null);
                    }
                }
                return chromatogramGroupInfo;
            }
        }

        /// <summary>
        /// Adds the precursor's chromatogram groups in every file of the replicate. When a file has more than
        /// one, the first one is used.
        /// </summary>
        private void LoadReplicate(SrmDocument document, ChromatogramSet chromatogramSet, bool loadPoints)
        {
            var peptideDocNode = (PeptideDocNode) document.FindNode(_precursorIdentityPath.Parent);
            var precursorDocNode = (TransitionGroupDocNode) peptideDocNode.FindNode(_precursorIdentityPath.Child);
            var tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            if (!document.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, precursorDocNode,
                    tolerance, out var chromatogramGroupInfos))
            {
                return;
            }
            foreach (var chromatogramGroupInfo in chromatogramGroupInfos)
            {
                var key = (chromatogramSet.Name, chromatogramGroupInfo.FilePath, loadPoints);
                if (_groups.ContainsKey(key))
                {
                    continue;
                }
                if (!loadPoints)
                {
                    chromatogramGroupInfo.DiscardData();
                }
                _groups.Add(key, chromatogramGroupInfo);
            }
        }
    }
}
