using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.Deconvolution
{
    public class ChromGroupSet
    {
        private Dictionary<TransitionGroup, ChromGroupEntry> _index;
        public ChromGroupSet(IEnumerable<ChromGroupEntry> entries)
        {
            Entries = ImmutableList.ValueOf(entries);
            _index = new Dictionary<TransitionGroup, ChromGroupEntry>(new IdentityEqualityComparer<TransitionGroup>());
            foreach (var entry in Entries)
            {
                if (null != entry.TransitionGroupDocNode)
                {
                    _index.Add(entry.TransitionGroupDocNode.TransitionGroup, entry);
                }
            }
        }

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
    }
}
