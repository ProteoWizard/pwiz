#include <map>

#define DEFINE_STD_MAP_WRAPPER(WrapperName, NativeKeyType, CLIKeyType, CLIKeyHandle, NativeKeyToCLI, CLIKeyToNative, NativeValueType, CLIValueType, CLIValueHandle, NativeValueToCLI, CLIValueToNative) \
public ref class WrapperName : public System::Collections::Generic::IDictionary<CLIKeyHandle, CLIValueHandle> \
{ \
    internal: WrapperName(std::map<NativeKeyType, NativeValueType>* base, System::Object^ owner) : base_(base), owner_(owner) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(WrapperName))} \
              WrapperName(std::map<NativeKeyType, NativeValueType>* base) : base_(base), owner_(nullptr) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(WrapperName))} \
              virtual ~WrapperName() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(WrapperName), (owner_ == nullptr)) if (owner_ == nullptr) SAFEDELETE(base_);} \
              !WrapperName() {delete this;} \
              std::map<NativeKeyType, NativeValueType>* base_; \
              System::Object^ owner_; \
\
    public: ModificationBaseMap() : base_(new std::map<NativeKeyType, NativeValueType>()) {} \
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
            for(std::map<NativeKeyType, NativeValueType>::iterator itr = base_->begin(); itr != base_->end(); ++itr) \
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
            for(std::map<NativeKeyType, NativeValueType>::iterator itr = base_->begin(); itr != base_->end(); ++itr) \
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
        std::map<NativeKeyType, NativeValueType>::iterator itr = base_->find(CLIKeyToNative(NativeKeyType, key)); \
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
        internal: std::map<NativeKeyType, NativeValueType>* base_; \
                  std::map<NativeKeyType, NativeValueType>::iterator* itr_; \
                  bool isReset_; \
        \
        public: Enumerator(std::map<NativeKeyType, NativeValueType>* base) : base_(base), itr_(new std::map<NativeKeyType, NativeValueType>::iterator), isReset_(true) {} \
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
