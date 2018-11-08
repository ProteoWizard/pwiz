//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2017 Matt Chambers
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

#ifndef __BINARYDATA_HPP__
#define __BINARYDATA_HPP__

#ifdef __cplusplus_cli
#pragma managed(push, off)
#endif

#include <algorithm>
#include <vector>
#include <stdexcept>
#include <memory>
#include <iterator>
#include <limits>
#include <boost/assert.hpp>
#include "pwiz/utility/misc/Export.hpp"


namespace pwiz {
namespace util {

/// A custom vector class that can store its contents in either a std::vector or a cli::array (when compiled with .NET).
template <typename T>
class PWIZ_API_DECL BinaryData
{
    public:

    typedef std::size_t size_type;
    typedef std::ptrdiff_t difference_type;
    typedef T value_type;
    typedef T &reference;
    typedef const T &const_reference;

    class PWIZ_API_DECL const_iterator : public std::iterator<std::random_access_iterator_tag, const T>
    {
        public:

        const T& operator*() const { return *current_; }
        const T* operator->() const { return current_; }
        const_iterator& operator++() { ++current_; return *this; }
        const_iterator operator++(int) { const_iterator copy = *this; ++(*this); return copy; }
        const_iterator& operator--() { --current_; return *this; }
        const_iterator operator--(int) { const_iterator copy = *this; --(*this); return copy; }
        const_iterator& operator+=(difference_type n) { current_ += n; return *this; }
        const_iterator& operator-=(difference_type n) { current_ -= n; return *this; }
        const_iterator operator+(difference_type n) const { const_iterator copy = *this; copy += n; return copy; }
        const_iterator operator-(difference_type n) const { const_iterator copy = *this; copy -= n; return copy; }
        difference_type operator-(const const_iterator& rhs) const { return current_ - rhs.current_; }
        const T& operator[](difference_type n) const { return *(current_ + n); }

        bool operator!=(const const_iterator& that) const { return current_ != that.current_; }
        bool operator==(const const_iterator& that) const { return current_ == that.current_; }
        bool operator<(const const_iterator& that) const { return current_ < that.current_; }
        bool operator<=(const const_iterator& that) const { return current_ <= that.current_; }
        bool operator>(const const_iterator& that) const { return current_ > that.current_; }
        bool operator>=(const const_iterator& that) const { return current_ >= that.current_; }

        const_iterator() : current_(NULL) {}
        const_iterator(const const_iterator& rhs) : current_(rhs.current_) {}

        protected:
        const_iterator(const BinaryData& binaryData, bool begin = true);

        friend class BinaryData;
        const T* current_;
    };

    class PWIZ_API_DECL iterator : public std::iterator<std::random_access_iterator_tag, T>
    {
        public:

        T& operator*() const { return *current_; }
        T* operator->() const { return current_; }
        iterator& operator++() { ++current_; return *this; }
        iterator operator++(int) { iterator copy = *this; ++(*this); return copy; }
        iterator& operator--() { --current_; return *this; }
        iterator operator--(int) { iterator copy = *this; --(*this); return copy; }
        iterator& operator+=(difference_type n) { current_ += n; return *this; }
        iterator& operator-=(difference_type n) { current_ -= n; return *this; }
        iterator operator+(difference_type n) const { iterator copy = *this; copy += n; return copy; }
        iterator operator-(difference_type n) const { iterator copy = *this; copy -= n; return copy; }
        difference_type operator-(const iterator& rhs) const { return current_ - rhs.current_; }
        T& operator[](difference_type n) const { return *(current_ + n); }

        bool operator!=(const iterator& that) const { return current_ != that.current_; }
        bool operator==(const iterator& that) const { return current_ == that.current_; }
        bool operator<(const iterator& that) const { return current_ < that.current_; }
        bool operator<=(const iterator& that) const { return current_ <= that.current_; }
        bool operator>(const iterator& that) const { return current_ > that.current_; }
        bool operator>=(const iterator& that) const { return current_ >= that.current_; }

        iterator() : current_(NULL) {}
        iterator(const iterator& rhs) : current_(rhs.current_) {}

        protected:
        iterator(BinaryData& binaryData, bool begin = true);

        friend class BinaryData;
        T* current_;
    };
    typedef std::reverse_iterator<iterator> reverse_iterator;
    typedef std::reverse_iterator<const_iterator> const_reverse_iterator;

#pragma region Ctors/Dtor
    BinaryData(size_type elements = 0, T t = T());

    BinaryData(const BinaryData &source);

    BinaryData(const_iterator first, const_iterator last);

    BinaryData(void* cliNumericArray);

    BinaryData &operator=(void* cliNumericArray);

    ~BinaryData();
#pragma endregion

#pragma region Iterators/accessors

    bool empty() const /*throw()*/
    {
        return size() == 0;
    }

    size_t size() const /*throw()*/
    {
        return _size();
    }

    size_t capacity() const /*throw()*/
    {
        return _capacity();
    }

    void reserve(size_type n)
    {
        if (n > _capacity())
            _reserve(n);
    }

    size_type max_size() const /*throw()*/
    {
        return std::numeric_limits<int>().max() / sizeof(T);
    }

    const_iterator cbegin() const /*throw(std::runtime_error)*/
    {
        return const_iterator(*this);
    }

    iterator begin() /*throw(std::runtime_error)*/
    {
        return iterator(*this);
    }

    const_iterator begin() const /*throw(std::runtime_error)*/
    {
        return cbegin();
    }

    const_iterator cend() const /*throw(std::runtime_error)*/
    {
        return const_iterator(*this, false);
    }

    const_iterator end() const /*throw(std::runtime_error)*/
    {
        return cend();
    }

    iterator end() /*throw(std::runtime_error)*/
    {
        return iterator(*this, false);
    }

    reverse_iterator rbegin() /*throw(std::runtime_error)*/
    {
        return reverse_iterator(end());
    }

    const_reverse_iterator crbegin() const /*throw(std::runtime_error)*/
    {
        return const_reverse_iterator(cend());
    }

    const_reverse_iterator rbegin() const /*throw(std::runtime_error)*/
    {
        return crbegin();
    }

    reverse_iterator rend() /*throw(std::runtime_error)*/
    {
        return reverse_iterator(begin());
    }

    const_reverse_iterator crend() const /*throw(std::runtime_error)*/
    {
        return const_reverse_iterator(begin());
    }

    const_reverse_iterator rend() const /*throw(std::runtime_error)*/
    {
        return crend();
    }

    const_reference front() const /*throw(std::runtime_error)*/
    {
        BOOST_ASSERT(!empty());
        return *begin();
    }

    reference front() /*throw(std::runtime_error)*/
    {
        BOOST_ASSERT(!empty());
        return *begin();
    }

    const_reference back() const /*throw(std::runtime_error)*/
    {
        BOOST_ASSERT(!empty());
        return *(--cend());
    }

    reference back() /*throw(std::runtime_error)*/
    {
        BOOST_ASSERT(!empty());
        return *(--end());
    }

    const_reference operator[] (size_type index) const; /*throw(std::runtime_error)*/

    reference operator[](size_type index);

    const_reference at(size_type index) const /*throw(std::runtime_error)*/
    {
        if (index < 0 || index >= size())
            throw std::out_of_range("out of range");
        return (*this)[index];
    }

    reference at(size_type index) /*throw(std::runtime_error)*/
    {
        if (index < 0 || index >= size())
            throw std::out_of_range("out of range");
        return (*this)[index];
    }
#pragma endregion

#pragma region Mutators
    BinaryData &operator=(const BinaryData &that)
    {
        _assign(that);
        return *this;
    }

    BinaryData &operator=(const std::vector<T>& source)
    {
        _assign(source);
        return *this;
    }

    void swap(BinaryData &that) /*throw()*/
    {
        _swap(that);
    }

    void swap(std::vector<T> &that) /*throw()*/
    {
        _swap(that);
    }

    template <typename Iter>
    void assign(const Iter& first, const Iter& last)
    {
        clear();
        insert(end(), first, last);
    }

    // Insert an element BEFORE i within the vector.
    // Call insert(end(), x) or push_back(x) to append.
    iterator insert(iterator i, const T& x = T()) /*throw(std::runtime_error)*/
    {
        BOOST_ASSERT(i >= begin() && i <= end());
        size_t Offset = i - begin();
        insert(i, 1, x);
        return begin() + Offset;
    }

    // Insert a repetition of x BEFORE i within the vector.
    void insert(iterator i, size_type n, const T &x) /*throw(std::runtime_error)*/
    {
        BOOST_ASSERT(i >= begin() && i <= end());
        size_t OldSize = size();
        size_type offset = i - begin();
        resize(OldSize + n);
        std::copy_backward(begin() + offset, begin() + OldSize, end());
        std::fill(begin() + offset, begin() + offset + n, x);
    }

    // Insert a sequence of elements BEFORE i within the vector.
    template<typename Iter>
    void insert(iterator i, const Iter& first, const Iter& last) /*throw(std::runtime_error)*/
    {
        BOOST_ASSERT(last >= first);
        BOOST_ASSERT(i >= begin() && i <= end());
        size_t count = last - first;
        if (count == 0)
            return;
        size_t offset = i - begin(), old_size = size();
        resize(old_size + count);
        for (iterator j = begin() + old_size, k = end(); j != begin() + offset; )
            std::iter_swap(--j, --k);
        std::copy(first, last, begin() + offset);
    }

    iterator erase(iterator i)
    {
        difference_type Offset = i - begin();
        std::copy(i + 1, end(), i);
        pop_back();
        return begin() + Offset;
    }

    iterator erase(iterator From, iterator To)
    {
        difference_type Offset = From - begin();
        iterator i = std::copy(To, end(), From);
        resize(i - begin());
        return begin() + Offset;
    }

    void resize(size_type elements, const T &FillWith)
    {
        _resize(elements, FillWith);
    }

    void resize(size_type elements)
    {
        _resize(elements);
    }

    void push_back(const T &value) /*throw(std::runtime_error)*/
    {
        _resize(size() + 1);
        back() = value;
    }

    void pop_back() /*throw(std::runtime_error)*/
    {
        _resize(size() - 1);
    }

    void clear()
    {
        _resize(0);
    }
#pragma endregion

    void* managedStorage() const;

    //operator std::vector<T>&(); // not compatible with caching iterators
    operator const std::vector<T>&() const;
    //operator std::vector<T>() const;

    private:
    class Impl;

#ifdef WIN32
#pragma warning(push)
#pragma warning(disable: 4251)
    std::unique_ptr<Impl> _impl;
#pragma warning(pop)
#else
    std::unique_ptr<Impl> _impl;
#endif

    void _alloc(size_type elements, const T & t);
    void _reserve(size_type elements);
    void _resize(size_type elements);
    void _resize(size_type elements, const T & FillWith);
    void _swap(BinaryData & that);
    void _swap(std::vector<T>& that);
    void _assign(const BinaryData & that);
    void _assign(const std::vector<T>& that);
    size_type _size() const;
    size_type _capacity() const;
};

} // util
} // pwiz


namespace std {
template <typename T> void swap(pwiz::util::BinaryData<T>& lhs, std::vector<T>& rhs) { lhs.swap(rhs); }
template <typename T> void swap(std::vector<T>& lhs, pwiz::util::BinaryData<T>& rhs) { rhs.swap(lhs); }
}

#ifdef __cplusplus_cli
#pragma managed(pop)
#endif

#endif //__BINARYDATA_HPP__
