using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace pwiz.Common.Collections
{
    public class ImmutableDictionary<K,V> : ImmutableCollection<KeyValuePair<K,V>>, IDictionary<K,V>
    {
        public ImmutableDictionary(IDictionary<K,V> dict) : base(dict)
        {
        }
        protected ImmutableDictionary()
        {
        }

        protected IDictionary<K, V> Dictionary { get { return (IDictionary<K, V>) Collection;} set { Collection = value;} }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool ContainsKey(K key)
        {
            return Dictionary.ContainsKey(key);
        }

        public void Add(K key, V value)
        {
            throw new InvalidOperationException();
        }

        public bool Remove(K key)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetValue(K key, out V value)
        {
            return Dictionary.TryGetValue(key, out value);
        }

        public V this[K key]
        {
            get { return Dictionary[key]; }
            set { throw new InvalidOperationException(); }
        }

        public ICollection<K> Keys
        {
            get { return new ImmutableCollection<K>(Dictionary.Keys); }
        }

        public ICollection<V> Values
        {
            get { return new ImmutableCollection<V>(Dictionary.Values); }
        }
    }
}
