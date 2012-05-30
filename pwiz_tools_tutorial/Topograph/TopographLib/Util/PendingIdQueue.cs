using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Util
{
    public class PendingIdQueue
    {
        private IList<long> _queriedIds = new long[0];
        private int _queriedIdIndex;
        private bool _requeryPending = true;
        private HashSet<long> _additionalIds = new HashSet<long>();

        public void SetQueriedIds(ICollection<long> ids)
        {
            lock(this)
            {
                _queriedIds = new List<long>(ids);
                _queriedIdIndex = 0;
                _requeryPending = false;
                _additionalIds = new HashSet<long>();
            }
        }

        public long? GetNextId()
        {
            lock (this)
            {
                if (_queriedIdIndex < _queriedIds.Count)
                {
                    long id = _queriedIds[_queriedIdIndex];
                    _queriedIdIndex++;
                    return id;
                }
                if (_additionalIds.Count > 0)
                {
                    long id = _additionalIds.First();
                    _additionalIds.Remove(id);
                    return id;
                }
                return null;
            }
            
        }

        public IEnumerable<long> EnumerateIds()
        {
            var returnedSet = new HashSet<long>();
            while (true)
            {
                long? id = GetNextId();
                if (id == null)
                {
                    yield break;
                }
                if (returnedSet.Add(id.Value))
                {
                    yield return id.Value;
                }
            }
        }

        public void SetRequeryPending()
        {
            lock(this)
            {
                _requeryPending = true;
            }
        }

        public bool IsRequeryPending()
        {
            lock(this)
            {
                return _requeryPending;
            }
        }

        public void AddId(long id)
        {
            lock(this)
            {
                _additionalIds.Add(id);
            }
        }
        public void AddIds(IEnumerable<long> ids)
        {
            lock(this)
            {
                _additionalIds.UnionWith(ids);
            }
        }

        public int PendingIdCount()
        {
            lock(this)
            {
                return _queriedIds.Count - _queriedIdIndex + _additionalIds.Count();
            }
        }
    }
}
