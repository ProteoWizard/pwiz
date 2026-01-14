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

#include "SampleInfoParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
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

} // namespace (anon)


SampleInfoParser::SampleInfoParser(const std::string& filepath, std::map<std::string, std::string>& sampleInfoMap)
    : filepath_(filepath), map_(sampleInfoMap)
{}

void SampleInfoParser::parse()
{
    map_.clear();

    ifstream ifs(filepath_.c_str(), std::ios::binary);
    if (!ifs)
        throw std::runtime_error(("SampleInfoParser: cannot open file: " + filepath_).c_str());

    HandlerSampleInfo handler(&map_);
    SAXParser::parse(ifs, handler);
}

} // namespace Agilent
} // namespace vendor_api
} // namespace pwiz
