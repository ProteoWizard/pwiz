//
// $Id$
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
        idStrings[i] = ToSystemString(v[i]);
    return idStrings;
}

bool Reader::accept(System::String^ filename, System::String^ head)
{
    try {return base_->accept(ToStdString(filename), ToStdString(head));} CATCH_AND_FORWARD
}

void Reader::read(System::String^ filename, System::String^ head, MSData^ result)
{
    read(filename, head, result, 0);
}

void Reader::read(System::String^ filename, System::String^ head, MSData^ result, int sampleIndex)
{
    try {base_->read(ToStdString(filename), ToStdString(head), **result->base_, sampleIndex);} CATCH_AND_FORWARD
}

void Reader::read(System::String^ filename, System::String^ head, MSDataList^ results)
{
    try {base_->read(ToStdString(filename), ToStdString(head), *results->base_);} CATCH_AND_FORWARD
}

void Reader::read(System::String^ filename, System::String^ head, MSData^ result, ReaderConfig^ config)
{
    read(filename, head, result, 0, config);
}

static void copyReaderConfig(pwiz::msdata::Reader::Config& config, ReaderConfig^ readerConfig)
{
    config.simAsSpectra = readerConfig->simAsSpectra;
    config.srmAsSpectra = readerConfig->srmAsSpectra;
    config.acceptZeroLengthSpectra = readerConfig->acceptZeroLengthSpectra;
    config.ignoreZeroIntensityPoints = readerConfig->ignoreZeroIntensityPoints;
    config.combineIonMobilitySpectra = readerConfig->combineIonMobilitySpectra;
    config.unknownInstrumentIsError = readerConfig->unknownInstrumentIsError;
    config.adjustUnknownTimeZonesToHostTimeZone = readerConfig->adjustUnknownTimeZonesToHostTimeZone;
    config.preferOnlyMsLevel = readerConfig->preferOnlyMsLevel;
}

void Reader::read(System::String^ filename, System::String^ head, MSData^ result, int sampleIndex, ReaderConfig^ readerConfig)
{
    pwiz::msdata::Reader::Config config;
    copyReaderConfig(config,readerConfig);
    try {base_->read(ToStdString(filename), ToStdString(head), **result->base_, sampleIndex, config);} CATCH_AND_FORWARD
}

void Reader::read(System::String^ filename, System::String^ head, MSDataList^ results, ReaderConfig^ readerConfig)
{
    pwiz::msdata::Reader::Config config;
    copyReaderConfig(config,readerConfig);
    try {base_->read(ToStdString(filename), ToStdString(head), *results->base_, config);} CATCH_AND_FORWARD
}

array<System::String^>^ Reader::readIds(System::String^ filename, System::String^ head)
{
    try
    {
        std::vector<std::string> ids;
        base_->readIds(ToStdString(filename), ToStdString(head), ids);
        return vectorToStringArray(ids);
    }
    CATCH_AND_FORWARD
}

System::String^ ReaderList::identify(System::String^ filename)
{    
    try {return ToSystemString(base_->identify(ToStdString(filename)));} CATCH_AND_FORWARD
}

System::String^ ReaderList::identify(System::String^ filename, System::String^ head)
{    
    try {return ToSystemString(base_->identify(ToStdString(filename), ToStdString(head)));} CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, System::String^ head, MSData^ result)
{
    read(filename, head, result, 0);
}

void ReaderList::read(System::String^ filename, System::String^ head, MSData^ result, int sampleIndex)
{    
    try {base_->read(ToStdString(filename), ToStdString(head), **result->base_, sampleIndex);} CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, System::String^ head, MSDataList^ results)
{    
    try {base_->read(ToStdString(filename), ToStdString(head), *results->base_);} CATCH_AND_FORWARD
}

array<System::String^>^ ReaderList::readIds(System::String^ filename, System::String^ head)
{
    try
    {
        std::vector<std::string> ids;
        base_->readIds(ToStdString(filename), ToStdString(head), ids);
        return vectorToStringArray(ids);
    }
    CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, MSData^ result)
{    
    read(filename, result, 0);
}

void ReaderList::read(System::String^ filename, MSData^ result, int sampleIndex)
{    
    try {base_->read(ToStdString(filename), **result->base_, sampleIndex);} CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, MSData^ result, int sampleIndex, ReaderConfig^ readerConfig)
{    
    pwiz::msdata::Reader::Config config;
    copyReaderConfig(config,readerConfig);
    try {base_->read(ToStdString(filename), **result->base_, sampleIndex, config);} CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, MSDataList^ results)
{    
    try {base_->read(ToStdString(filename), *results->base_);} CATCH_AND_FORWARD
}

void ReaderList::read(System::String^ filename, MSDataList^ results, ReaderConfig^ readerConfig)
{    
    pwiz::msdata::Reader::Config config;
    copyReaderConfig(config,readerConfig);
    try {base_->read(ToStdString(filename), *results->base_, config);} CATCH_AND_FORWARD
}

array<System::String^>^ ReaderList::readIds(System::String^ filename)
{
    try
    {
        std::vector<std::string> ids;
        base_->readIds(ToStdString(filename), ids);
        return vectorToStringArray(ids);
    }
    CATCH_AND_FORWARD
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
    try
    {
        pwiz::msdata::FullReaderList* list = new pwiz::msdata::FullReaderList();
        return gcnew ReaderList(list, gcnew System::Object());
    }
    CATCH_AND_FORWARD
}


} // namespace msdata
} // namespace CLI
} // namespace pwiz
