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

#ifndef _PWIZ_CLI_BINARYDATA_
#define _PWIZ_CLI_BINARYDATA_

#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "pwiz/data/msdata/MSData.hpp"
#pragma warning( pop )

using namespace System;

#include "vector.hpp" // for RANGE_CHECK and ValidateCopyToArrayArgs
#include "SharedCLI.hpp"
#include <cstring>

#ifndef INTERNAL
#define INTERNAL internal
#endif

namespace pwiz {
namespace CLI {
namespace util {

/// <summary>
/// Wrapper for binary data from spectra and chromatograms, accessible as IList&lt;double&gt; the underlying data may be stored in a native std::vector or in a managed cli::array.
/// Call Storage() to get access to the managed array: if the underlying storage is a std::vector, it will be copied to a managed array before returning.
/// </summary>
template <typename ValueType>
public ref class BinaryData : public System::Collections::Generic::IList<ValueType>
{
public:

    System::IntPtr void_base() { return (System::IntPtr) base_; }

INTERNAL:
    typedef pwiz::util::BinaryData<ValueType> binarydata_type;
    typedef binarydata_type* binarydataptr_type;
    typedef typename binarydata_type::iterator iterator_type;

    /* void* downcast is needed for cross-assembly calls; */
    /* native types are private by default and */
    /* #pragma make_public doesn't work on templated types */
    BinaryData(void* base, System::Object^ owner) : base_(static_cast<binarydataptr_type>(base)), baseRef_((*base_)), owner_(owner) {}
    BinaryData(void* base) : base_(static_cast<binarydataptr_type>(base)), baseRef_((*base_)), owner_(nullptr) {}

    virtual ~BinaryData() { if (owner_ == nullptr && base_ != NULL) delete base_; }
    !BinaryData() { delete this; }
    binarydataptr_type base_;
    binarydata_type& baseRef_;
    System::Object^ owner_;
    binarydata_type& base() { return baseRef_; }
    binarydata_type& assign(BinaryData<ValueType>^ rhs) { return base() = rhs->base(); }

public:

    BinaryData() : base_(new binarydata_type()), baseRef_((*base_)) {}

    property int Count { virtual int get() { return (int)base().size(); } }
    property bool IsReadOnly { virtual bool get() { return false; } }

    property ValueType default[int]
    {
        virtual ValueType get(int index) { RANGE_CHECK(index) return base()[(size_t)index]; }
        virtual void set(int index, ValueType value) { RANGE_CHECK(index) base()[(size_t)index] = value; }
    }

    virtual void Add(ValueType item) { base().push_back(item); }
    virtual void Clear() { base().clear(); }
    virtual bool Contains(ValueType item) { return std::find(base().begin(), base().end(), item) != base().end(); }

    virtual void CopyTo(array<ValueType>^ arrayTarget, int arrayIndex)
    {
        ValidateCopyToArrayArgs(arrayTarget, arrayIndex, base().size());
        pin_ptr<ValueType> pinnedArray = &arrayTarget[0];
        memcpy((ValueType*)pinnedArray + arrayIndex, &base()[0], base().size());
    }
    virtual bool Remove(ValueType item) { auto itr = std::find(base().begin(), base().end(), item); if (itr == base().end()) return false; base().erase(itr); return true; }
    virtual int IndexOf(ValueType item) { return (int)(std::find(base().begin(), base().end(), item) - base().begin()); }
    virtual void Insert(int index, ValueType item) { base().insert(base().begin() + index, item); }
    virtual void RemoveAt(int index) { RANGE_CHECK(index) base().erase(base().begin() + index); }

    /// <summary>
    /// Returns a managed array storing the underlying data; if the underlying storage is a std::vector, it will be copied to a managed array before returning.
    /// </summary>
    virtual cli::array<ValueType>^ Storage()
    {
        try
        {
            System::Runtime::InteropServices::GCHandle handle = (System::Runtime::InteropServices::GCHandle) System::IntPtr(base().managedStorage());
            return (cli::array<ValueType>^) handle.Target;
        }
        CATCH_AND_FORWARD
    }

    ref struct Enumerator : System::Collections::Generic::IEnumerator<ValueType>
    {
        Enumerator(binarydataptr_type base) : base_(base), itr_(new iterator_type), end_(new iterator_type(base_->end())), owner_(nullptr), isReset_(true) {}
        Enumerator(binarydataptr_type base, System::Object^ owner) : base_(base), itr_(new iterator_type), end_(new iterator_type(base_->end())), owner_(owner), isReset_(true) {}

        property ValueType Current { virtual ValueType get() { return **itr_; } }
        property System::Object^ Current2 { virtual System::Object^ get() sealed = System::Collections::IEnumerator::Current::get{ return (System::Object^) **itr_; } }
        virtual bool MoveNext()
        {
            /*if (base().empty()) return false;
            else */if (isReset_) { isReset_ = false; *itr_ = base().begin(); }
            else if (*itr_ + 1 == *end_) return false;
            else ++*itr_;
            return true;
        }
        virtual void Reset() { isReset_ = true; *itr_ = *end_ = base().end(); }
        ~Enumerator() { delete itr_; delete end_; }

    internal:
        typedef pwiz::util::BinaryData<ValueType> binarydata_type;
        typedef binarydata_type* binarydataptr_type;
        typedef typename binarydata_type::iterator iterator_type;

        binarydataptr_type base_;
        iterator_type* itr_;
        iterator_type* end_;
        System::Object^ owner_;
        bool isReset_;
        binarydata_type& base() { return (*base_); }
    };

    virtual System::Collections::Generic::IEnumerator<ValueType>^ GetEnumerator() { return gcnew Enumerator(base_, this); }
    virtual System::Collections::IEnumerator^ GetEnumerator2() sealed = System::Collections::IEnumerable::GetEnumerator{ return gcnew Enumerator(base_, this); }
};


// unfortunately C# can't use C++ template instantiations directly, even through typedefs or type aliases,
// so these subclasses make the instantiations accessible

public ref class BinaryDataDouble : public BinaryData<double>
{
    INTERNAL:
    BinaryDataDouble(void* base, System::Object^ owner) : BinaryData(base, owner) {}
    BinaryDataDouble(void* base) : BinaryData(base) {}

    public:
    BinaryDataDouble() : BinaryData() {}
};

public ref class BinaryDataInteger : public BinaryData<System::Int64>
{
    INTERNAL:
    BinaryDataInteger(void* base, System::Object^ owner) : BinaryData(base, owner) {}
    BinaryDataInteger(void* base) : BinaryData(base) {}

    public:
    BinaryDataInteger() : BinaryData() {}
};


} // namespace util
} // namespace CLI
} // namespace pwiz

#endif // _PWIZ_CLI_BINARYDATA_
