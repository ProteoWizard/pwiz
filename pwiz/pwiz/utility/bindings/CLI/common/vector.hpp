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


#include <vector>

#ifndef INTERNAL
#define INTERNAL internal
#endif

#define DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIHandle, NativeToCLI, CLIToNative) \
public ref class WrapperName : public System::Collections::Generic::IList<CLIHandle> \
{ \
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
        virtual CLIHandle get(int index) {return NativeToCLI(NativeType, CLIType, (*base_)[(size_t) index]);} \
        virtual void set(int index, CLIHandle value) {(*base_)[(size_t) index] = CLIToNative(NativeType, value);} \
    } \
\
    virtual void Add(CLIHandle item) {base_->push_back(CLIToNative(NativeType, item));} \
    virtual void Clear() {base_->clear();} \
    virtual bool Contains(CLIHandle item) {return std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item)) != base_->end();} \
    virtual void CopyTo(array<CLIHandle>^ arrayTarget, int arrayIndex) {throw gcnew System::Exception("method not implemented");} \
    virtual bool Remove(CLIHandle item) {WrappedType::iterator itr = std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item)); if(itr == base_->end()) return false; base_->erase(itr); return true;} \
    virtual int IndexOf(CLIHandle item) {return (int) (std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item))-base_->begin());} \
    virtual void Insert(int index, CLIHandle item) {base_->insert(base_->begin() + index, CLIToNative(NativeType, item));} \
    virtual void RemoveAt(int index) {base_->erase(base_->begin() + index);} \
\
    ref class Enumerator : System::Collections::Generic::IEnumerator<CLIHandle> \
    { \
        public: Enumerator(WrappedType* base) : base_(base), itr_(new WrappedType::iterator), isReset_(true) {} \
        internal: WrappedType* base_; \
                  WrappedType::iterator* itr_; \
                  bool isReset_; \
\
        public: \
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
    }; \
\
    virtual System::Collections::Generic::IEnumerator<CLIHandle>^ GetEnumerator() {return gcnew Enumerator(base_);} \
    virtual System::Collections::IEnumerator^ GetEnumerator2() sealed = System::Collections::IEnumerable::GetEnumerator {return gcnew Enumerator(base_);} \
};

#define DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(WrapperName, NativeType, CLIType, NativeToCLI, CLIToNative) \
    DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIType^, NativeToCLI, CLIToNative)
#define DEFINE_STD_VECTOR_WRAPPER_FOR_VALUE_TYPE(WrapperName, NativeType, CLIType, NativeToCLI, CLIToNative) \
    DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIType, NativeToCLI, CLIToNative)
