//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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

#define PWIZ_SOURCE

#include "BinaryData.hpp"
#include "optimized_lexical_cast.hpp"

#ifdef __cplusplus_cli
#define PWIZ_MANAGED_PASSTHROUGH
#endif

#ifdef PWIZ_MANAGED_PASSTHROUGH
#pragma managed
#include "pinned_gcroot.h"
#endif

namespace pwiz {
namespace util {

#ifdef PWIZ_MANAGED_PASSTHROUGH
typedef System::Runtime::InteropServices::GCHandle GCHandle;
typedef System::Runtime::InteropServices::GCHandleType GCHandleType;
#define __GCHANDLE_TO_VOIDPTR(x) ((GCHandle::operator System::IntPtr(x)).ToPointer())
#define __VOIDPTR_TO_GCHANDLE(x) (GCHandle::operator GCHandle(System::IntPtr(x)))
#endif

template <typename T>
class BinaryData<T>::Impl
{
    public:
    Impl() :
    #ifdef PWIZ_MANAGED_PASSTHROUGH
        managedStorage_(),
    #endif
        nativeStorage_()
    {}

    Impl(void* cliNumericArray)
    {      
#ifdef PWIZ_MANAGED_PASSTHROUGH
        GCHandle handle = __VOIDPTR_TO_GCHANDLE(cliNumericArray); // freed by caller
        managedStorage_ = (cli::array<T>^) handle.Target;
#else
        throw std::runtime_error("[BinaryData<T>::ctor(void*)] only supported with MSVC C++/CLI");
#endif
    }

    ~Impl()
    {
    }

    Impl& operator=(void* cliNumericArray)
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        GCHandle handle = __VOIDPTR_TO_GCHANDLE(cliNumericArray); // freed by caller
        managedStorage_ = (cli::array<T>^) handle.Target;
        nativeStorage_.clear();
        return *this;
#else
        throw std::runtime_error("[BinaryData<T>::operator=(void*)] only supported with MSVC C++/CLI");
#endif
    }

    void _alloc(size_type elements, const T &t)
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr)
        {
            managedStorage_ = gcnew array<T>((int)elements);
            if (t != T())
                std::fill(begin_, begin_ + elements, t);
            nativeStorage_.clear();
            return;
        }
#endif
        nativeStorage_.assign(elements, t);
    }

    void _resize(size_type elements)
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr)
        {
            cli::array<T>^% storageRef = (array<T>^) managedStorage_;
            System::Array::Resize<T>(storageRef, (int)elements);
            managedStorage_ = storageRef;
            nativeStorage_.clear();
            return;
        }
#endif
        nativeStorage_.resize(elements);
    }

    void _resize(size_type elements, const T &FillWith)
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr)
        {
            cli::array<T>^% storageRef = (array<T>^) managedStorage_;
            System::Array::Resize<T>(storageRef, (int)elements);
            managedStorage_ = storageRef;
            std::fill(begin_, end_, FillWith);
            nativeStorage_.clear();
            return;
        }
#endif
        nativeStorage_.resize(elements, FillWith);
    }

    void _swap(std::vector<T>& that)
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr)
        {
            if (managedStorage_->Length == that.size())
            {
                // swap the managed array to the vector and vice versa
                pin_ptr<T> pinnedArrayPtr = &managedStorage_[0];
                T* nativeArrayPtr = &that[0];
                std::swap_ranges(nativeArrayPtr, nativeArrayPtr + that.size(), (T*)&pinnedArrayPtr[0]);
            }
            else
            {
                // create temporary managed array and copy over the native array's contents
                auto tmp = gcnew cli::array<T>((int)that.size());
                {
                    pin_ptr<T> pinnedTmpPtr = &tmp[0];
                    //memcpy(&pinnedTmpPtr[0], &that[0], that.size());
                    for (int i = 0; i < that.size(); ++i)
                        pinnedTmpPtr[i] = that[i];
                }

                // copy the managed array's contents to the native array
                {
                    pin_ptr<T> pinnedArrayPtr = &managedStorage_[0];
                    that.resize(managedStorage_->Length);
                    memcpy(&that[0], &pinnedArrayPtr[0], that.size() * sizeof(T));
                }

                // replace the managed array with the temporary one
                managedStorage_ = tmp;
            }

            nativeStorage_.clear();
            return;
        }
#endif
        nativeStorage_.swap(that);
    }

    void* managedStorage()
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ == nullptr)
        {
            managedStorage_ = gcnew cli::array<T>((int)nativeStorage_.size());

            if (nativeStorage_.empty())
                return managedStorage_.handle();

            // copy the source native array's contents to the target managed array
            pin_ptr<T> pinnedArrayPtr = &managedStorage_[0];
            memcpy(&pinnedArrayPtr[0], &nativeStorage_[0], nativeStorage_.size() * sizeof(T));
        }

        return managedStorage_.handle();
#else
        throw std::runtime_error("[BinaryData<T>::managedStorage()] only supported with MSVC C++/CLI");
#endif
    }

    void _assign(const Impl& that)
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (that.managedStorage_ != nullptr)
        {
            // if the lengths are not the same, the target array must be reallocated
            if (managedStorage_ == nullptr ||
                managedStorage_->Length != that.managedStorage_->Length)
            {
                managedStorage_ = gcnew cli::array<T>(that.managedStorage_->Length);
            }

            System::Array::Copy(that.managedStorage_, managedStorage_, managedStorage_->Length);
            return;
        }
#endif
        _assign(that.nativeStorage_);
    }

    void _assign(const std::vector<T>& that)
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr)
        {
            // if the lengths are not the same, the target array must be reallocated
            if (managedStorage_->Length != that.size())
                managedStorage_ = gcnew cli::array<T>((int)that.size());

            // copy the source native array's contents to the target managed array
            pin_ptr<T> pinnedArrayPtr = &managedStorage_[0];
            memcpy(&pinnedArrayPtr[0], &that[0], that.size() * sizeof(T));
            nativeStorage_.clear();
            return;
        }
#endif
        nativeStorage_ = that;
    }

    typename BinaryData<T>::size_type _size() const
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr)
            return managedStorage_->Length;
#endif
        return nativeStorage_.size();
    }

    typename BinaryData<T>::size_type _capacity() const
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr && managedStorage_->Length > nativeStorage_.capacity())
        {
            return managedStorage_->Length;
        }
#endif
        return nativeStorage_.capacity();
    }

    operator const std::vector<T>&() const
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr)
        {
            if (nativeStorage_.empty())
                nativeStorage_.insert(nativeStorage_.end(), cbegin_, cend_);
            else if (managedStorage_->Length != nativeStorage_.size())
                throw std::length_error("managed and native storage have different sizes (" +
                                        boost::lexical_cast<std::string>(managedStorage_->Length) +
                                        " vs. " + boost::lexical_cast<std::string>(nativeStorage_.size()) + ")");
        }
#endif
        return nativeStorage_;
    }

    template <typename Itr> void makeIterator(Itr& itr, bool begin)
    {
#ifdef PWIZ_MANAGED_PASSTHROUGH
        if (managedStorage_ != nullptr && managedStorage_->Length > 0)
        {
            //auto arrayPtr = (cli::array<T>^) binaryData._impl->managedStorage_;
            //pin_ptr<T> pinnedArrayPtr = &arrayPtr[0]; // the array is already pinned in binaryData, so when this goes out of scope it should not unpin
            T* pinnedArrayPtr = static_cast<T*>((&managedStorage_).ToPointer());
            itr.current_ = begin ? &pinnedArrayPtr[0] : (&pinnedArrayPtr[managedStorage_->Length - 1]) + 1;
            return;
        }
#endif
        if (!nativeStorage_.empty())
            itr.current_ = begin ? &nativeStorage_.front() : (&nativeStorage_.back()) + 1;
        else
            itr.current_ = 0;
    }

    void cacheIterators(BinaryData& binaryData)
    {
        begin_ = iterator(binaryData, true);
        end_ = iterator(binaryData, false);
        cbegin_ = const_iterator(binaryData, true);
        cend_ = const_iterator(binaryData, false);
    }

    #ifdef PWIZ_MANAGED_PASSTHROUGH
        pinned_gcroot<cli::array<T>^ > managedStorage_;
        mutable std::vector<T> nativeStorage_;
    #else
        std::vector<T> nativeStorage_;
    #endif

    iterator begin_, end_;
    const_iterator cbegin_, cend_;
};

#pragma managed(push, off)

PWIZ_API_DECL template <typename T> BinaryData<T>::BinaryData(size_type elements, T t)
    : _impl(new Impl)
{
    if (elements > 0)
        _alloc(elements, t);
}

PWIZ_API_DECL template <typename T> BinaryData<T>::BinaryData(const BinaryData &source)
    : _impl(new Impl)
{
    _assign(source);
}

PWIZ_API_DECL template <typename T> BinaryData<T>::BinaryData(const_iterator first, const_iterator last)
    : _impl(new Impl)
{
    std::uninitialized_copy(first, last, begin());
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> BinaryData<T>::~BinaryData() {}

PWIZ_API_DECL template <typename T> BinaryData<T>::BinaryData(void* cliNumericArray)
    : _impl(new Impl(cliNumericArray))
{
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> BinaryData<T>& BinaryData<T>::operator=(void* cliNumericArray)
{
    _impl->operator=(cliNumericArray);
    _impl->cacheIterators(*this);
    return *this;
}

PWIZ_API_DECL template <typename T> void BinaryData<T>::_alloc(size_type elements, const T &t)
{
    _impl->_alloc(elements, t);
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> void BinaryData<T>::_reserve(size_type elements)
{
#ifdef PWIZ_MANAGED_PASSTHROUGH
    // reserve only affects native storage
#endif
    _impl->nativeStorage_.reserve(elements);
}

PWIZ_API_DECL template <typename T> void BinaryData<T>::_resize(size_type elements)
{
    _impl->_resize(elements);
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> void BinaryData<T>::_resize(size_type elements, const T &FillWith)
{
    _impl->_resize(elements, FillWith);
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> void BinaryData<T>::_swap(BinaryData& that)
{
#ifdef PWIZ_MANAGED_PASSTHROUGH
    _impl->managedStorage_.swap(that._impl->managedStorage_); // swapping one or both nullptrs should work (?)
#endif
    _impl->nativeStorage_.swap(that._impl->nativeStorage_);
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> void BinaryData<T>::_swap(std::vector<T>& that)
{
    _impl->_swap(that);
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> void* BinaryData<T>::managedStorage() const
{
    return _impl->managedStorage();
}

PWIZ_API_DECL template <typename T> void BinaryData<T>::_assign(const BinaryData<T>& that)
{
    if (that.empty())
        return;

    _impl->_assign(*that._impl);
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> void BinaryData<T>::_assign(const std::vector<T>& that)
{
    if (that.empty())
    {
        clear();
        return;
    }

    _impl->_assign(that);
    _impl->cacheIterators(*this);
}

PWIZ_API_DECL template <typename T> typename BinaryData<T>::size_type BinaryData<T>::_size() const
{
    return _impl->_size();
}

PWIZ_API_DECL template <typename T> typename BinaryData<T>::size_type BinaryData<T>::_capacity() const
{
    return _impl->_capacity();
}

/*PWIZ_API_DECL template <typename T> BinaryData<T>::operator std::vector<T>&()
{
#ifdef PWIZ_MANAGED_PASSTHROUGH
    if (_impl->managedStorage_ != nullptr)
    {
        if (_impl->nativeStorage_.empty())
            _impl->nativeStorage_.insert(_impl->nativeStorage_.end(), cbegin(), cend());
        else if (_impl->managedStorage_->Length != _impl->nativeStorage_.size())
            throw std::length_error("managed and native storage have different sizes");
    }
#endif
    return _impl->nativeStorage_;
}*/

PWIZ_API_DECL template <typename T> BinaryData<T>::operator const std::vector<T>&() const
{
    return static_cast<const std::vector<T>&>(*_impl);
}

/*PWIZ_API_DECL template <typename T> BinaryData<T>::operator std::vector<double>() const
{
#ifdef PWIZ_MANAGED_PASSTHROUGH
    if (_impl->managedStorage_ != nullptr)
        return std::vector<double>(cbegin(), cend());
#endif
    return _impl->nativeStorage_;
}*/


PWIZ_API_DECL template <typename T> typename BinaryData<T>::const_reference BinaryData<T>::operator[] (size_type index) const
{
    BOOST_ASSERT(index >= 0 && index < size());
    BOOST_ASSERT(_impl->cbegin_.current_ != NULL);
    return _impl->cbegin_[index];
}

PWIZ_API_DECL template <typename T> typename BinaryData<T>::reference BinaryData<T>::operator[](size_type index)
{
    BOOST_ASSERT(index >= 0 && index < size());
    BOOST_ASSERT(_impl->cbegin_.current_ != NULL);
    return _impl->begin_[index];
}


PWIZ_API_DECL template <typename T> BinaryData<T>::const_iterator::const_iterator(const BinaryData& binaryData, bool begin)
{
    binaryData._impl->makeIterator(*this, begin);
}

PWIZ_API_DECL template <typename T> BinaryData<T>::iterator::iterator(BinaryData& binaryData, bool begin)
{
    binaryData._impl->makeIterator(*this, begin);
}

PWIZ_API_DECL template class BinaryData<double>;
PWIZ_API_DECL template class BinaryData<float>;

#pragma managed(pop)

} // util
} // pwiz
