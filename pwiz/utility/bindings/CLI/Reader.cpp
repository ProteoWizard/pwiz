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


#pragma unmanaged
#include <boost/utility/mutexed_singleton.hpp>
#pragma managed


namespace pwiz {
namespace CLI {
namespace msdata {


bool Reader::accept(System::String^ filename, System::String^ head)
{
    return base_->accept(ToStdString(filename), ToStdString(head));
}

void Reader::read(System::String^ filename, System::String^ head, MSData^ result)
{
    base_->read(ToStdString(filename), ToStdString(head), **result->base_);
}

void Reader::read(System::String^ filename, System::String^ head, MSDataList^ results)
{
    base_->read(ToStdString(filename), ToStdString(head), *results->base_);
}


System::String^ ReaderList::identify(System::String^ filename)
{
    return gcnew System::String(base_->identify(ToStdString(filename)).c_str());
}

System::String^ ReaderList::identify(System::String^ filename, System::String^ head)
{
    return gcnew System::String(base_->identify(ToStdString(filename), ToStdString(head)).c_str());
}

void ReaderList::read(System::String^ filename, System::String^ head, MSData^ result)
{
    base_->read(ToStdString(filename), ToStdString(head), **result->base_);
}

void ReaderList::read(System::String^ filename, System::String^ head, MSDataList^ results)
{
    base_->read(ToStdString(filename), ToStdString(head), *results->base_);
}

void ReaderList::read(System::String^ filename, MSData^ result)
{
    base_->read(ToStdString(filename), **result->base_);
}

void ReaderList::read(System::String^ filename, MSDataList^ results)
{
    base_->read(ToStdString(filename), *results->base_);
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
