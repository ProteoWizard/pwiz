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


#include <map>

#ifndef INTERNAL
#define INTERNAL internal
#endif

#define DEFINE_STD_MAP_WRAPPER(WrapperName, NativeKeyType, CLIKeyType, CLIKeyHandle, NativeKeyToCLI, CLIKeyToNative, NativeValueType, CLIValueType, CLIValueHandle, NativeValueToCLI, CLIValueToNative) \
ref class WrapperName : public System::Collections::Generic::IDictionary<CLIKeyHandle, CLIValueHandle> \
{ \
    public:   System::IntPtr void_base() {return (System::IntPtr) base_;} \
    INTERNAL: typedef std::map<NativeKeyType, NativeValueType> WrappedType; \
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
    property CLIValueHandle Item[CLIKeyHandle] \
    { \
        virtual CLIValueHandle get(CLIKeyHandle key) {return NativeValueToCLI(NativeValueType, CLIValueType, (*base_)[CLIKeyToNative(NativeKeyType, key)]);} \
        virtual void set(CLIKeyHandle key, CLIValueHandle value) {(*base_)[CLIKeyToNative(NativeKeyType, key)] = CLIValueToNative(NativeValueType, value);} \
    } \
\
    property System::Collections::Generic::ICollection<CLIKeyHandle>^ Keys \
    { \
        virtual System::Collections::Generic::ICollection<CLIKeyHandle>^ get() \
        { \
            System::Collections::Generic::List<CLIKeyHandle>^ keys = gcnew System::Collections::Generic::List<CLIKeyHandle>(); \
            for(WrappedType::iterator itr = base_->begin(); itr != base_->end(); ++itr) \
                keys->Add(NativeKeyToCLI(NativeKeyType, CLIKeyType, itr->first)); \
            return keys; \
        } \
    } \
\
    property System::Collections::Generic::ICollection<CLIValueHandle>^ Values \
    { \
        virtual System::Collections::Generic::ICollection<CLIValueHandle>^ get() \
        { \
            System::Collections::Generic::List<CLIValueHandle>^ values = gcnew System::Collections::Generic::List<CLIValueHandle>(); \
            for(WrappedType::iterator itr = base_->begin(); itr != base_->end(); ++itr) \
                values->Add(NativeValueToCLI(NativeValueType, CLIValueType, itr->second)); \
            return values; \
        } \
    } \
\
    virtual void Add(CLIKeyHandle key, CLIValueHandle value) {base_->insert(std::make_pair(CLIKeyToNative(NativeKeyType, key), CLIValueToNative(NativeValueType, value)));} \
    virtual bool ContainsKey(CLIKeyHandle key) {return base_->count(CLIKeyToNative(NativeKeyType, key)) != 0;} \
    virtual bool Remove(CLIKeyHandle key) {return base_->erase(CLIKeyToNative(NativeKeyType, key)) != 0;} \
\
    virtual bool TryGetValue(CLIKeyHandle key, CLIValueHandle % value) \
    { \
        WrappedType::iterator itr = base_->find(CLIKeyToNative(NativeKeyType, key)); \
        if(itr != base_->end()) \
        { \
            value = NativeValueToCLI(NativeValueType, CLIValueType, itr->second); \
            return true; \
        } else \
            return false; \
    } \
\
    virtual void Clear() {base_->clear();} \
\
    virtual void Add(System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle> kvp) {base_->insert(std::make_pair(CLIKeyToNative(NativeKeyType, kvp.Key), CLIValueToNative(NativeValueType, kvp.Value)));} \
    virtual bool Contains(System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle> kvp) {return base_->count(CLIKeyToNative(NativeKeyType, kvp.Key)) != 0;} \
    virtual bool Remove(System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle> kvp) {return base_->erase(CLIKeyToNative(NativeKeyType, kvp.Key)) != 0;} \
    virtual void CopyTo(array< System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle> >^ arrayTarget, int arrayIndex) {throw gcnew System::Exception("method not implemented");} \
\
    ref class Enumerator : System::Collections::Generic::IEnumerator< System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle> > \
    { \
        internal: WrappedType* base_; \
                  WrappedType::iterator* itr_; \
                  bool isReset_; \
\
        public: Enumerator(WrappedType* base) : base_(base), itr_(new WrappedType::iterator), isReset_(true) {} \
\
        property System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle> Current \
        { \
            virtual System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle> get() \
            { \
                CLIKeyHandle key = NativeKeyToCLI(NativeKeyType, CLIKeyType, (*itr_)->first); \
                CLIValueHandle value = NativeValueToCLI(NativeValueType, CLIValueType, (*itr_)->second); \
                return System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle>(key, value); \
            } \
        } \
\
        property System::Object^ Current2 \
        { \
            virtual System::Object^ get() sealed = System::Collections::IEnumerator::Current::get \
            { \
                CLIKeyHandle key = NativeKeyToCLI(NativeKeyType, CLIKeyType, (*itr_)->first); \
                CLIValueHandle value = NativeValueToCLI(NativeValueType, CLIValueType, (*itr_)->second); \
                return (System::Object^) System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle>(key, value); \
            } \
        } \
\
        virtual bool MoveNext() \
        { \
            if (base_->empty()) return false; \
            else if (isReset_) {isReset_ = false; *itr_ = base_->begin();} \
            else if (&**itr_ == &*base_->rbegin()) return false; \
            else ++*itr_; \
            return true; \
        } \
        virtual void Reset() {isReset_ = true; *itr_ = base_->end();} \
        ~Enumerator() {delete itr_;} \
    }; \
\
    virtual System::Collections::Generic::IEnumerator< System::Collections::Generic::KeyValuePair<CLIKeyHandle, CLIValueHandle> >^ GetEnumerator() {return gcnew Enumerator(base_);} \
    virtual System::Collections::IEnumerator^ GetEnumerator2() sealed = System::Collections::IEnumerable::GetEnumerator {return gcnew Enumerator(base_);} \
};
