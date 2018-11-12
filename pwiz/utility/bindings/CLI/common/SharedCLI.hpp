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


#ifndef _SHAREDCLI_HPP_
#define _SHAREDCLI_HPP_

#ifndef NOMINMAX
# define NOMINMAX
#endif

#include <stdlib.h>
#include <vcclr.h>
#pragma unmanaged
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>
#include <boost/pointer_cast.hpp>
#include <boost/preprocessor/stringize.hpp>
#include "pwiz/utility/misc/Exception.hpp"
#include "comdef.h" // for _com_error

#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;
#pragma managed

//#define GC_DEBUG

#ifdef GC_DEBUG
#define LOG_DESTRUCT(msg, willDelete) \
    pwiz::CLI::util::ObjectStructorLog::WriteLine("In " + msg + \
                                                  " destructor (will delete: " + \
                                                  ((willDelete) ? "yes" : "no") + ").\n");
#define LOG_FINALIZE(msg) \
    pwiz::CLI::util::ObjectStructorLog::WriteLine("In " + msg + " finalizer.\n");
#define LOG_CONSTRUCT(msg) \
    pwiz::CLI::util::ObjectStructorLog::WriteLine("In " + msg + " constructor.\n");

namespace pwiz { namespace CLI { namespace util {
public ref class ObjectStructorLog
{
    static System::IO::TextWriter^ writer = gcnew System::IO::StringWriter();

    // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static ObjectStructorLog()
    {
    }

    ObjectStructorLog()
    {
    }

    static System::Object^ mutex = gcnew System::Object();

    public:

    static property System::IO::TextWriter^ OutputStream
    {
        void set(System::IO::TextWriter^ value)
        {
            System::Threading::Monitor::Enter(mutex);
            writer = value;
            System::Threading::Monitor::Exit(mutex);
        }
    }

    static void WriteLine(System::String^ line)
    {
        System::Threading::Monitor::Enter(mutex);
        writer->WriteLine(line);
        System::Threading::Monitor::Exit(mutex);
    }
};
} } }

#else // !defined GC_DEBUG
#define LOG_DESTRUCT(msg, willDelete)
#define LOG_FINALIZE(msg)
#define LOG_CONSTRUCT(msg)
#endif

#define SAFEDELETE(x) if(x) {delete x; x = NULL;}

// null deleter to create shared_ptrs that do not delete when reset
namespace { void nullDelete(void* s) {} }

// catch a C++ or COM exception and rethrow it as a .NET Exception
// invalid_argument and runtime_error have probably been rethrown by pwiz so don't prepend the forwarding function
#undef CATCH_AND_FORWARD
#define CATCH_AND_FORWARD \
    catch (std::bad_cast& e) { throw gcnew System::InvalidCastException("[" + __FUNCTION__ + "] " + ToSystemString(e.what())); } \
    catch (std::bad_alloc& e) { throw gcnew System::OutOfMemoryException("[" + __FUNCTION__ + "] " + ToSystemString(e.what())); } \
    catch (std::out_of_range& e) { throw gcnew System::IndexOutOfRangeException("[" + __FUNCTION__ + "] " + ToSystemString(e.what())); } \
    catch (std::invalid_argument& e) { throw gcnew System::ArgumentException(ToSystemString(e.what())); } \
    catch (std::runtime_error& e) { throw gcnew System::Exception(ToSystemString(e.what())); } \
    catch (std::exception& e) { throw gcnew System::Exception("[" + __FUNCTION__ + "] Unhandled exception: " + ToSystemString(e.what())); } \
    catch (_com_error& e) { throw gcnew System::Exception("[" + __FUNCTION__ + "] Unhandled COM error: " + gcnew System::String(e.ErrorMessage())); } \
    catch (...) { throw gcnew System::Exception("[" + __FUNCTION__ + "] Unknown exception"); }

#ifndef INTERNAL
#define INTERNAL internal
#endif

/*#define NativeType* NativeType*
#define x x
#define static_cast<NativeType*>(x) ((NativeType*) (x))*/

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


// void* downcast is needed for cross-assembly calls;
// native types are private by default and
// #pragma make_public doesn't work on templated types (like boost::shared_ptr<T>);
// could use the #pragma on the non-templated types, but for consistency
// the void* downcast is used everywhere

// defines internal members for wrapping a raw pointer to a non-derived native class with a managed wrapper class
#define DEFINE_INTERNAL_BASE_CODE(CLIType, NativeType) \
public:   System::IntPtr void_base() {return (System::IntPtr) base_;} \
INTERNAL: CLIType(NativeType* base, System::Object^ owner) : base_(base), owner_(owner) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CLIType))} \
          CLIType(NativeType* base) : base_(base), owner_(nullptr) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CLIType))} \
          virtual ~CLIType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(CLIType), (owner_ == nullptr)) if (owner_ == nullptr) {SAFEDELETE(base_);}} \
          !CLIType() {LOG_FINALIZE(BOOST_PP_STRINGIZE(CLIType)) delete this;} \
          NativeType* base_; \
          System::Object^ owner_; \
          NativeType& base() {return *base_;}

// defines internal members for wrapping a raw pointer to a derived native class with a managed wrapper class
#define DEFINE_DERIVED_INTERNAL_BASE_CODE(ns, ClassType, BaseClassType) \
public:   System::IntPtr void_base() new {return (System::IntPtr) base_;} \
INTERNAL: ClassType(ns::ClassType* base, System::Object^ owner) : BaseClassType(base), base_(base) {owner_ = owner; LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          ClassType(ns::ClassType* base) : BaseClassType(base), base_(base) {owner_ = nullptr; LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          virtual ~ClassType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(ClassType), (owner_ == nullptr)) if (owner_ == nullptr) {SAFEDELETE(base_); BaseClassType::base_ = NULL;}} \
          !ClassType() {LOG_FINALIZE(BOOST_PP_STRINGIZE(ClassType)) delete this;} \
          ns::ClassType* base_; \
          ns::ClassType& base() new {return *base_;}

// defines internal members for wrapping a shared_ptr to a non-derived native class with a managed wrapper class
#define DEFINE_SHARED_INTERNAL_BASE_CODE(ns, ClassType) \
public:   System::IntPtr void_base() {return (System::IntPtr) base_;} \
INTERNAL: ClassType(boost::shared_ptr<ns::ClassType>* base, System::Object^ owner) : base_(base), owner_(owner) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          ClassType(boost::shared_ptr<ns::ClassType>* base) : base_(base), owner_(nullptr) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          virtual ~ClassType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(ClassType), true) SAFEDELETE(base_);} \
          !ClassType() {LOG_FINALIZE(BOOST_PP_STRINGIZE(ClassType)) delete this;} \
          boost::shared_ptr<ns::ClassType>* base_; \
          System::Object^ owner_; \
          ns::ClassType& base() {return **base_;}

// defines internal members for wrapping a shared_ptr to a derived native class with a managed wrapper class
#define DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(ns, ClassType, BaseClassType) \
public:   System::IntPtr void_base() new {return (System::IntPtr) base_;} \
INTERNAL: ClassType(boost::shared_ptr<ns::ClassType>* base) : BaseClassType(base->get()), base_(base) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          virtual ~ClassType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(ClassType), true) SAFEDELETE(base_); BaseClassType::base_ = NULL;} \
          !ClassType() {LOG_FINALIZE(BOOST_PP_STRINGIZE(ClassType)) delete this;} \
          boost::shared_ptr<ns::ClassType>* base_; \
          ns::ClassType& base() new {return **base_;}

// defines internal members for wrapping a shared_ptr to a derived native class with a managed wrapper class
// TODO: explain why SpectrumList/MSData derivatives need this but ParamContainer derivatives don't
#define DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(ns, ClassType, BaseClassType) \
public:   System::IntPtr void_base() new {return (System::IntPtr) base_;} \
INTERNAL: ClassType(boost::shared_ptr<ns::ClassType>* base) : BaseClassType(new boost::shared_ptr<ns::BaseClassType>(base->get(), nullDelete)), base_(base) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ClassType))} \
          virtual ~ClassType() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(ClassType), true) SAFEDELETE(base_); BaseClassType::base_ = NULL;} \
          !ClassType() {LOG_FINALIZE(BOOST_PP_STRINGIZE(ClassType)) delete this;} \
          boost::shared_ptr<ns::ClassType>* base_; \
          ns::ClassType& base() new {return **base_;}


// wraps a std::string native member variable
#define DEFINE_STRING_PROPERTY(Name) \
property System::String^ Name \
{ \
    System::String^ get() {return ToSystemString(base().Name.c_str());} \
    void set(System::String^ value) {base().Name = ToStdString(value);} \
}

// wraps a native member variable with no indirection (and owned by the class)
#define DEFINE_REFERENCE_PROPERTY(Type, Name) \
property Type^ Name \
{ \
    Type^ get() {return gcnew Type(&base().Name, this);} \
    void set(Type^ value) {base().Name = value->base();} \
}

// wraps a native member variable held by a shared pointer, but still owned by the class
#define DEFINE_OWNED_SHARED_REFERENCE_PROPERTY(SharedPtrType, CLIType, SharedPtrName, CLIName) \
property CLIType^ CLIName \
{ \
    CLIType^ get() {return ((base().SharedPtrName).get() ? gcnew CLIType(new SharedPtrType((base().SharedPtrName)), this) : nullptr);} \
    void set(CLIType^ value) {base().SharedPtrName = *value->base_;} \
}

// wraps a native member variable held by a shared pointer with shared ownership
#define DEFINE_SHARED_REFERENCE_PROPERTY(SharedPtrType, CLIType, SharedPtrName, CLIName) \
property CLIType^ CLIName \
{ \
    CLIType^ get() {return ((base().SharedPtrName).get() ? gcnew CLIType(new SharedPtrType((base().SharedPtrName))) : nullptr);} \
    void set(CLIType^ value) {base().SharedPtrName = *value->base_;} \
}

// wraps a native primitive member variable and casts it to/from a CLI primitive type
#define DEFINE_PRIMITIVE_PROPERTY(NativeType, CLIType, Name) \
property CLIType Name \
{ \
    CLIType get() {return (CLIType) base().Name;} \
    void set(CLIType value) {base().Name = (NativeType) value;} \
}

// wraps a native primitive member variable without casting to a different CLI type
#define DEFINE_SIMPLE_PRIMITIVE_PROPERTY(Type, Name) \
    DEFINE_PRIMITIVE_PROPERTY(Type, Type, Name)

#define IMPLEMENT_STRING_PROPERTY_GET(Class, Property) \
    System::String^ Class::Property::get() {return ToSystemString(base().Property);}
#define IMPLEMENT_STRING_PROPERTY_SET(Class, Property) \
    void Class::Property::set(System::String^ value) {base().Property = ToStdString(value);}

#define IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_GET(Type, Class, Property) \
    Type Class::Property::get() {return base().Property;}
#define IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_SET(Type, Class, Property) \
    void Class::Property::set(Type value) {base().Property = value;}

#define IMPLEMENT_ENUM_PROPERTY_GET(Type, Class, Property) \
    Type Class::Property::get() {return (Type) base().Property;}
#define IMPLEMENT_ENUM_PROPERTY_SET(CLIType, NativeType, Class, Property) \
    void Class::Property::set(CLIType value) {base().Property = (NativeType) value;}

#define IMPLEMENT_REFERENCE_PROPERTY_GET(Type, Class, Property) \
    Type^ Class::Property::get() {return gcnew Type(&base().Property, this);}
#define IMPLEMENT_REFERENCE_PROPERTY_SET(Type, Class, Property) \
    void Class::Property::set(Type^ value) {base().Property = value;}

#endif // _SHAREDCLI_HPP_
