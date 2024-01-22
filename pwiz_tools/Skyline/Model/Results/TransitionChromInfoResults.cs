using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ReplicatePositions : IReadOnlyList<IEnumerable<int>>
    {
        private ImmutableList<int> _replicateEndPositions;

        public static ReplicatePositions FromResults<T>(IResults<T> results) where T : ChromInfo
        {
            return FromCounts(results.Select(chromInfoList => chromInfoList.Count));
        }

        public static ReplicatePositions FromCounts(IEnumerable<int> counts)
        {
            int total = 0;
            var endPositions = ImmutableList.ValueOf(counts.Select(count => total += count));
            return new ReplicatePositions(endPositions);
        }

        private ReplicatePositions(ImmutableList<int> endPositions)
        {
            _replicateEndPositions = endPositions;
        }

        public int Count
        {
            get { return _replicateEndPositions.Count; }
        }
        public int TotalCount
        {
            get
            {
                if (_replicateEndPositions.Count == 0)
                {
                    return 0;
                }
                return _replicateEndPositions[_replicateEndPositions.Count - 1];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<IEnumerable<int>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        }

        public IEnumerable<int> this[int index] 
        {
            get
            {
                int start = index == 0 ? 0 : _replicateEndPositions[index - 1];
                int count = _replicateEndPositions[index] - start;
                return Enumerable.Range(start, count);
            }
        }
    }

    public abstract class ListFieldDef
    {
        public abstract Type Type { get; }
        public abstract int GetCount(IEnumerable list);

        public static ListFieldDef MakeFieldDef(Type type)
        {
            var fieldDefType = typeof(ListFieldDef<>).MakeGenericType(type);
            var constructor = fieldDefType.GetConstructor(null);
            return (ListFieldDef) constructor.Invoke(Array.Empty<object>());
        }

        public static IEnumerable<ListFieldDef> FromClass(Type type)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.GetProperty |
                                                        BindingFlags.SetProperty | BindingFlags.Public))
            {
                yield return MakeFieldDef(property.PropertyType);
            }
        }
    }

    public class ListFieldDef<T> : ListFieldDef
    {
        public override Type Type => typeof(T);
        public override int GetCount(IEnumerable list)
        {
            return ((IReadOnlyList<T>)list).Count;
        }
    }

    public abstract class ListFields : Immutable
    {
        private ImmutableList<IEnumerable> _lists;
        protected abstract ImmutableList<ListFieldDef> ListFieldDefs { get; }

        public int Count
        {
            get { return ListFieldDefs[0].GetCount(_lists[0]); }
        }
    }

    public abstract class ListFields<T> : ListFields
    {
        private static ImmutableList<ListFieldDef> _fieldDefs;

        static ListFields()
        {
            _fieldDefs = ImmutableList.ValueOf(ListFieldDef.FromClass(typeof(T)));
        }
    }

    public abstract class AbstractResults : Immutable
    {
        public ImmutableList<ReferenceValue<ChromFileInfoId>> ChromFileInfoIds { get; protected set; }

        public int Count
        {
            get
            {
                return ChromFileInfoIds.Count;
            }
        }

        protected T GetListValue<T>(IReadOnlyList<T> list, int index)
        {
            return list == null ? default : list[index];
        }
    }

    public class TransitionChromInfoResults : ListFields<TransitionChromInfoResults.Fields>, IReadOnlyList<TransitionChromInfo>
    {
        public class Fields
        {
            public ReferenceValue<ChromFileInfoId> ChromFileInfoId { get; set; }
            public int OptimizationStep { get; set; }
            public float? MassError { get; set; }
            public float RetentionTime { get; set; }
            public float StartRetentionTime { get; set; }
            public float EndRetentionTime { get; set; }
            public float IonMobilityFilter { get; set; }
            public float Area { get; set; }
            public float BackgroundArea { get; set; }
            public float Height { get; set; }
            public float Fwhm { get; set; }
            public bool FwhmDegenerate { get; set; }
            public bool? Truncated { get; set; }
            public short? PointsAcrossPeak { get; set; }
            public PeakIdentification Identifier { get; set; }
            public short Rank { get; set; }
            public short RankByLevel { get; set; }
            public Annotations Annotations { get; set; }
            public UserSet UserSet { get; set; }
            public bool IsForcedIntegration { get; set; }
            public PeakShapeValues? PeakShapeValues { get; set; }
        }
    }


}
