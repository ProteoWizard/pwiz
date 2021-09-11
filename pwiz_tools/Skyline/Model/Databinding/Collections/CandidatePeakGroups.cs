using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results.Scoring;


namespace pwiz.Skyline.Model.Databinding.Collections
{
    public class CandidatePeakGroups : SkylineObjectList<CandidatePeakGroups.PeakKey, CandidatePeakGroup>
    {
        private ImmutableList<Precursor> _precursors;
        private Replicate _replicate;


        public CandidatePeakGroups(SkylineDataSchema dataSchema) : base(dataSchema)
        {
        }

        public IList<Precursor> Precursors
        {
            get
            {
                return _precursors;
            }
            set
            {
                var newValue = ImmutableList.ValueOf(value);
                if (Equals(Precursors, newValue))
                {
                    return;
                }

                _precursors = newValue;
                FireListChanged();
            }
        }

        public void SetSelectedIdentityPaths(IEnumerable<IdentityPath> identityPaths)
        {
            var precursorPaths = PathsToMaxLevel(SrmDocument.Level.TransitionGroups, identityPaths);
            var peptidePaths = PathsToMaxLevel(SrmDocument.Level.Molecules, precursorPaths);
            var peptides = new Peptides(DataSchema, peptidePaths.ToList());
            var precursors = new List<Precursor>();
            foreach (Entities.Peptide peptide in peptides.GetItems())
            {
                var comparableGroups = PeakFeatureEnumerator.ComparableGroups(peptide.DocNode)
                    .Select(group=>group.ToList()).ToList();
                bool containsAllGroups = comparableGroups.Count == 1
                                         || peptidePaths.Contains(peptide.IdentityPath)
                                         || peptidePaths.Contains(peptide.IdentityPath.Parent);
                foreach (var comparableGroup in comparableGroups)
                {
                    if (containsAllGroups || comparableGroup.Any(nodeGroup =>
                        precursorPaths.Contains(new IdentityPath(peptide.IdentityPath, nodeGroup.Id))))
                    {
                        precursors.Add(new Precursor(DataSchema, new IdentityPath(peptide.IdentityPath, comparableGroup[0].Id)));
                    }
                }
            }

            Precursors = precursors;
        }

        private HashSet<IdentityPath> PathsToMaxLevel(SrmDocument.Level level, IEnumerable<IdentityPath> identityPaths)
        {
            return identityPaths.Select(path => path.Length > (int) level + 1 ? path.GetPathTo((int) level) : path)
                .ToHashSet();
        }


        public Replicate Replicate
        {
            get
            {
                return _replicate;
            }
            set
            {
                if (Equals(Replicate, value))
                {
                    return;
                }

                _replicate = value;
                FireListChanged();
            }
        }

        protected override IEnumerable<PeakKey> ListKeys()
        {
            if (_precursors == null)
            {
                yield break;
            }
            foreach (var precursor in _precursors)
            {
                foreach (var entry in precursor.Results)
                {
                    if (Replicate != null && entry.Key.ReplicateIndex != Replicate.ReplicateIndex)
                    {
                        continue;
                    }

                    var precursorResult = entry.Value;
                    int peakCount = precursorResult.GetCandidatePeakGroupCount();
                    for (int peakIndex = 0; peakIndex < peakCount; peakIndex++)
                    {
                        yield return new PeakKey(precursorResult, peakIndex);
                    }
                }
            }
        }

        protected override CandidatePeakGroup ConstructItem(PeakKey key)
        {
            var candidatePeakGroups = key.PrecursorResult.CandidatePeakGroups;
            if (key.PeakIndex < 0 || key.PeakIndex >= candidatePeakGroups.Count)
            {
                return null;
            }
            return candidatePeakGroups[key.PeakIndex];
        }

        public class PeakKey
        {
            private Tuple<IdentityPath, ResultFileKey, int> _key;
            public PeakKey(PrecursorResult precursorResult, int peakIndex)
            {
                PrecursorResult = precursorResult;
                PeakIndex = peakIndex;
                _key = Tuple.Create(precursorResult.Precursor.IdentityPath, precursorResult.GetResultFile().ToFileKey(),
                    PeakIndex);
            }
            public PrecursorResult PrecursorResult { get; private set; }
            public int PeakIndex { get; private set; }

            protected bool Equals(PeakKey other)
            {
                return Equals(_key, other._key);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((PeakKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return _key.GetHashCode();
                }
            }
        }
    }
}
