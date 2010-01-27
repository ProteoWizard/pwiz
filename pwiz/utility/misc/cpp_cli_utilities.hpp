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


#include <gcroot.h>
#include <vcclr.h>
#include "automation_vector.h"


namespace pwiz {
namespace util {


inline std::string ToStdString(System::String^ source)
{
    if (System::String::IsNullOrEmpty(source))
        return std::string();

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


inline System::String^ ToSystemString(const std::string& source)
{
    return gcnew System::String(source.c_str());
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


} // namespace util
} // namespace pwiz


/// forwards managed exception to unmanaged code
#define CATCH_AND_FORWARD \
    catch (System::Exception^ e) \
{throw std::runtime_error(pwiz::util::ToStdString(e->Message));}


#endif // _CPP_CLI_UTILITIES_HPP_
