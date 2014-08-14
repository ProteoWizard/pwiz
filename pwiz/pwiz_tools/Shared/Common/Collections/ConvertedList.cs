using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Collections
{
    public abstract class ConvertedList<TSource, TTarget> : IList<TTarget>
    {
        protected ConvertedList(IList<TSource> sourceList)
        {
            SourceList = sourceList;
        }

        public IList<TSource> SourceList
        {
            get; set;
        }

        public abstract TTarget Convert(TSource source);
        public abstract TSource Deconvert(TTarget target);
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TTarget> GetEnumerator()
        {
            return SourceList.Select(Convert).GetEnumerator();
        }

        public void Add(TTarget item)
        {
            SourceList.Add(Deconvert(item));
        }

        public void Clear()
        {
            SourceList.Clear();
        }

        public bool Contains(TTarget item)
        {
            return SourceList.Contains(Deconvert(item));
        }

        public void CopyTo(TTarget[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array.SetValue(item, arrayIndex++);
            }
        }

        public bool Remove(TTarget item)
        {
            return SourceList.Remove(Deconvert(item));
        }

        public int Count
        {
            get { return SourceList.Count; }
        }

        public bool IsReadOnly
        {
            get { return SourceList.IsReadOnly; }
        }

        public int IndexOf(TTarget item)
        {
            return SourceList.IndexOf(Deconvert(item));
        }

        public void Insert(int index, TTarget item)
        {
            SourceList.Insert(index, Deconvert(item));
        }

        public void RemoveAt(int index)
        {
            SourceList.RemoveAt(index);
        }

        public TTarget this[int index]
        {
            get { return Convert(SourceList[index]); }
            set { SourceList[index] = Deconvert(value); }
        }

        public static ConvertedList<TSource, TTarget> Create(IList<TSource> sourceList, Converter<TSource, TTarget> converter, Converter<TTarget,TSource> deconverter)
        {
            return new Impl(sourceList, converter, deconverter);
        }

        class Impl : ConvertedList<TSource, TTarget>
        {
            private readonly Converter<TSource, TTarget> _converter;
            private readonly Converter<TTarget, TSource> _deconverter;
            public Impl(IList<TSource> sourceList, Converter<TSource,TTarget> converter, Converter<TTarget,TSource> deconverter) : base(sourceList)
            {
                _converter = converter;
                _deconverter = deconverter;
                                        
            }

            public override TTarget Convert(TSource source)
            {
                return _converter(source);
            }

            public override TSource Deconvert(TTarget target)
            {
                return _deconverter(target);
            }
        }
    }
}
