/*
 * Author: Jarrett Egertson <jegertso .at. u.washington.edu>,
 *             MacCoss Lab, Department of Genome Sciences, UW
 *
 * Modified version of the code posted at :
 * http://geekswithblogs.net/robp/archive/2008/08/07/speedy-c-part-2-optimizing-memory-allocations---pooling-and.aspx
 */

using System.Collections.Generic;

namespace pwiz.Skyline.Model.Results
{
    public class ArrayPool<T> where T : new()
    {
        private readonly Stack<T[]> _items;
        private readonly int _arraySize;

        public ArrayPool(int arraySize, int? preAlloc)
        {
            _items = new Stack<T[]>();
            _arraySize = arraySize;
            if (preAlloc.HasValue)
            {
                for (int i = 0; i < preAlloc.Value; ++i )
                    _items.Push(new T[_arraySize]);
            }
        }
        
        public T[] Get()
        {
            return _items.Count == 0 ? new T[_arraySize] : _items.Pop();
        }

        public void Free(T[] item)
        {
            _items.Push(item);
        }
    }
}
