//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#ifndef _CPP_CLI_UTILITIES_HPP_
#define _CPP_CLI_UTILITIES_HPP_

#define WIN32_LEAN_AND_MEAN
#define NOGDI

#ifndef NOMINMAX
# define NOMINMAX
#endif

#include <gcroot.h>
#include <vcclr.h>
#pragma unmanaged
#include <comdef.h> // _com_error
#include <vector>
#include <string>
#include <stdexcept>
#include <boost/algorithm/string/split.hpp>
#include <boost/range/algorithm/copy.hpp>
#include "automation_vector.h"
#pragma managed
#include "BinaryData.hpp"

#ifdef __cplusplus_cli
//#define PWIZ_MANAGED_PASSTHROUGH
#endif

namespace pwiz {
namespace util {


inline std::string ToStdString(System::String^ source)
{
    if (System::String::IsNullOrEmpty(source))
        return std::string();

    System::Text::Encoding^ encoding = System::Text::Encoding::UTF8;
    array<System::Byte>^ encodedBytes = encoding->GetBytes(source);

    std::string target("", encodedBytes->Length);
    char* buffer = &target[0];
    unsigned char* unsignedBuffer = reinterpret_cast<unsigned char*>(buffer);
    System::Runtime::InteropServices::Marshal::Copy(encodedBytes, 0, (System::IntPtr) unsignedBuffer, encodedBytes->Length);
	return target;
}


inline System::String^ ToSystemString(const std::string& source, bool utf8=true)
{
    if (utf8)
    {
        System::Text::Encoding^ encoding = System::Text::Encoding::UTF8;
        int length = source.length();
        array<System::Byte>^ buffer = gcnew array<System::Byte>(length);
        System::Runtime::InteropServices::Marshal::Copy((System::IntPtr) const_cast<char*>(source.c_str()), buffer, 0, length);
        return encoding->GetString(buffer);
    }
    else
        return gcnew System::String(source.c_str());
}


template<typename managed_value_type, typename native_value_type, typename conversion_functor>
inline cli::array<managed_value_type>^ ToSystemArray(const std::vector<native_value_type>& source, conversion_functor f = [](native_value_type i) {return i;})
{
    auto result = gcnew cli::array<managed_value_type>(source.size());
    for (int i = 0; i < (int)source.size(); ++i)
        result[i] = f(source[i]);
    return result;
}


template<typename managed_value_type, typename native_value_type>
void ToStdVector(cli::array<managed_value_type>^ managedArray, std::vector<native_value_type>& stdVector)
{
    stdVector.clear();
    if (managedArray->Length > 0)
    {
        cli::pin_ptr<managed_value_type> pin = &managedArray[0];
        native_value_type* begin = (native_value_type*) pin;
        stdVector.assign(begin, begin + managedArray->Length);
    }
}


template<typename managed_value_type>
void ToStdVector(cli::array<managed_value_type>^ managedArray, std::vector<std::string>& stdVector)
{
    stdVector.clear();
    if (managedArray->Length > 0)
    {
        stdVector.reserve(managedArray->Length);
        for (size_t i = 0, end = managedArray->Length; i < end; ++i)
            stdVector.push_back(ToStdString(managedArray[i]->ToString()));
    }
}


template<typename native_value_type, typename managed_value_type>
std::vector<native_value_type> ToStdVector(cli::array<managed_value_type>^ managedArray)
{
    std::vector<native_value_type> result;
    ToStdVector(managedArray, result);
    return result;
}


template<typename managed_value_type, typename native_value_type>
void ToStdVector(cli::array<managed_value_type>^ managedArray, int sourceIndex, std::vector<native_value_type>& stdVector, int destinationIndex, int count)
{
    stdVector.clear();
    if (managedArray->Length > 0)
    {
        cli::pin_ptr<managed_value_type> pin = &managedArray[sourceIndex];
        native_value_type* begin = (native_value_type*)pin;
        stdVector.assign(begin + destinationIndex, begin + destinationIndex + count);
    }
}


template<typename managed_value_type, typename native_value_type>
void ToStdVector(System::Collections::Generic::IList<managed_value_type>^ managedList, std::vector<native_value_type>& stdVector)
{
    stdVector.clear();
    if (managedList->Count > 0)
    {
        stdVector.reserve(managedList->Count);
        for (size_t i = 0, end = managedList->Count; i < end; ++i)
            stdVector.push_back((native_value_type) managedList[i]);
    }
}


/// wraps a managed array in an automation_vector to enable direct access from unmanaged code
template<typename managed_value_type, typename native_value_type>
void ToAutomationVector(cli::array<managed_value_type>^ managedArray, automation_vector<native_value_type>& automationArray)
{
    VARIANT v;
    ::VariantInit(&v);
    System::IntPtr vPtr = (System::IntPtr) &v;
    System::Runtime::InteropServices::Marshal::GetNativeVariantForObject((System::Object^) managedArray, vPtr);
    automationArray.attach(v);
}


template<typename managed_value_type, typename native_value_type>
void ToBinaryData(cli::array<managed_value_type>^ managedArray, BinaryData<native_value_type>& binaryData)
{
    typedef System::Runtime::InteropServices::GCHandle GCHandle;

#ifdef PWIZ_MANAGED_PASSTHROUGH
    GCHandle handle = GCHandle::Alloc(managedArray);
    binaryData = ((System::IntPtr)handle).ToPointer();
    handle.Free();
#else
    ToBinaryData(managedArray, 0, binaryData, 0, managedArray->Length);
#endif
}


template<typename managed_value_type, typename native_value_type>
void ToBinaryData(cli::array<managed_value_type>^ managedArray, int sourceIndex, BinaryData<native_value_type>& binaryData, int destinationIndex, int count)
{
    binaryData.clear();
    if (managedArray->Length > 0)
    {
        cli::pin_ptr<managed_value_type> pin = &managedArray[sourceIndex];
        native_value_type* begin = (native_value_type*)pin;
        binaryData.assign(begin + destinationIndex, begin + destinationIndex + count);
    }
}


template<typename managed_value_type, typename native_value_type>
void ToBinaryData(System::Collections::Generic::IList<managed_value_type>^ managedList, BinaryData<native_value_type>& binaryData)
{
    binaryData.clear();
    if (managedList->Count > 0)
    {
        binaryData.reserve(managedList->Count);
        for (size_t i = 0, end = managedList->Count; i < end; ++i)
            binaryData.push_back((native_value_type)managedList[i]);
    }
}



ref class Lock
{
    System::Object^ m_pObject;

    public:
    Lock(System::Object^ pObject) : m_pObject(pObject) { System::Threading::Monitor::Enter(m_pObject); }
    ~Lock() { System::Threading::Monitor::Exit(m_pObject); }
};


} // namespace util
} // namespace pwiz


namespace {

/// prepends function with a single level of scope,
/// e.g. "Reader::read()" instead of "pwiz::data::msdata::Reader::read()"
template <typename T>
std::string trimFunctionMacro(const char* function, const T& param)
{
    std::vector<boost::iterator_range<std::string::const_iterator> > tokens;
    std::string functionStr(function);
    boost::algorithm::split(tokens, functionStr, boost::is_any_of(":"), boost::algorithm::token_compress_on);
    std::string what("[");
    if (tokens.size() > 1)
    {
        boost::range::copy(*(tokens.rbegin() + 1), std::back_inserter(what));
        what += "::";
        if (boost::range::equal(*(tokens.rbegin() + 1), *tokens.rbegin()))
            what += "ctor";
        else if (tokens.rbegin()->front() == '~')
            what += "dtor";
        else
            boost::range::copy(*tokens.rbegin(), std::back_inserter(what));
    }
    else if (tokens.size() > 0)
        boost::range::copy(*tokens.rbegin(), std::back_inserter(what));
    what += "(" + lexical_cast<std::string>(param) + ")] ";
    return what;
}

std::string flattenInnerExceptions(System::Exception^ e)
{
    auto what = e->Message;
    while (e->InnerException != nullptr)
    {
        e = e->InnerException;
        auto newWhat = e->Message;
        if (!what->Contains(newWhat))
            what += "\r\n" + newWhat;
    }
    return pwiz::util::ToStdString(what);
}

} // namespace


/// forwards managed exception to unmanaged code;
/// prepends function with a single level of scope,
/// e.g. "Reader::read()" instead of "pwiz::data::msdata::Reader::read()"
#define CATCH_AND_FORWARD_EX(param) \
    catch (std::exception&) {throw;} \
    catch (_com_error& e) {throw std::runtime_error(std::string("COM error: ") + e.ErrorMessage());} \
    /*catch (CException* e) {std::auto_ptr<CException> exceptionDeleter(e); char message[1024]; e->GetErrorMessage(message, 1024); throw std::runtime_error(string("MFC error: ") + message);}*/ \
    catch (System::AggregateException^ e) { throw std::runtime_error(trimFunctionMacro(__FUNCTION__, (param)) + pwiz::util::ToStdString(e->ToString())); } \
    catch (System::Exception^ e) { throw std::runtime_error(trimFunctionMacro(__FUNCTION__, (param)) + flattenInnerExceptions(e)); }

#define CATCH_AND_FORWARD CATCH_AND_FORWARD_EX("")

#endif // _CPP_CLI_UTILITIES_HPP_
