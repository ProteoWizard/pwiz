using pwiz.Common.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public abstract class AlignmentSet<TKey, TAlignment>
    {
        protected abstract TAlignment GetAlignment(TKey to, TKey from);
        protected abstract IEnumerable<KeyValuePair<TKey, TAlignment>> GetAvailableAlignmentsTo(TKey alignTo);

        public IEnumerable<TAlignment> FindAlignmentPath(TKey alignTo, TKey alignFrom, int maxStopovers)
        {
            var queue = new Queue<ImmutableList<KeyValuePair<TKey, TAlignment>>>();
            queue.Enqueue(ImmutableList<KeyValuePair<TKey, TAlignment>>.EMPTY);
            while (queue.Count > 0)
            {
                var list = queue.Dequeue();
                var nextStep = list.LastOrDefault().Key ?? alignTo;
                var endAlignment = GetAlignment(alignFrom, nextStep);
                if (endAlignment != null)
                {
                    return list.Select(tuple => tuple.Value).Prepend(endAlignment);
                }

                if (list.Count < maxStopovers)
                {
                    var excludeNames = list.Select(tuple => tuple.Key).ToHashSet();
                    foreach (var availableAlignment in GetAvailableAlignmentsTo(nextStep))
                    {
                        if (!excludeNames.Contains(availableAlignment.Key))
                        {
                            queue.Enqueue(ImmutableList.ValueOf(list.Prepend(availableAlignment)));
                        }
                    }
                }
            }

            return null;
        }
    }
}
