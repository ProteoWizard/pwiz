using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model
{
    public class PeptideSettingsDiff
    {
        private Dictionary<ChromatogramListKey, ImmutableList<ChromatogramGroupInfo>> _chromatogramLists =
            new Dictionary<ChromatogramListKey, ImmutableList<ChromatogramGroupInfo>>();
        public PeptideSettingsDiff(SrmSettings settings, PeptideDocNode nodePep) 
            : this(settings, nodePep, new SrmSettingsDiff(settings, true))
        {
        }
        public PeptideSettingsDiff(SrmSettings settingsNew, PeptideDocNode nodePep, SrmSettingsDiff diff) 
            : this(settingsNew, nodePep, nodePep.ExplicitMods, diff)
        {
        }

        public PeptideSettingsDiff(SrmSettings settingsNew, PeptideDocNode nodePep, ExplicitMods mods,
            SrmSettingsDiff diff)
        {
            SettingsNew = settingsNew;
            NodePep = nodePep;
            ExplicitMods = mods;
            SrmSettingsDiff = diff;
        }

        public PeptideSettingsDiff ChangeSettingsDiff(SrmSettingsDiff diffNew)
        {
            var peptideSettingsDiff = (PeptideSettingsDiff) MemberwiseClone();
            peptideSettingsDiff.SrmSettingsDiff = diffNew;
            return peptideSettingsDiff;
        }

        public SrmSettings SettingsNew { get; }

        public PeptideDocNode NodePep { get; }
        public ExplicitMods ExplicitMods { get; }
        public SrmSettingsDiff SrmSettingsDiff { get; private set; }

        public bool MustReadAllChromatograms()
        {
            if (null != SettingsNew.PeptideSettings.Integration.ResultsHandler)
            {
                return true;
            }

            var settingsOld = SrmSettingsDiff.SettingsOld;
            if (settingsOld == null)
            {
                return true;
            }

            if (SettingsNew.TransitionSettings.Instrument.MzMatchTolerance !=
                settingsOld.TransitionSettings.Instrument.MzMatchTolerance)
            {
                return true;
            }

            return false;
        }

        private void ReadPeaksForAllReplicates(TransitionGroupDocNode transitionGroupDocNode)
        {
            var allChromatogramGroupInfos =
                SettingsNew.MeasuredResults.LoadChromatogramsForAllReplicates(NodePep, transitionGroupDocNode,
                    MzMatchTolerance);
            ChromatogramGroupInfo.LoadPeaksForAll(allChromatogramGroupInfos.SelectMany(list => list), false);
            for (int replicateIndex = 0; replicateIndex < allChromatogramGroupInfos.Count; replicateIndex++)
            {
                var key = new ChromatogramListKey(transitionGroupDocNode.TransitionGroup, replicateIndex);
                _chromatogramLists[key] = ImmutableList.ValueOf(allChromatogramGroupInfos[replicateIndex]);
            }
        }

        private float MzMatchTolerance
        {
            get { return (float) SettingsNew.TransitionSettings.Instrument.MzMatchTolerance; }
        }

        public ImmutableList<ChromatogramGroupInfo> LoadChromatograms(int replicateIndex, TransitionGroupDocNode transitionGroupDocNode)
        {
            if (!SettingsNew.HasResults || replicateIndex < 0 ||
                replicateIndex >= SettingsNew.MeasuredResults.Chromatograms.Count)
            {
                return ImmutableList<ChromatogramGroupInfo>.EMPTY;
            }
            var key = new ChromatogramListKey(transitionGroupDocNode.TransitionGroup, replicateIndex);
            if (_chromatogramLists.TryGetValue(key, out var list))
            {
                return list;
            }

            if (MustReadAllChromatograms())
            {
                ReadPeaksForAllReplicates(transitionGroupDocNode);
                if (_chromatogramLists.TryGetValue(key, out list))
                {
                    return list;
                }
            }

            var measuredResults = SettingsNew.MeasuredResults;
            var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
            measuredResults.TryLoadChromatogram(chromatogramSet, NodePep, transitionGroupDocNode, MzMatchTolerance,
                out var arrayChromGroupInfo);
            list = ImmutableList.ValueOfOrEmpty(arrayChromGroupInfo);
            _chromatogramLists[key] = list;
            return list;
        }

        public OnDemandFeatureCalculator GetOnDemandFeatureCalculator(int replicateIndex, ChromFileInfo chromFileInfo, PeptideDocNode peptideDocNode)
        {
            PeptideGroup peptideGroup = peptideDocNode.Peptide.FastaSequence ?? new PeptideGroup(false);
            var peptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, Annotations.EMPTY, ProteinMetadata.EMPTY,
                new[] {peptideDocNode}, false);
            var srmDocument = (SrmDocument) new SrmDocument(SettingsNew).ChangeChildren(ImmutableList.Singleton<DocNode>(peptideGroupDocNode));
            return new OnDemandFeatureCalculator(FeatureCalculators.ALL, srmDocument, peptideDocNode, replicateIndex,
                chromFileInfo, LoadChromatograms);
        }

        private class ChromatogramListKey
        {
            public ChromatogramListKey(TransitionGroup transitionGroup, int replicateIndex)
            {
                TransitionGroup = transitionGroup;
                ReplicateIndex = replicateIndex;
            }

            public TransitionGroup TransitionGroup { get; }
            public int ReplicateIndex { get; }

            protected bool Equals(ChromatogramListKey other)
            {
                return ReferenceEquals(TransitionGroup, other.TransitionGroup) &&
                       ReplicateIndex == other.ReplicateIndex;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ChromatogramListKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(TransitionGroup) * 397) ^ ReplicateIndex;
                }
            }
        }

        public PeptideDocNode RecalculateScores(PeptideDocNode peptideDocNode)
        {
            if (peptideDocNode.Results == null)
            {
                return peptideDocNode;
            }

        }

        private PeptideDocNode RecalculateScores(int replicateIndex, PeptideDocNode peptideDocNode)
        {

        }

        private bool CanUseOldScores(int replicateIndex, PeptideDocNode peptideDocNode)
        {
            var measuredResults = SettingsNew.MeasuredResults;
            if (measuredResults.HasNewChromatogramData(replicateIndex))
            {
                return true;
            }

        }
    }
}
