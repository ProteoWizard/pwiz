#ifndef _SHAREDCLI_HPP_
#define _SHAREDCLI_HPP_

#include <stdlib.h>
#include <vcclr.h>
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>
#include <boost/preprocessor/stringize.hpp>
#include "pwiz/utility/misc/Exception.hpp"
#include "comdef.h" // for _com_error


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


//#define GC_DEBUG

#ifdef GC_DEBUG
#define LOG_DESTRUCT(msg, willDelete) \
    pwiz::CLI::util::ObjectStructorLog::Log->Append("In " + msg + \
                                                " destructor (will delete: " + \
                                                ((willDelete) ? "yes" : "no") + ").\n");
#define LOG_CONSTRUCT(msg) \
    pwiz::CLI::util::ObjectStructorLog::Log->Append("In " + msg + " constructor.\n");

namespace pwiz { namespace CLI { namespace util {
public ref class ObjectStructorLog
{
    static System::Text::StringBuilder^ log = gcnew System::Text::StringBuilder();

    // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static ObjectStructorLog()
    {
    }

    ObjectStructorLog()
    {
    }

    public:
    static property System::Text::StringBuilder^ Log
    {
        System::Text::StringBuilder^ get() {return log;}
    }
};
} } }

#else // !defined GC_DEBUG
#define LOG_DESTRUCT(msg, willDelete)
#define LOG_CONSTRUCT(msg)
#endif

#define SAFEDELETE(x) if(x) {delete x; x = NULL;}

// catch a C++ or COM exception and rethrow it as a .NET Exception
#define CATCH_AND_FORWARD(x) \
    try { x } \
    /* runtime_error has been rethrown by pwiz so don't prepend the forwarding function */ \
    catch (std::runtime_error& e) { throw gcnew Exception(gcnew String(e.what())); } \
    catch (std::exception& e) { throw gcnew Exception("[" + __FUNCTION__ + "] Unhandled exception: " + gcnew String(e.what())); } \
    catch (_com_error& e) { throw gcnew Exception("[" + __FUNCTION__ + "] Unhandled COM error: " + gcnew String(e.ErrorMessage())); } \
    catch (...) { throw gcnew Exception("[" + __FUNCTION__ + "] Unknown exception"); }

#include "vector.hpp"
#include "map.hpp"
#include "virtual_map.hpp"

#define NATIVE_SHARED_PTR_TO_CLI(SharedPtrType, CLIType, SharedPtr) ((SharedPtr).get() ? gcnew CLIType(new SharedPtrType((SharedPtr))) : nullptr)
#define NATIVE_OWNED_SHARED_PTR_TO_CLI(SharedPtrType, CLIType, SharedPtr, Owner) ((SharedPtr).get() ? gcnew CLIType(new SharedPtrType((SharedPtr)),(Owner)) : nullptr)

#define NATIVE_REFERENCE_TO_CLI(NativeType, CLIType, NativeRef) gcnew CLIType(&(NativeRef), this)
#define NATIVE_VALUE_TO_CLI(NativeType, CLIType, NativeValue) ((CLIType) NativeValue)
#define STD_STRING_TO_CLI_STRING(NativeType, CLIType, StdString) gcnew CLIType((StdString).c_str())

#define CLI_TO_NATIVE_SHARED_PTR(NativeType, CLIObject) (CLIObject == nullptr ? NativeType() : NativeType(*(CLIObject)->base_))
#define CLI_TO_NATIVE_REFERENCE(NativeType, CLIObject) NativeType(*(CLIObject)->base_)
#define CLI_SHARED_PTR_TO_NATIVE_REFERENCE(NativeType, CLIObject) NativeType(**(CLIObject)->base_)
#define CLI_VALUE_TO_NATIVE_VALUE(NativeType, CLIObject) ((NativeType) CLIObject)
#define CLI_STRING_TO_STD_STRING(NativeType, CLIObject) ToStdString(CLIObject)


#define DEFINE_INTERNAL_BASE_CODE(CLIType, NativeType) \
internal: CLIType(NativeType* base, System::Object^ owner) : base_(base), owner_(owner) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CLIType))} \
          CLIType(NativeType* base) : base_(base), owner_(nullptr) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CLIType))} \
          virtual ~CLIType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(CLIType), (owner_ == nullptr)) if (owner_ == nullptr) {SAFEDELETE(base_);}} \
          !CLIType() {delete this;} \
          NativeType* base_; \
          System::Object^ owner_;

#define DEFINE_DERIVED_INTERNAL_BASE_CODE(ns, ClassType, BaseClassType) \
internal: ClassType(ns::ClassType* base, System::Object^ owner) : BaseClassType(base), base_(base) {owner_ = owner; LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          ClassType(ns::ClassType* base) : BaseClassType(base), base_(base) {owner_ = nullptr; LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          virtual ~ClassType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(ClassType), (owner_ == nullptr)) if (owner_ == nullptr) {SAFEDELETE(base_); BaseClassType::base_ = NULL;}} \
          !ClassType() {delete this;} \
          ns::ClassType* base_;

#define DEFINE_SHARED_INTERNAL_BASE_CODE(ns, ClassType) \
internal: ClassType(boost::shared_ptr<ns::ClassType>* base, System::Object^ owner) : base_(base), owner_(owner) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          ClassType(boost::shared_ptr<ns::ClassType>* base) : base_(base), owner_(nullptr) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          virtual ~ClassType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(ClassType), true) SAFEDELETE(base_);} \
          !ClassType() {delete this;} \
          boost::shared_ptr<ns::ClassType>* base_; \
          System::Object^ owner_;

#define DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(ns, ClassType, BaseClassType) \
internal: ClassType(boost::shared_ptr<ns::ClassType>* base) : BaseClassType((ns::BaseClassType*) &**base), base_(base) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          virtual ~ClassType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(ClassType), true) SAFEDELETE(base_); BaseClassType::base_ = NULL;} \
          !ClassType() {delete this;} \
          boost::shared_ptr<ns::ClassType>* base_;

#define DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(ns, ClassType, BaseClassType) \
internal: ClassType(boost::shared_ptr<ns::ClassType>* base) : BaseClassType((boost::shared_ptr<ns::BaseClassType>*) base), base_(base) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          virtual ~ClassType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(ClassType), true) SAFEDELETE(base_); BaseClassType::base_ = NULL;} \
          !ClassType() {delete this;} \
          boost::shared_ptr<ns::ClassType>* base_;

namespace pwiz { namespace CLI { namespace util {
public ref struct tribool
{
    enum class value_type
    {
        tribool_false = 0,
        tribool_true = 1,
        tribool_indeterminate = 2
    };

    public:
    value_type value;

    tribool() : value(value_type::tribool_indeterminate) {}
    tribool(bool value) : value(value ? value_type::tribool_true : value_type::tribool_false) {}
    tribool(value_type value) : value(value) {}

    bool operator==(tribool rhs) {return value == rhs.value;}
    operator bool() {return value == value_type::tribool_true;}
    bool operator!() {return value == value_type::tribool_false;}
};
} } }

#endif // _SHAREDCLI_HPP_
