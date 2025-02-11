using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public class FileTargetMatrix<T>
    {
        public static readonly FileTargetMatrix<T> EMPTY = new FileTargetMatrix<T>(ImmutableList.Empty<RetentionTimeSource>(),
            ImmutableList.Empty<Target>(), ImmutableList<ImmutableList<T>>.EMPTY);
        public bool IsEmpty
        {
            get
            {
                return FileNames.Count == 0 || Targets.Count == 0;
            }
        }
        public FileTargetMatrix(IEnumerable<RetentionTimeSource> fileNames, IEnumerable<Target> targets,
            IEnumerable<ImmutableList<T>> entries)
        {
            FileNames = ImmutableList.ValueOf(fileNames);
            Targets = ImmutableList.ValueOf(targets);
            Entries = ImmutableList.ValueOf(entries);
            if (Targets.Count != Entries.Count)
            {
                throw new ArgumentException();
            }
        }

        public LibKeyIndex LibKeyIndex { get; private set; }
        public ImmutableList<RetentionTimeSource> FileNames { get; private set; }
        public ImmutableList<Target> Targets { get; private set; }
        public ImmutableList<ImmutableList<T>> Entries { get; private set; }

        public FileTargetMatrix<T> Merge(params FileTargetMatrix<T>[] rightList)
        {
            var targetDictionary = new Dictionary<Target, int>();
            foreach (var target in Targets)
            {
                targetDictionary.Add(target, targetDictionary.Count);
            }

            var newEntries = Entries.Select(list => list.ToList()).ToList();
            var newFiles = FileNames.ToList();
            foreach (var right in rightList)
            {
                for (int iRight = 0; iRight < right.Targets.Count; iRight++)
                {
                    var target = right.Targets[iRight];
                    if (!targetDictionary.TryGetValue(target, out int index))
                    {
                        index = targetDictionary.Count;
                        targetDictionary.Add(target, targetDictionary.Count);
                        newEntries.Add(Enumerable.Repeat(default(T), newFiles.Count).ToList());
                    }
                    newEntries[index].AddRange(right.Entries[iRight]);
                }
                newFiles.AddRange(right.FileNames);
                foreach (var list in newEntries)
                {
                    list.AddRange(Enumerable.Repeat(default(T), newFiles.Count - list.Count));
                }
            }

            if (targetDictionary.Count == Targets.Count)
            {
                return new FileTargetMatrix<T>(newFiles, Targets, newEntries.Select(ImmutableList.ValueOf));
            }

            var targets = ImmutableList.ValueOf(targetDictionary.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key));
            return new FileTargetMatrix<T>(newFiles, targets, newEntries.Select(ImmutableList.ValueOf));
        }

        public static FileTargetMatrix<T> MergeAll(IEnumerable<FileTargetMatrix<T>> lists)
        {
            var nonEmpty = lists.Where(list => !list.IsEmpty).ToList();
            if (nonEmpty.Count == 0)
            {
                return EMPTY;
            }

            if (nonEmpty.Count == 1)
            {
                return nonEmpty[0];
            }

            return nonEmpty[0].Merge(nonEmpty.Skip(1).ToArray());
        }
    }
}
