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


#ifndef _CHARINDEXEDVECTOR_HPP_
#define _CHARINDEXEDVECTOR_HPP_

#include "boost/array.hpp"

namespace pwiz {
namespace util {

/// an iterator for CharIndexedVector
template<class T>
class CharIndexedVectorIterator
{
    typedef boost::array<T, 129> type;
    typename type::iterator m_itr;

public:
    typedef typename type::value_type value_type;
    typedef typename type::iterator iterator;
    typedef typename type::iterator pointer;
    typedef typename type::const_iterator const_iterator;
    typedef typename type::difference_type difference_type;
    typedef typename type::reference reference;
    typedef typename type::const_reference const_reference;
    typedef typename type::size_type size_type;
    typedef std::random_access_iterator_tag iterator_category;

    CharIndexedVectorIterator(const iterator& itr) : m_itr(itr) {}

    reference operator*() const
    {
        return *m_itr;
    }

    bool operator!=(const CharIndexedVectorIterator& rhs) const
    {
        return m_itr != *(iterator*)&rhs;
    }

    difference_type operator-(const CharIndexedVectorIterator& rhs) const
    {
        return m_itr - rhs.m_itr;
    }

    CharIndexedVectorIterator& operator++()
    {	// preincrement
        ++m_itr;
        return (*this);
    }

    CharIndexedVectorIterator operator++(int)
    {	// postincrement
        CharIndexedVectorIterator _Tmp = *this;
        ++m_itr;
        return (_Tmp);
    }

    CharIndexedVectorIterator& operator--()
    {	// predecrement
        ++m_itr;
        return (*this);
    }

    CharIndexedVectorIterator operator--(int)
    {	// postdecrement
        CharIndexedVectorIterator _Tmp = *this;
        ++m_itr;
        return (_Tmp);
    }

    CharIndexedVectorIterator& operator+=(difference_type _Off)
    {	// increment by integer
        m_itr += _Off;
        return (*this);
    }

    CharIndexedVectorIterator& operator-=(difference_type _Off)
    {	// decrement by integer
        return (*this += -_Off);
    }

    bool operator<(const CharIndexedVectorIterator& rhs)
    {
        return m_itr < rhs.m_itr;
    }
};

/// a const_iterator for CharIndexedVector
template<class T>
class CharIndexedVectorConstIterator
{
    typedef boost::array<T, 129> type;
    typename type::const_iterator m_itr;

public:
    typedef typename type::value_type value_type;
    typedef typename type::iterator iterator;
    typedef typename type::iterator pointer;
    typedef typename type::const_iterator const_iterator;
    typedef typename type::difference_type difference_type;
    typedef typename type::reference reference;
    typedef typename type::const_reference const_reference;
    typedef typename type::size_type size_type;
    typedef std::random_access_iterator_tag iterator_category;

    CharIndexedVectorConstIterator(const const_iterator& itr) : m_itr(itr) {}

    const_reference operator*() const
    {
        return *m_itr;
    }

    bool operator!=(const CharIndexedVectorConstIterator& rhs) const
    {
        return m_itr != *(const_iterator*)&rhs;
    }

    difference_type operator-(const CharIndexedVectorConstIterator& rhs) const
    {
        return m_itr - rhs.m_itr;
    }

    CharIndexedVectorConstIterator& operator++()
    {	// preincrement
        ++m_itr;
        return (*this);
    }

    CharIndexedVectorConstIterator operator++(int)
    {	// postincrement
        CharIndexedVectorConstIterator _Tmp = *this;
        ++m_itr;
        return (_Tmp);
    }

    CharIndexedVectorConstIterator& operator--()
    {	// predecrement
        ++m_itr;
        return (*this);
    }

    CharIndexedVectorConstIterator operator--(int)
    {	// postdecrement
        CharIndexedVectorConstIterator _Tmp = *this;
        ++m_itr;
        return (_Tmp);
    }

    CharIndexedVectorConstIterator& operator+=(difference_type _Off)
    {	// increment by integer
        m_itr += _Off;
        return (*this);
    }

    CharIndexedVectorConstIterator& operator-=(difference_type _Off)
    {	// decrement by integer
        return (*this += -_Off);
    }

    bool operator<(const CharIndexedVectorConstIterator& rhs)
    {
        return m_itr < rhs.m_itr;
    }
};

/// a wrapper for boost::array that is indexable by character; supports indexes 0-127
template<class T>
struct CharIndexedVector : public boost::array<T, 129>
{
    typedef boost::array<T, 129> type;
    typedef CharIndexedVectorIterator<T> iterator;
    typedef CharIndexedVectorConstIterator<T> const_iterator;
    typedef std::reverse_iterator<iterator> reverse_iterator;
    typedef std::reverse_iterator<const_iterator> const_reverse_iterator;

    CharIndexedVector()
    {
        clear();
    }

    size_t size() const
    {
        return 128;
    }

    void erase(const char c)
    {
        this->operator[](c) = T();
    }

    void clear()
    {
        std::fill(type::begin(), type::end(), T());
    }

    char getIndexAsChar(iterator itr) const
    {
        return 'A' + (itr - type::begin());
    }

    char getIndexAsChar(size_t i) const
    {
        return 'A' + (&this->operator[](i) - type::begin());
    }

    const T& operator[](const char c) const
    {
        return type::operator[]((size_t) c);
    }

    T& operator[](const char c)
    {
        return type::operator[]((size_t) c);
    }

    const_iterator begin() const    {return type::begin();}
    const_iterator end() const      {return type::end();}
    iterator begin()                {return type::begin();}
    iterator end()                  {return type::end();}
};

} // namespace util
} // namespace pwiz

#endif // _CHARINDEXEDVECTOR_HPP_
