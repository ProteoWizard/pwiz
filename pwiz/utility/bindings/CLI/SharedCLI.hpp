#ifndef _SHAREDCLI_HPP_
#define _SHAREDCLI_HPP_

#include <stdlib.h>
#include <vcclr.h>
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>

inline std::string ToStdString(System::String^ source)
{
	int len = (( source->Length+1) * 2);
	char *ch = new char[ len ];
	bool result ;
	{
		pin_ptr<const wchar_t> wch = PtrToStringChars( source );
		result = wcstombs( ch, wch, len ) != -1;
	}
	std::string target = ch;
	delete ch;
	if(!result)
        throw gcnew System::Exception("error converting System::String to std::string");
	return target;
}

template<typename value_type>
std::vector<value_type> ToStdVector(cli::array<value_type>^ valueArray)
{
    pin_ptr<value_type> pin = &valueArray[0];
    value_type* begin = (value_type*) pin;
    return std::vector<value_type>(begin, begin + valueArray->Length);
}

#define DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIHandle, NativeToCLI, CLIToNative) \
public ref class WrapperName : public System::Collections::Generic::IList<CLIHandle> \
{ \
    internal: WrapperName(std::vector<NativeType>* base) : base_(base) {} \
              virtual ~WrapperName() {if (base_) delete base_;} \
              std::vector<NativeType>* base_; \
    \
    public: WrapperName() : base_(new std::vector<NativeType>()) {} \
    \
    public: \
    property int Count { virtual int get() {return (int) base_->size();} } \
    property bool IsReadOnly { virtual bool get() {return false;} } \
    \
    property CLIHandle Item[int] \
    { \
        virtual CLIHandle get(int index) {return NativeToCLI(CLIType, base_->at((size_t) index));} \
        virtual void set(int index, CLIHandle value) {} \
    } \
    \
    virtual void Add(CLIHandle item) {base_->push_back(CLIToNative(NativeType, item));} \
    virtual void Clear() {base_->clear();} \
    virtual bool Contains(CLIHandle item) {return std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item)) != base_->end();} \
    virtual void CopyTo(array<CLIHandle>^ arrayTarget, int arrayIndex) {} \
    virtual bool Remove(CLIHandle item) {std::vector<NativeType>::iterator itr = std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item)); if(itr == base_->end()) return false; base_->erase(itr); return true;} \
    virtual int IndexOf(CLIHandle item) {return (int) (std::find(base_->begin(), base_->end(), CLIToNative(NativeType, item))-base_->begin());} \
    virtual void Insert(int index, CLIHandle item) {base_->insert(base_->begin() + index, CLIToNative(NativeType, item));} \
    virtual void RemoveAt(int index) {base_->erase(base_->begin() + index);} \
    \
    ref class Enumerator : System::Collections::Generic::IEnumerator<CLIHandle> \
    { \
        public: Enumerator(std::vector<NativeType>* base) : base_(base), itr_(new std::vector<NativeType>::iterator), isReset_(true) {} \
        internal: std::vector<NativeType>* base_; \
                  std::vector<NativeType>::iterator* itr_; \
                  bool isReset_; \
        \
        public: \
        property CLIHandle Current { virtual CLIHandle get() {return NativeToCLI(CLIType, **itr_);} } \
        property System::Object^ Current2 { virtual System::Object^ get() sealed = System::Collections::IEnumerator::Current::get {return (System::Object^) NativeToCLI(CLIType, **itr_);} } \
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
    virtual System::Collections::Generic::IEnumerator<CLIHandle>^ GetEnumerator() {return gcnew Enumerator(base_);} \
    virtual System::Collections::IEnumerator^ GetEnumerator2() sealed = System::Collections::IEnumerable::GetEnumerator {return gcnew Enumerator(base_);} \
};


#define DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(WrapperName, NativeType, CLIType, NativeToCLI, CLIToNative) \
    DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIType^, NativeToCLI, CLIToNative)
#define DEFINE_STD_VECTOR_WRAPPER_FOR_VALUE_TYPE(WrapperName, NativeType, CLIType, NativeToCLI, CLIToNative) \
    DEFINE_STD_VECTOR_WRAPPER(WrapperName, NativeType, CLIType, CLIType, NativeToCLI, CLIToNative)

#define NATIVE_SHARED_PTR_TO_CLI(CLIType, SharedPtr) ((SharedPtr).get() ? gcnew CLIType(&(SharedPtr)) : nullptr)
#define NATIVE_REFERENCE_TO_CLI(CLIType, NativeRef) gcnew CLIType(&(NativeRef))
#define NATIVE_VALUE_TO_CLI(CLIType, NativeValue) ((CLIType) NativeValue)
#define STD_STRING_TO_CLI_STRING(CLIType, StdString) gcnew CLIType((StdString).c_str())

#define CLI_TO_NATIVE_SHARED_PTR(NativeType, CLIObject) NativeType(*(CLIObject)->base_)
#define CLI_TO_NATIVE_REFERENCE(NativeType, CLIObject) NativeType(*(CLIObject)->base_)
#define CLI_VALUE_TO_NATIVE_VALUE(NativeType, CLIObject) ((NativeType) CLIObject)
#define CLI_STRING_TO_STD_STRING(NativeType, CLIObject) ToStdString(CLIObject)


#define DEFINE_INTERNAL_BASE_CODE(ClassType) \
internal: ClassType(pwiz::msdata::ClassType* base) : base_(base) {} \
          virtual ~ClassType() {if (base_) delete base_;} \
          pwiz::msdata::ClassType* base_;

#define DEFINE_DERIVED_INTERNAL_BASE_CODE(ClassType, BaseClassType) \
internal: ClassType(pwiz::msdata::ClassType* base) : BaseClassType(base), base_(base) {} \
          virtual ~ClassType() {if (base_) delete base_;} \
          pwiz::msdata::ClassType* base_;

#define DEFINE_SHARED_INTERNAL_BASE_CODE(ClassType) \
internal: ClassType(boost::shared_ptr<pwiz::msdata::ClassType>* base) : base_(base) {} \
          virtual ~ClassType() {if (base_) delete base_;} \
          boost::shared_ptr<pwiz::msdata::ClassType>* base_;

#define DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(ClassType, BaseClassType) \
internal: ClassType(boost::shared_ptr<pwiz::msdata::ClassType>* base) : BaseClassType((pwiz::msdata::BaseClassType*) &**base), base_(base) {} \
          virtual ~ClassType() {if (base_) delete base_;} \
          boost::shared_ptr<pwiz::msdata::ClassType>* base_;

#define DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(ClassType, BaseClassType) \
internal: ClassType(boost::shared_ptr<pwiz::msdata::ClassType>* base) : BaseClassType((boost::shared_ptr<pwiz::msdata::BaseClassType>*) base), base_(base) {} \
          virtual ~ClassType() {if (base_) delete base_;} \
          boost::shared_ptr<pwiz::msdata::ClassType>* base_;


#endif // _SHAREDCLI_HPP_
