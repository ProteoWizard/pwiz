using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Deconvolution
{
    public class ChromGroupSet
    {
        private static readonly IdentityEqualityComparer<TransitionGroup> TRANSITION_GROUP_COMPARER = new IdentityEqualityComparer<TransitionGroup>();
        private Dictionary<TransitionGroup, ChromGroupEntry> _index;
        private Dictionary<MsDataFileUri, ImmutableList<DeconvolutedChromatograms>> _deconvolutedChromatograms = new Dictionary<MsDataFileUri, ImmutableList<DeconvolutedChromatograms>>();
        public ChromGroupSet(SrmSettings settings, IEnumerable<ChromGroupEntry> entries)
        {
            Settings = settings;
            Entries = ImmutableList.ValueOf(entries);
            _index = new Dictionary<TransitionGroup, ChromGroupEntry>(TRANSITION_GROUP_COMPARER);
            foreach (var entry in Entries)
            {
                if (null != entry.TransitionGroupDocNode)
                {
                    _index.Add(entry.TransitionGroupDocNode.TransitionGroup, entry);
                }
            }
        }

        public SrmSettings Settings { get; private set; }

        public string GetFileNameLabel()
        {
            var distinctFilePaths = Entries.Select(entry => entry.ChromatogramGroupInfo.FilePath).Distinct().ToList();
            if (distinctFilePaths.Count == 0)
            {
                return string.Empty;
            }
            if (distinctFilePaths.Count == 1)
            {
                var filePath = distinctFilePaths[0];

                string sampleName = filePath.GetSampleName();
                return string.IsNullOrEmpty(sampleName) ? filePath.GetFileName() : sampleName;
            }

            return Resources.GraphChromatogram_UpdateToolbar_All;
        }

        public MsDataFileUri FirstFilePath
        {
            get
            {
                return Entries.FirstOrDefault()?.ChromatogramGroupInfo.FilePath;
            }
        }

        public IEnumerable<MsDataFileUri> DistinctFilePaths
        {
            get
            {
                return Entries.Select(entry => entry.ChromatogramGroupInfo.FilePath).Distinct();
            }
        }

        public ImmutableList<ChromGroupEntry> Entries
        {
            get;
            private set;
        }

        public ChromGroupEntry FindEntry(TransitionGroup transitionGroup)
        {
            _index.TryGetValue(transitionGroup, out ChromGroupEntry entry);
            return entry;
        }

        public class ChromGroupEntry
        {
            public ChromGroupEntry(IdentityPath identityPath, 
                PeptideDocNode peptideDocNode, 
                TransitionGroupDocNode transitionGroupDocNode,
                ChromatogramGroupInfo chromatogramGroupInfo)
            {
                PeptideDocNode = peptideDocNode;
                TransitionGroupDocNode = transitionGroupDocNode;
                ChromatogramGroupInfo = chromatogramGroupInfo;
            }

            public IdentityPath IdentityPath { get; private set; }
            public PeptideDocNode PeptideDocNode { get; private set; }
            public TransitionGroupDocNode TransitionGroupDocNode { get; private set; }
            public ChromatogramGroupInfo ChromatogramGroupInfo { get; private set; }
        }

        public IList<DeconvolutedChromatograms> GetDeconvolutedChromatograms(MsDataFileUri dataFileUri)
        {
            if (_deconvolutedChromatograms.TryGetValue(dataFileUri, out var deconvolutedChromatograms))
            {
                return deconvolutedChromatograms;
            }
            var deconvoluter = new Deconvoluter(Settings);
            var deconvolutionKeys = new List<DeconvolutionKey>();
            var chromatogramGroupInfos = new List<ChromatogramGroupInfo>();
            foreach (var entry in Entries)
            {
                if (entry.TransitionGroupDocNode == null)
                {
                    continue;
                }
                if (!Equals(entry.ChromatogramGroupInfo.FilePath, dataFileUri))
                {
                    continue;
                }
                deconvolutionKeys.Add(deconvoluter.MakeDeconvolutionKey(entry.PeptideDocNode, entry.TransitionGroupDocNode));
                chromatogramGroupInfos.Add(entry.ChromatogramGroupInfo);
            }

            deconvolutedChromatograms = ImmutableList.ValueOf(
                deconvoluter.DeconvoluteChromatograms(chromatogramGroupInfos, deconvolutionKeys));
            _deconvolutedChromatograms[dataFileUri] = deconvolutedChromatograms;
            return deconvolutedChromatograms;
        }

        /// <summary>
        /// For all of the precursor transitions in the list, replace the ChromatogramInfo with either null
        /// or the deconvolute chromatogram
        /// </summary>
        public void DeconvoluteChromatograms(IList<TransitionDocNode> transitions, IList<ChromatogramInfo> chromatogramInfos)
        {
            Assume.AreEqual(transitions.Count, chromatogramInfos.Count);
            var precursorEntries = new List<Tuple<int, ChromGroupEntry, TransitionDocNode>>();
            for (int i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (!transition.IsMs1)
                {
                    continue;
                }

                var chromGroupEntry = FindEntry(transition.Transition.Group);
                if (chromGroupEntry == null)
                {
                    continue;
                }
                precursorEntries.Add(Tuple.Create(i, chromGroupEntry, transition));
            }

            if (!precursorEntries.Any())
            {
                return;
            }

            foreach (var grouping in precursorEntries.ToLookup(tuple => tuple.Item2.ChromatogramGroupInfo.FilePath))
            {
                var deconvolutedChromatograms = GetDeconvolutedChromatograms(grouping.Key);
                foreach (var precursorGroup in grouping.ToLookup(precursorEntry=>precursorEntry.Item3.Transition.Group, TRANSITION_GROUP_COMPARER))
                {
                    ReplaceChromatogramInfos(deconvolutedChromatograms, chromatogramInfos, precursorGroup.Key, precursorGroup);
                }
            }
        }

        private void ReplaceChromatogramInfos(
            IEnumerable<DeconvolutedChromatograms> deconvolutedChromatograms,
            IList<ChromatogramInfo> chromatogramInfos,
            TransitionGroup transitionGroup,
            IEnumerable<Tuple<int, ChromGroupEntry, TransitionDocNode>> precursorTransitionTuples)
        {
            var timeIntensities = deconvolutedChromatograms.SelectMany(dc=>dc.Chromatograms).FirstOrDefault(tuple =>
                ReferenceEquals(tuple.Item1.TransitionGroupDocNode.TransitionGroup, transitionGroup))?.Item2;
            if (timeIntensities == null)
            {
                return;
            }
            var orderedEntries = precursorTransitionTuples.OrderBy(entry => entry.Item3,
                Comparer<TransitionDocNode>.Create(ComparePrecursorTransitions)).ToList();
            var bestEntry = orderedEntries[0];
            var originalChromInfo = chromatogramInfos[bestEntry.Item1];
            var chromatogramInfo = new ChromatogramInfo(originalChromInfo.GroupInfo, 0)
            {
                TimeIntensities = timeIntensities
            };
            chromatogramInfos[bestEntry.Item1] = chromatogramInfo;
            foreach (var entry in orderedEntries.Skip(1))
            {
                chromatogramInfos[entry.Item1] = null;
            }
        }

        private int ComparePrecursorTransitions(TransitionDocNode transition1, TransitionDocNode transition2)
        {
            int result = -transition1.ExplicitQuantitative.CompareTo(transition2.ExplicitQuantitative);
            if (result == 0)
            {
                result = Math.Sign(transition1.Transition.MassIndex)
                    .CompareTo(Math.Sign(transition2.Transition.MassIndex));
            }

            if (result == 0)
            {
                result = transition1.Transition.MassIndex.CompareTo(transition2.Transition.MassIndex);
            }

            return result;
        }
    }
}
