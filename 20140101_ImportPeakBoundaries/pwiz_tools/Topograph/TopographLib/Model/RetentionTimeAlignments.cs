using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public class RetentionTimeAlignments
    {
        private ImmutableSortedList<AlignmentKey, RetentionTimeAlignment> _alignments;
        public RetentionTimeAlignments(WorkspaceData workspaceData)
        {
            WorkspaceData = workspaceData;
            _alignments = ImmutableSortedList<AlignmentKey, RetentionTimeAlignment>.EMPTY;
        }
        public WorkspaceData WorkspaceData { get; private set; }
        public void MergeFrom(RetentionTimeAlignments retentionTimeAlignments)
        {
            var newAlignments = new List<KeyValuePair<AlignmentKey, RetentionTimeAlignment>>();
            foreach (var pair in retentionTimeAlignments._alignments)
            {
                if (_alignments.ContainsKey(pair.Key))
                {
                    continue;
                }
                if (Equals(GetRetentionTimes(pair.Key.FromId), retentionTimeAlignments.GetRetentionTimes(pair.Key.FromId))
                    && Equals(GetRetentionTimes(pair.Key.ToId), retentionTimeAlignments.GetRetentionTimes(pair.Key.ToId)))
                {
                    newAlignments.Add(pair);
                }
            }
            if (newAlignments.Count == 0)
            {
                return;
            }
            _alignments = ImmutableSortedList.FromValues(_alignments.Concat(newAlignments));
        }
        public void SetData(WorkspaceData workspaceData)
        {
            var dataFileIds =
                _alignments.Keys.Select(key => key.FromId)
                           .Concat(_alignments.Keys.Select(key => key.ToId))
                           .Distinct()
                           .ToArray();
            var changedDataFileIds = new HashSet<long>();
            foreach (var dataFileId in dataFileIds)
            {
                if (!Equals(GetRetentionTimes(dataFileId), GetRetentionTimes(workspaceData, dataFileId)))
                {
                    changedDataFileIds.Add(dataFileId);
                }
            }
            if (changedDataFileIds.Count > 0)
            {
                _alignments = ImmutableSortedList.FromValues(
                        _alignments.Where(pair
                            => !changedDataFileIds.Contains(pair.Key.FromId) 
                            && !changedDataFileIds.Contains(pair.Key.ToId)));
            }
            WorkspaceData = workspaceData;
        }

        public RetentionTimeAlignment GetAlignment(AlignmentKey alignmentKey)
        {
            RetentionTimeAlignment alignmentValue;
            if (!_alignments.TryGetValue(alignmentKey, out alignmentValue))
            {
                return null;
            }
            return alignmentValue;
        }

        public void AddAlignment(AlignmentKey alignmentKey, RetentionTimeAlignment alignmentValue)
        {
            _alignments = ImmutableSortedList.FromValues(
                    _alignments.Concat(new[]
                        {
                            new KeyValuePair<AlignmentKey, RetentionTimeAlignment>(alignmentKey, alignmentValue)
                        }));
        }
        
        ImmutableSortedList<string, double> GetRetentionTimes(long dataFileId)
        {
            return GetRetentionTimes(WorkspaceData, dataFileId);
        }
        static ImmutableSortedList<string, double> GetRetentionTimes(WorkspaceData workspaceData, long dataFileId)
        {
            var msDataFiles = workspaceData.MsDataFiles;
            if (null == msDataFiles)
            {
                return null;
            }
            MsDataFileData msDataFileData;
            if (msDataFiles.TryGetValue(dataFileId, out msDataFileData))
            {
                return msDataFileData.RetentionTimesByModifiedSequence;
            }
            return null;
        }

        public struct AlignmentKey : IComparable
        {
            public AlignmentKey(long fromId, long toId) : this()
            {
                FromId = fromId;
                ToId = toId;
            }
            public long FromId { get; private set; }
            public long ToId { get; private set; }
            public int CompareTo(object o)
            {
                if (null == o)
                {
                    return 1;
                }
                var that = (AlignmentKey) o;
                int result = FromId.CompareTo(that.FromId);
                if (result == 0)
                {
                    result = ToId.CompareTo(that.ToId);
                }
                return result;
            }
        }
    }
}
