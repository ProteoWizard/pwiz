using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Databinding
{
    public class CandidatePeakGroupFactory
    {
        public CandidatePeakGroupFactory(CancellationToken cancellationToken, SkylineDataSchema dataSchema)
        {
            CancellationToken = cancellationToken;
            DataSchema = dataSchema;
        }
        
        public CancellationToken CancellationToken { get; }
        public SkylineDataSchema DataSchema { get; }

        public IEnumerable<CandidatePeakGroup> GetCandidatePeakGroups(IdentityPath peptideIdentityPath,
            IEnumerable<TransitionGroup> transitionGroups, int replicateIndex, ChromFileInfoId chromFileInfoId)
        {
            var document = DataSchema.Document;
            var candidatePeakGroups = new List<CandidatePeakGroup>();
            var featureCalculator = OnDemandFeatureCalculator.GetFeatureCalculator(document,
                peptideIdentityPath, replicateIndex, chromFileInfoId);
            if (featureCalculator == null)
            {
                return candidatePeakGroups;
            }

            var precursorIdentityPath = new IdentityPath(peptideIdentityPath, transitionGroups.First());
            var precursor = new Precursor(DataSchema, precursorIdentityPath);
            var precursorResult = new PrecursorResult(precursor,
                new ResultFile(new Replicate(DataSchema, replicateIndex), chromFileInfoId, 0));

            var transitionGroup = (TransitionGroup)precursorIdentityPath.Child;
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

            return candidatePeakGroups.OrderBy(peak => Tuple.Create(peak.PeakGroupStartTime, peak.PeakGroupEndTime)).ToList();
        }

        public IEnumerable<CandidatePeakGroup> GetAllCandidatePeakGroups()
        {
            var document = DataSchema.Document;
            if (!document.Settings.HasResults)
            {
                yield break;
            }

            var chromatograms = document.MeasuredResults.Chromatograms;
            foreach (var peptideGroupDocNode in document.MoleculeGroups)
            {
                CancellationToken.ThrowIfCancellationRequested();
                foreach (var peptideDocNode in peptideGroupDocNode.Molecules)
                {
                    CancellationToken.ThrowIfCancellationRequested();
                    var peptideIdentityPath =
                        new IdentityPath(peptideGroupDocNode.PeptideGroup, peptideDocNode.Peptide);
                    foreach (var comparableGroup in peptideDocNode.GetComparableGroups())
                    {
                        var transitionGroups = comparableGroup.Select(tg => tg.TransitionGroup).ToImmutable();
                        for (int replicateIndex = 0; replicateIndex < chromatograms.Count; replicateIndex++)
                        {
                            var chromatogramSet = chromatograms[replicateIndex];
                            foreach (var msDataFileInfo in chromatogramSet.MSDataFileInfos)
                            {
                                CancellationToken.ThrowIfCancellationRequested();
                                foreach (var candidatePeakGroup in GetCandidatePeakGroups(peptideIdentityPath,
                                             transitionGroups, replicateIndex, msDataFileInfo.FileId))
                                {
                                    yield return candidatePeakGroup;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
