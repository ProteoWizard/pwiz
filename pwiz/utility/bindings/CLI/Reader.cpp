//
// Reader.cpp
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "Reader.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "comdef.h" // for _com_error

using System::Exception;
using System::String;

//#pragma unmanaged
//#include <boost/utility/mutexed_singleton.hpp>
//#pragma managed


namespace pwiz {
namespace CLI {
namespace msdata {

static array<System::String^>^ vectorToStringArray(const std::vector<std::string>& v)
{
    array<System::String^>^ idStrings = gcnew array<System::String^>(v.size());
    for (size_t i = 0; i < v.size(); i++)
        idStrings[i] = gcnew System::String(v[i].c_str());
    return idStrings;
}

bool Reader::accept(System::String^ filename, System::String^ head)
{
    return base_->accept(ToStdString(filename), ToStdString(head));
}

void Reader::read(System::String^ filename, System::String^ head, MSData^ result)
{
    read(filename, head, result, 0);
}

void Reader::read(System::String^ filename, System::String^ head, MSData^ result, int sampleIndex)
{
    base_->read(ToStdString(filename), ToStdString(head), **result->base_, sampleIndex);
}

void Reader::read(System::String^ filename, System::String^ head, MSDataList^ results)
{
    base_->read(ToStdString(filename), ToStdString(head), *results->base_);
}

array<System::String^>^ Reader::readIds(System::String^ filename, System::String^ head)
{
    std::vector<std::string> ids;
    base_->readIds(ToStdString(filename), ToStdString(head), ids);
    return vectorToStringArray(ids);
}

System::String^ ReaderList::identify(System::String^ filename)
{    
    try { return gcnew System::String(base_->identify(ToStdString(filename)).c_str()); }
    catch (exception& e) { throw gcnew Exception("[ReaderList::identify()] " + gcnew String(e.what())); }
    catch (_com_error& e) { throw gcnew Exception("[ReaderList::identify()] COM error: " + gcnew String(e.ErrorMessage())); }
    catch (...) { throw gcnew Exception("[ReaderList::identify()] Unhandled exception"); }
}

System::String^ ReaderList::identify(System::String^ filename, System::String^ head)
{    
    try { return gcnew System::String(base_->identify(ToStdString(filename), ToStdString(head)).c_str()); }
    catch (exception& e) { throw gcnew Exception("[ReaderList::identify()] " + gcnew String(e.what())); }
    catch (_com_error& e) { throw gcnew Exception("[ReaderList::identify()] COM error: " + gcnew String(e.ErrorMessage())); }
    catch (...) { throw gcnew Exception("[ReaderList::identify()] Unhandled exception"); }
}

void ReaderList::read(System::String^ filename, System::String^ head, MSData^ result)
{
    read(filename, head, result, 0);
}

void ReaderList::read(System::String^ filename, System::String^ head, MSData^ result, int sampleIndex)
{    
    try { base_->read(ToStdString(filename), ToStdString(head), **result->base_, sampleIndex); }
    catch (exception& e) { throw gcnew Exception("[ReaderList::read()] " + gcnew String(e.what())); }
    catch (_com_error& e) { throw gcnew Exception("[ReaderList::read()] COM error: " + gcnew String(e.ErrorMessage())); }
    catch (...) { throw gcnew Exception("[ReaderList::read()] Unhandled exception"); }
}

void ReaderList::read(System::String^ filename, System::String^ head, MSDataList^ results)
{    
    try { base_->read(ToStdString(filename), ToStdString(head), *results->base_); }
    catch (exception& e) { throw gcnew Exception("[ReaderList::read()] " + gcnew String(e.what())); }
    catch (_com_error& e) { throw gcnew Exception("[ReaderList::read()] COM error: " + gcnew String(e.ErrorMessage())); }
    catch (...) { throw gcnew Exception("[ReaderList::read()] Unhandled exception"); }
}

array<System::String^>^ ReaderList::readIds(System::String^ filename, System::String^ head)
{
    try
    {
        std::vector<std::string> ids;
        base_->readIds(ToStdString(filename), ToStdString(head), ids);
        return vectorToStringArray(ids);
    }
    catch (exception& e) { throw gcnew Exception("[ReaderList::readIds()] " + gcnew String(e.what())); }
    catch (_com_error& e) { throw gcnew Exception("[ReaderList::readIds()] COM error: " + gcnew String(e.ErrorMessage())); }
    catch (...) { throw gcnew Exception("[ReaderList::readIds()] Unhandled exception"); }
}

void ReaderList::read(System::String^ filename, MSData^ result)
{    
    read(filename, result, 0);
}

void ReaderList::read(System::String^ filename, MSData^ result, int sampleIndex)
{    
    try { base_->read(ToStdString(filename), **result->base_, sampleIndex); }
    catch (exception& e) { throw gcnew Exception("[ReaderList::read()] " + gcnew String(e.what())); }
    catch (_com_error& e) { throw gcnew Exception("[ReaderList::read()] COM error: " + gcnew String(e.ErrorMessage())); }
    catch (...) { throw gcnew Exception("[ReaderList::read()] Unhandled exception"); }
}

void ReaderList::read(System::String^ filename, MSDataList^ results)
{    
    try { base_->read(ToStdString(filename), *results->base_); }
    catch (exception& e) { throw gcnew Exception("[ReaderList::read()] " + gcnew String(e.what())); }
    catch (_com_error& e) { throw gcnew Exception("[ReaderList::read()] COM error: " + gcnew String(e.ErrorMessage())); }
    catch (...) { throw gcnew Exception("[ReaderList::read()] Unhandled exception"); }
}

array<System::String^>^ ReaderList::readIds(System::String^ filename)
{
    try
    {
        std::vector<std::string> ids;
        base_->readIds(ToStdString(filename), ids);
        return vectorToStringArray(ids);
    }
    catch (exception& e) { throw gcnew Exception("[ReaderList::readIds()] " + gcnew String(e.what())); }
    catch (_com_error& e) { throw gcnew Exception("[ReaderList::readIds()] COM error: " + gcnew String(e.ErrorMessage())); }
    catch (...) { throw gcnew Exception("[ReaderList::readIds()] Unhandled exception"); }
}

namespace {

/*#pragma unmanaged
struct FullReaderListSingleton : public boost::mutexed_singleton<FullReaderListSingleton>
{
    FullReaderListSingleton(boost::restricted) {}

    pwiz::msdata::FullReaderList list;
};
#pragma managed*/

} // namespace


ReaderList^ ReaderList::FullReaderList::get()
{
    pwiz::msdata::FullReaderList* list = new pwiz::msdata::FullReaderList();
    return gcnew ReaderList(list, gcnew System::Object());
}


} // namespace msdata
} // namespace CLI
} // namespace pwiz
