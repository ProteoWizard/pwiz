//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _VIRTUAL_MAP_HPP_
#define _VIRTUAL_MAP_HPP_

#include <map>

namespace pwiz {
namespace util {

/// a wrapper for std::map that will behave properly with polymorphism
template<class keyT,
         class valueT,
         class _Pr = std::less<keyT>,
         class _Alloc = std::allocator<std::pair<const keyT, valueT> > >
class virtual_map
{
    public:

    typedef std::map<keyT, valueT, _Pr, _Alloc> BaseType;
    typedef typename BaseType::allocator_type allocator_type;
    typedef typename BaseType::key_type key_type;
    typedef typename BaseType::value_type value_type;
    typedef typename BaseType::key_compare key_compare;
    typedef typename BaseType::value_compare value_compare;
    typedef typename BaseType::size_type size_type;
    typedef typename BaseType::mapped_type mapped_type;
    typedef typename BaseType::difference_type difference_type;
    typedef typename BaseType::pointer pointer;
    typedef typename BaseType::const_pointer const_pointer;
    typedef typename BaseType::reference reference;
    typedef typename BaseType::const_reference const_reference;
    typedef typename BaseType::iterator iterator;
    typedef typename BaseType::const_iterator const_iterator;
    typedef typename BaseType::reverse_iterator reverse_iterator;
    typedef typename BaseType::const_reverse_iterator const_reverse_iterator;

    private:
        BaseType _base;

    public:

    /// Constructs an empty map that uses the predicate _Pred to order keys, if it is supplied. The map uses the allocator _Alloc for all storage management, if it is supplied.
    explicit virtual_map(const key_compare& predicate = key_compare(), const allocator_type& allocator = allocator_type())
        : _base(predicate, allocator)
    {
    }

    /// Constructs a map containing values in the range [_First, _Last). Creation of the new map is only guaranteed to succeed if the iterators start and finish return values of type pair<class Key, class Value> and all values of Key in the range [_First, _Last) are unique. The map uses the predicate _Pred to order keys, and the allocator _Alloc for all storage management.
    template<class _Iter>
    virtual_map(_Iter _First, _Iter _Last)
        : _base(key_compare(), allocator_type())
    {
        for (; _First != _Last; ++_First)
            this->insert(*_First);
    }

    /// Constructs a map containing values in the range [_First, _Last). Creation of the new map is only guaranteed to succeed if the iterators start and finish return values of type pair<class Key, class Value> and all values of Key in the range [_First, _Last) are unique. The map uses the predicate _Pred to order keys, and the allocator _Alloc for all storage management.
    template<class _Iter>
    virtual_map(_Iter _First, _Iter _Last, const key_compare& _Pred)
        : _base(_Pred, allocator_type())
    {
        for (; _First != _Last; ++_First)
            this->insert(*_First);
    }

    /// Constructs a map containing values in the range [_First, _Last). Creation of the new map is only guaranteed to succeed if the iterators start and finish return values of type pair<class Key, class Value> and all values of Key in the range [_First, _Last) are unique. The map uses the predicate _Pred to order keys, and the allocator _Alloc for all storage management.
    template<class _Iter>
    virtual_map(_Iter _First, _Iter _Last, const key_compare& _Pred, const allocator_type& _Al)
        : _base(_Pred, _Al)
    {
        for (; _First != _Last; ++_First)
            this->insert(*_First);
    }

    virtual ~virtual_map() {}

    /// Returns a copy of the allocator used by self for storage management.
    virtual allocator_type get_allocator() const
	{return _base.get_allocator();}

    /// Returns an iterator pointing to the first element stored in the map. First is defined by the map's comparison operator, Compare.
    virtual iterator begin()
	{return _base.begin();}

    /// Returns a const_iterator pointing to the first element stored in the map.
    virtual const_iterator begin() const
	{return _base.begin();}

    /// Returns an iterator pointing to the last element stored in the map; in other words, to the off-the-end value.
    virtual iterator end()
	{return _base.end();}

    /// Returns a const_iterator pointing to the last element stored in the map.
    virtual const_iterator end() const
	{return _base.end();}

    /// Returns a reverse_iterator pointing to the first element stored in the map. First is defined by the map's comparison operator, Compare.
    virtual reverse_iterator rbegin()
	{return _base.rbegin();}

    /// Returns a const_reverse_iterator pointing to the first element stored in the map.
    virtual const_reverse_iterator rbegin() const
	{return _base.rbegin();}

    /// Returns a reverse_iterator pointing to the last element stored in the map; in other words, to the off-the-end value).
    virtual reverse_iterator rend()
	{return _base.rend();}

    /// Returns a const_reverse_iterator pointing to the last element stored in the map.
    virtual const_reverse_iterator rend() const
	{return _base.rend();}

    /// Replaces the contents of *this with a copy of the map x.
    virtual virtual_map<keyT, valueT, key_compare, allocator_type>& operator=(const virtual_map<keyT, valueT, key_compare, allocator_type>& x)
    {_base = x._base; return *this;}

    /// If an element with the key x exists in the map, then a reference to its associated value is returned. Otherwise the pair x,T() is inserted into the map and a reference to the default object T() is returned.
    virtual mapped_type& operator[](const key_type& x)
    {return _base[x];}

    /// Erases all elements from the self.
    virtual void clear()
	{_base.clear();}

    /// Returns a 1 if a value with the key x exists in the map. Otherwise returns a 0.
    virtual size_type count(const key_type& x) const
	{return _base.count(x);}

    /// Returns true if the map is empty, false otherwise.
    virtual bool empty() const
	{return _base.empty();}

    /// Returns the pair (lower_bound(x), upper_bound(x)).
    virtual std::pair<iterator, iterator> equal_range(const key_type& x)
    {return _base.equal_range(x);}

    /// Returns the pair (lower_bound(x), upper_bound(x)).
    virtual std::pair<const_iterator,const_iterator> equal_range(const key_type& x) const
    {return _base.equal_range(x);}

    /// Deletes the map element pointed to by the iterator position.
    virtual void erase(iterator position)
	{_base.erase(position);}

    /// If the iterators start and finish point to the same map and last is reachable from first, all elements in the range [start, finish) are deleted from the map.
    virtual void erase(iterator start, iterator finish)
	{_base.erase(start, finish);}

    /// Deletes the element with the key value x from the map, if one exists. Returns 1 if x existed in the map, 0 otherwise.
    virtual size_type erase(const key_type& x)
	{return _base.erase(x);}

    /// Searches the map for a pair with the key value x and returns an iterator to that pair if it is found. If such a pair is not found the value end() is returned.
    virtual iterator find(const key_type& x)
	{return _base.find(x);}

    /// Same as find above but returns a const_iterator.
    virtual const_iterator find(const key_type& x) const
	{return _base.find(x);} 

    /// If a value_type with the same key as x is not present in the map, then x is inserted into the map. Otherwise, the pair is not inserted. A position may be supplied as a hint regarding where to do the insertion. If the insertion is done right after position, then it takes amortized constant time. Otherwise it takes O(log N) time.
    virtual std::pair<iterator, bool> insert(const value_type& x)
    {return _base.insert(x);}

    /// If a value_type with the same key as x is not present in the map, then x is inserted into the map. Otherwise, the pair is not inserted. A position may be supplied as a hint regarding where to do the insertion. If the insertion is done right after position, then it takes amortized constant time. Otherwise it takes O(log N) time.
    virtual iterator insert(iterator position, const value_type& x)
	{return _base.insert(position, x);}

    /// Copies of each element in the range [start, finish) that possess a unique key (one not already in the map) are inserted into the map. The iterators start and finish must return values of type pair<T1,T2>. This operation takes approximately O(N*log(size()+N)) time.
    template <class InputIterator>
    void insert(InputIterator start, InputIterator finish)
	{_base.insert(start, finish);}

    /// Returns a function object capable of comparing key values using the comparison operation, Compare, of the current map.
    virtual key_compare key_comp() const
	{return _base.key_comp();}

    /// Returns a reference to the first entry with a key greater than or equal to x.
    virtual iterator lower_bound(const key_type& x)
	{return _base.lower_bound(x);}

    /// Same as lower_bound above but returns a const_iterator.
    virtual const_iterator lower_bound(const key_type& x) const
	{return _base.lower_bound(x);}

    /// Returns the maximum possible size of the map. This size is only constrained by the number of unique keys that can be represented by the type Key.
    virtual size_type max_size() const
	{return _base.max_size();}

    /// Returns the number of elements in the map.
    virtual size_type size() const
	{return _base.size();}

    /// Swaps the contents of the map x with the current map, *this.
    virtual void swap(virtual_map<keyT, valueT, key_compare, allocator_type>& x)
	{_base.swap(x._base);}

    /// Returns an iterator for the first entry with a key greater than x.
    virtual iterator upper_bound(const key_type& x)
	{return _base.upper_bound(x);}

    /// Same as upper_bound above, but returns a const_iterator.
    virtual const_iterator upper_bound(const key_type& x) const
	{return _base.upper_bound(x);}

    /// Returns a function object capable of comparing pair<const Key, T> values using the comparison operation, Compare, of the current map. This function is identical to key_comp for sets.
    virtual value_compare value_comp() const
	{return _base.value_comp();}
};


} // namespace util
} // namespace pwiz

#endif // _VIRTUAL_MAP_HPP_
