//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2026 University of Washington
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


#define PWIZ_SOURCE

#include "XmlMetadataParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include <sstream>
#include <cctype>

using namespace pwiz::minimxml;

namespace pwiz {
namespace vendor_api {
namespace Agilent {

namespace {

inline bool is_all_ws(const char* s)
{
    if (!s) return true;
    for (; *s; ++s)
        if (!isspace(static_cast<unsigned char>(*s)))
            return false;
    return true;
}

struct HandlerSampleInfo : public SAXParser::Handler
{
    std::map<std::string, std::string>* mapPtr;
    enum class State
    {
        Other,
        ParsingKey,
        ParsingValue
    };
    State state;
    std::string key;

    HandlerSampleInfo(std::map<std::string, std::string>* m) : mapPtr(m)
    {
        parseCharacters = true;
    }

    virtual Status startElement(const std::string& name, const Attributes& /*attributes*/, boost::iostreams::stream_offset /*position*/)
    {
        if (name == "Name")
            state = State::ParsingKey;
        else if (name == "Value")
            state = State::ParsingValue;
        else
            state = State::Other;
        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text, boost::iostreams::stream_offset /*position*/)
    {
        if (state == State::Other) return Status::Ok;
        if (text.length() == 0) return Status::Ok;
        if (is_all_ws(text.c_str())) return Status::Ok;

        if (state == State::ParsingKey)
        {
            // ensure unique key: append suffix if needed
            string uniqueKey = text.c_str();
            bal::trim(uniqueKey);
            int suffix = 1;
            while (mapPtr->find(uniqueKey) != mapPtr->end())
            {
                ++suffix;
                uniqueKey = key + "_" + std::to_string(suffix);
            }
            key = uniqueKey;
        }
        else if (state == State::ParsingValue)
        {
            string value = text.c_str();
            bal::trim(value);
            (*mapPtr)[key] = value;
        }
        return Status::Ok;
    }

    virtual Status endElement(const std::string& /*name*/, boost::iostreams::stream_offset /*position*/)
    {
        return Status::Ok;
    }
};


struct HandlerDevices : public SAXParser::Handler
{
    vector<Device>* devicesPtr;
    string* currentProperty;

    HandlerDevices(vector<Device>* devices) : devicesPtr(devices), currentProperty(nullptr)
    {
        parseCharacters = true;
    }

    virtual Status startElement(const string& name, const Attributes& attributes, boost::iostreams::stream_offset position)
    {
        if (name == "Device")
        {
            devicesPtr->push_back(Device());
            getAttribute(attributes, "DeviceID", devicesPtr->back().DeviceID);
        }
        else if (name == "Devices" || name == "Version") return Status::Ok;
        else if (name == "Name") currentProperty = &devicesPtr->back().Name;
        else if (name == "DriverVersion") currentProperty = &devicesPtr->back().DriverVersion;
        else if (name == "FirmwareVersion") currentProperty = &devicesPtr->back().FirmwareVersion;
        else if (name == "ModelNumber") currentProperty = &devicesPtr->back().ModelNumber;
        else if (name == "OrdinalNumber") currentProperty = &devicesPtr->back().OrdinalNumber;
        else if (name == "SerialNumber") currentProperty = &devicesPtr->back().SerialNumber;
        else if (name == "Type") currentProperty = &devicesPtr->back().Type;
        else if (name == "StoredDataType") currentProperty = &devicesPtr->back().StoredDataType;
        else if (name == "Delay") currentProperty = &devicesPtr->back().Delay;
        else if (name == "Vendor") currentProperty = &devicesPtr->back().Vendor;
        else
            throw runtime_error(("[HandlerDevices] Unexpected element name: " + name).c_str());

        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text, boost::iostreams::stream_offset position)
    {
        if (currentProperty)
        {
            currentProperty->assign(text.c_str());
            currentProperty = nullptr;
        }

        return Status::Ok;
    }
};

} // namespace (anon)


XmlMetadataParser::XmlMetadataParser(const std::string& acqDataPath,
                                     std::map<std::string, std::string>& sampleInfoMap,
                                     std::vector<Device>& devices)
    : acqDataPath_(acqDataPath), sampleInfoMap_(sampleInfoMap), devices_(devices)
{}

void XmlMetadataParser::parse()
{
    sampleInfoMap_.clear();
    devices_.clear();

    // Parse sample_info.xml if it exists
    bfs::path sampleInfoPath = bfs::path(acqDataPath_) / "sample_info.xml";
    if (bfs::exists(sampleInfoPath))
    {
        ifstream ifs(sampleInfoPath.string().c_str(), std::ios::binary);
        if (ifs)
        {
            HandlerSampleInfo handler(&sampleInfoMap_);
            SAXParser::parse(ifs, handler);
        }
    }

    // Parse Devices.xml if it exists
    bfs::path devicesPath = bfs::path(acqDataPath_) / "Devices.xml";
    if (bfs::exists(devicesPath))
    {
        ifstream ifs(devicesPath.string().c_str(), std::ios::binary);
        if (ifs)
        {
            HandlerDevices handler(&devices_);
            SAXParser::parse(ifs, handler);
        }
    }
}

} // namespace Agilent
} // namespace vendor_api
} // namespace pwiz
