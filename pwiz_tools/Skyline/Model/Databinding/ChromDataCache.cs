using System.Collections.Generic;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Remembers the chromatogram groups of the most recently used precursors, in every replicate and file
    /// that has been asked for, and the scan ids of one file, so that report cells for the same precursor do
    /// not read them again. Several precursors are kept because report exporters calculate rows on several
    /// threads at once, and those threads are usually working on different precursors.
    /// </summary>
    public class ChromDataCache
    {
        // Each thread calculating report rows (see ParallelEx) works on one precursor at a time. Twice as many
        // are kept so that a precursor still in use is not forgotten when the other threads move on to new ones.
        private static readonly int MAX_PRECURSORS = ParallelEx.GetThreadCount() * 2;

        private readonly object _lock = new object();
        private object _documentReference;
        private readonly Dictionary<IdentityPath, Dictionary<(string Replicate, MsDataFileUri File, bool LoadPoints), ChromatogramGroupInfo>> _precursors =
            new Dictionary<IdentityPath, Dictionary<(string Replicate, MsDataFileUri File, bool LoadPoints), ChromatogramGroupInfo>>();
        private readonly Queue<IdentityPath> _precursorOrder = new Queue<IdentityPath>();
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
        /// Returns the chromatogram group of a precursor in one file of a replicate. When more precursors than
        /// the cache holds have been asked for, the groups of the one asked for first are forgotten; asking for
        /// a different document forgets them all.
        /// </summary>
        /// <param name="loadPoints">False to get a group whose time and intensity points have been discarded,
        /// which is cheaper to hold on to.</param>
        public ChromatogramGroupInfo GetChromatogramGroupInfo(SrmDocument document,
            ChromatogramSet chromatogramSet, MsDataFileUri filePath,
            IdentityPath precursorIdentityPath, bool loadPoints)
        {
            lock (_lock)
            {
                if (!ReferenceEquals(_documentReference, document.ReferenceId))
                {
                    _documentReference = document.ReferenceId;
                    _precursors.Clear();
                    _precursorOrder.Clear();
                }
                if (!_precursors.TryGetValue(precursorIdentityPath, out var groups))
                {
                    if (_precursorOrder.Count >= MAX_PRECURSORS)
                    {
                        _precursors.Remove(_precursorOrder.Dequeue());
                    }
                    groups = new Dictionary<(string Replicate, MsDataFileUri File, bool LoadPoints), ChromatogramGroupInfo>();
                    _precursors.Add(precursorIdentityPath, groups);
                    _precursorOrder.Enqueue(precursorIdentityPath);
                }
                var key = (chromatogramSet.Name, filePath, loadPoints);
                if (!groups.TryGetValue(key, out var chromatogramGroupInfo))
                {
                    LoadReplicate(document, precursorIdentityPath, chromatogramSet, loadPoints, groups);
                    if (!groups.TryGetValue(key, out chromatogramGroupInfo))
                    {
                        // Remember that this file has no chromatogram group, so it is not looked for again
                        groups.Add(key, null);
                    }
                }
                return chromatogramGroupInfo;
            }
        }

        /// <summary>
        /// Adds the precursor's chromatogram groups in every file of the replicate. When a file has more than
        /// one, the first one is used.
        /// </summary>
        private static void LoadReplicate(SrmDocument document, IdentityPath precursorIdentityPath,
            ChromatogramSet chromatogramSet, bool loadPoints,
            Dictionary<(string Replicate, MsDataFileUri File, bool LoadPoints), ChromatogramGroupInfo> groups)
        {
            var peptideDocNode = (PeptideDocNode) document.FindNode(precursorIdentityPath.Parent);
            var precursorDocNode = (TransitionGroupDocNode) peptideDocNode.FindNode(precursorIdentityPath.Child);
            var tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            if (!document.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, precursorDocNode,
                    tolerance, out var chromatogramGroupInfos))
            {
                return;
            }
            foreach (var chromatogramGroupInfo in chromatogramGroupInfos)
            {
                var key = (chromatogramSet.Name, chromatogramGroupInfo.FilePath, loadPoints);
                if (groups.ContainsKey(key))
                {
                    continue;
                }
                if (!loadPoints)
                {
                    chromatogramGroupInfo.DiscardData();
                }
                groups.Add(key, chromatogramGroupInfo);
            }
        }
    }
}
