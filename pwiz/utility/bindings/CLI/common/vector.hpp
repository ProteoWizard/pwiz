//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
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

#ifndef _PWIZ_CLI_VECTOR_
#define _PWIZ_CLI_VECTOR_

#include <vector>

#ifndef INTERNAL
#define INTERNAL internal
#endif


#define RANGE_CHECK(index) \
    if (index < 0 || index >= static_cast<int>(base().size())) \
        throw gcnew System::IndexOutOfRangeException(); 

#define DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIHandle, NativeToCLI, CLIToNative) \
ref class WrapperName : public System::Collections::Generic::IList<CLIHandle> \
{ \
    public:   System::IntPtr void_base() {return (System::IntPtr) base_;} \
    INTERNAL: typedef std::vector<NativeType> WrappedType; \
\
              /* void* downcast is needed for cross-assembly calls; */ \
              /* native types are private by default and */ \
              /* #pragma make_public doesn't work on templated types */ \
              WrapperName(void* base, System::Object^ owner) : base_(static_cast<WrappedType*>(base)), owner_(owner) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(WrapperName))} \
              WrapperName(void* base) : base_(static_cast<WrappedType*>(base)), owner_(nullptr) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(WrapperName))} \
\
              virtual ~WrapperName() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(WrapperName), (owner_ == nullptr)) if (owner_ == nullptr) SAFEDELETE(base_);} \
              !WrapperName() {delete this;} \
              WrappedType* base_; \
              System::Object^ owner_; \
              WrappedType& base() {return *base_;} \
              WrappedType& assign(WrapperName^ rhs) {return base() = rhs->base();} \
\
    public: WrapperName() : base_(new WrappedType()) {} \
\
    property int Count { virtual int get() {return (int) base_->size();} } \
    property bool IsReadOnly { virtual bool get() {return false;} } \
\
    property CLIHandle Item[int] \
    { \
        virtual CLIHandle get(int index) {RANGE_CHECK(index) return NativeToCLI(NativeType, CLIType, (*base_)[(size_t) index]);} \
        virtual void set(int index, CLIHandle value) {RANGE_CHECK(index) (*base_)[(size_t) index] = CLIToNative(NativeType, value);} \
    } \
\
    virtual void Add(CLIHandle item) {base_->push_back(CLIToNative(NativeType, item));} \
    virtual void Clear() {base_->clear();} \
    virtual bool Contains(CLIHandle item) {return std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item)) != base_->end();} \
    virtual void CopyTo(array<CLIHandle>^ arrayTarget, int arrayIndex) \
    { \
        ValidateCopyToArrayArgs(arrayTarget, arrayIndex, base_->size()); \
        for (int i = 0; i < (int) base_->size(); i++) {arrayTarget[i + arrayIndex] = NativeToCLI(NativeType, CLIType, (*base_)[i]);} \
    } \
    virtual bool Remove(CLIHandle item) {WrappedType::iterator itr = std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item)); if(itr == base_->end()) return false; base_->erase(itr); return true;} \
    virtual int IndexOf(CLIHandle item) {return (int) (std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item))-base_->begin());} \
    virtual void Insert(int index, CLIHandle item) {base_->insert(base_->begin() + index, CLIToNative(NativeType, item));} \
    virtual void RemoveAt(int index) {RANGE_CHECK(index) base_->erase(base_->begin() + index);} \
\
    ref struct Enumerator : System::Collections::Generic::IEnumerator<CLIHandle> \
    { \
        Enumerator(WrappedType* base) : base_(base), itr_(new WrappedType::iterator), owner_(nullptr), isReset_(true) {} \
        Enumerator(WrappedType* base, System::Object^ owner) : base_(base), itr_(new WrappedType::iterator), owner_(owner), isReset_(true) {} \
\
        property CLIHandle Current { virtual CLIHandle get() {return NativeToCLI(NativeType, CLIType, **itr_);} } \
        property System::Object^ Current2 { virtual System::Object^ get() sealed = System::Collections::IEnumerator::Current::get {return (System::Object^) NativeToCLI(NativeType, CLIType, **itr_);} } \
        virtual bool MoveNext() \
        { \
            if (base_->empty()) return false; \
            else if (isReset_) {isReset_ = false; *itr_ = base_->begin();} \
            else if (*itr_+1 == base_->end()) return false; \
            else ++*itr_; \
            return true; \
        } \
        virtual void Reset() {isReset_ = true; *itr_ = base_->end();} \
        ~Enumerator() {delete itr_;} \
\
        internal: \
        WrappedType* base_; \
        WrappedType::iterator* itr_; \
        System::Object^ owner_; \
        bool isReset_; \
    }; \
\
    virtual System::Collections::Generic::IEnumerator<CLIHandle>^ GetEnumerator() {return gcnew Enumerator(base_, this);} \
    virtual System::Collections::IEnumerator^ GetEnumerator2() sealed = System::Collections::IEnumerable::GetEnumerator {return gcnew Enumerator(base_, this);} \
};

#define DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(WrapperName, NativeType, CLIType, NativeToCLI, CLIToNative) \
    DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIType^, NativeToCLI, CLIToNative)
#define DEFINE_STD_VECTOR_WRAPPER_FOR_VALUE_TYPE(WrapperName, NativeType, CLIType, NativeToCLI, CLIToNative) \
    DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIType, NativeToCLI, CLIToNative)

template<class CLIHandle> inline void ValidateCopyToArrayArgs(array<CLIHandle>^ array, int arrayIndex, size_t elementCount)
{
    if (!array)
        throw gcnew System::ArgumentNullException("array");
    if (array->Rank != 1)
        throw gcnew System::ArgumentException("Array must be of rank 1");
    if (array->GetLowerBound(0) != 0)
        throw gcnew System::ArgumentException("Array must have lower bound 0");
    if (arrayIndex < 0)
        throw gcnew System::ArgumentOutOfRangeException("arrayIndex");
    if (elementCount >= INT_MAX || array->Length - arrayIndex < (int) elementCount)
        throw gcnew System::ArgumentException("Insufficient space");
}

#endif // _PWIZ_CLI_VECTOR_
