using System;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Databinding.Entities;

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
            return key.PrecursorResult.CandidatePeakGroups[key.PeakIndex];
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
