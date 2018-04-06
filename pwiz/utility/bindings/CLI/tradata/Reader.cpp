//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
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

using System::Exception;
using System::String;

//#pragma unmanaged
//#include <boost/utility/mutexed_singleton.hpp>
//#pragma managed


namespace pwiz {
namespace CLI {
namespace tradata {

static array<System::String^>^ vectorToStringArray(const std::vector<std::string>& v)
{
    array<System::String^>^ idStrings = gcnew array<System::String^>(v.size());
    for (size_t i = 0; i < v.size(); i++)
        idStrings[i] = ToSystemString(v[i]);
    return idStrings;
}

bool Reader::accept(System::String^ filename, System::String^ head)
{
    try {return base_->accept(ToStdString(filename), ToStdString(head));} CATCH_AND_FORWARD
}

void Reader::read(System::String^ filename, System::String^ head, TraData^ result)
{
    read(filename, head, result, 0);
}

void Reader::read(System::String^ filename, System::String^ head, TraData^ result, int sampleIndex)
{
    try {base_->read(ToStdString(filename), ToStdString(head), **result->base_, sampleIndex);} CATCH_AND_FORWARD
}

void Reader::read(System::String^ filename, System::String^ head, TraDataList^ results)
{
    try {base_->read(ToStdString(filename), ToStdString(head), *results->base_);} CATCH_AND_FORWARD
}

System::String^ ReaderList::identify(System::String^ filename)
{    
    try {return ToSystemString(base_->identify(ToStdString(filename)));} CATCH_AND_FORWARD
}

System::String^ ReaderList::identify(System::String^ filename, System::String^ head)
{    
    try {return ToSystemString(base_->identify(ToStdString(filename), ToStdString(head)));} CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, System::String^ head, TraData^ result)
{
    read(filename, head, result, 0);
}

void ReaderList::read(System::String^ filename, System::String^ head, TraData^ result, int sampleIndex)
{    
    try {base_->read(ToStdString(filename), ToStdString(head), **result->base_, sampleIndex);} CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, System::String^ head, TraDataList^ results)
{    
    try {base_->read(ToStdString(filename), ToStdString(head), *results->base_);} CATCH_AND_FORWARD
}


void ReaderList::read(System::String^ filename, TraData^ result)
{    
    read(filename, result, 0);
}

void ReaderList::read(System::String^ filename, TraData^ result, int sampleIndex)
{    
    try {base_->read(ToStdString(filename), **result->base_, sampleIndex);} CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, TraDataList^ results)
{    
    try {base_->read(ToStdString(filename), *results->base_);} CATCH_AND_FORWARD
}

namespace {

/*#pragma unmanaged
struct FullReaderListSingleton : public boost::mutexed_singleton<FullReaderListSingleton>
{
    FullReaderListSingleton(boost::restricted) {}

    pwiz::tradata::FullReaderList list;
};
#pragma managed*/

} // namespace


ReaderList^ ReaderList::DefaultReaderList::get()
{
    try
    {
        pwiz::tradata::DefaultReaderList* list = new pwiz::tradata::DefaultReaderList();
        return gcnew ReaderList(list, gcnew System::Object());
    }
    CATCH_AND_FORWARD
}


} // namespace tradata
} // namespace CLI
} // namespace pwiz