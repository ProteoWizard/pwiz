using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;


namespace pwiz.Skyline.Model.Databinding.Collections
{
    public class CandidatePeakGroups : AbstractRowSource
    {
        private IdentityPath _precursorIdentityPath;
        private int _replicateIndex;
        private ChromFileInfoId _chromFileInfoId;
        private SkylineDataSchema _dataSchema;
        private readonly IDocumentChangeListener _documentChangeListener;


        public CandidatePeakGroups(SkylineDataSchema dataSchema)
        {
            _dataSchema = dataSchema;
            _documentChangeListener = new DocumentChangeListener(this);
        }

        protected override void BeforeFirstListenerAdded()
        {
            base.BeforeFirstListenerAdded();
            _dataSchema.Listen(_documentChangeListener);
        }

        protected override void AfterLastListenerRemoved()
        {
            _dataSchema.Unlisten(_documentChangeListener);
            base.AfterLastListenerRemoved();
        }

        public IdentityPath PrecursorIdentityPath
        {
            get { return _precursorIdentityPath; }
            set
            {
                if (Equals(PrecursorIdentityPath, value))
                {
                    return;
                }

                _precursorIdentityPath = value;
                FireListChanged();
            }
        }

        public int ReplicateIndex
        {
            get
            {
                return _replicateIndex;
            }
            set
            {
                if (ReplicateIndex == value)
                {
                    return;
                }

                _replicateIndex = value;
                FireListChanged();
            }
        }

        public ChromFileInfoId ChromFileInfoId
        {
            get
            {
                return _chromFileInfoId;
            }
            set
            {
                if (ReferenceEquals(ChromFileInfoId, value))
                {
                    return;
                }

                _chromFileInfoId = value;
                FireListChanged();
            }
        }

        public override IEnumerable GetItems()
        {
            var candidatePeakGroups = new List<CandidatePeakGroup>();
            var featureCalculator = GetFeatureCalculator();
            if (featureCalculator == null)
            {
                return candidatePeakGroups;
            }

            var precursor = new Precursor(_dataSchema, PrecursorIdentityPath);
            var precursorResult = new PrecursorResult(precursor,
                new ResultFile(new Replicate(_dataSchema, ReplicateIndex), _chromFileInfoId, 0));

            var transitionGroup = (TransitionGroup) PrecursorIdentityPath.Child;
            foreach (var peakGroupData in featureCalculator.GetCandidatePeakGroups(transitionGroup))
            {
                candidatePeakGroups.Add(new CandidatePeakGroup(precursorResult, peakGroupData));
            }

            if (!candidatePeakGroups.Any(peak => peak.Chosen))
            {
                var chosenPeak = featureCalculator.GetChosenPeakGroupData(transitionGroup);
                if (chosenPeak != null)
                {
                    candidatePeakGroups.Add(new CandidatePeakGroup(precursorResult, chosenPeak));
                }
            }

            return candidatePeakGroups.OrderBy(peak => Tuple.Create(peak.PeakGroupStartTime, peak.PeakGroupEndTime));
        }

        private OnDemandFeatureCalculator GetFeatureCalculator()
        {
            var document = _dataSchema.Document;
            if (!document.Settings.HasResults || ReplicateIndex < 0 ||
                ReplicateIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return null;
            }

            if (_precursorIdentityPath == null)
            {
                return null;
            }

            var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[ReplicateIndex];
            var chromFileInfo = chromatogramSet.GetFileInfo(ChromFileInfoId);
            if (chromFileInfo == null)
            {
                chromFileInfo = chromatogramSet.MSDataFileInfos.First();
                _chromFileInfoId = chromFileInfo.FileId;
            }
            var peptideDocNode = (PeptideDocNode) document.FindNode(_precursorIdentityPath.Parent);
            if (peptideDocNode == null)
            {
                return null;
            }

            return new OnDemandFeatureCalculator(FeatureCalculators.ALL, document, peptideDocNode, ReplicateIndex,
                chromFileInfo);
        }

        private class DocumentChangeListener : IDocumentChangeListener
        {
            private readonly CandidatePeakGroups _candidatePeakGroups;
            public DocumentChangeListener(CandidatePeakGroups candidatePeakGroups)
            {
                _candidatePeakGroups = candidatePeakGroups;
            }

            public void DocumentOnChanged(object sender, DocumentChangedEventArgs args)
            {
                _candidatePeakGroups.FireListChanged();
            }
        }

    }
}
