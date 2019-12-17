//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#include "Reader.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;



Reader::Config::Config()
    : simAsSpectra(false)
    , srmAsSpectra(false)
    , acceptZeroLengthSpectra(false)
    , ignoreZeroIntensityPoints(false)
    , combineIonMobilitySpectra(false)
    , unknownInstrumentIsError(false)
    , adjustUnknownTimeZonesToHostTimeZone(true)
    , iterationListenerRegistry(nullptr)
    , preferOnlyMsLevel(0)
    , allowMsMsWithoutPrecursor(true)
    , sortAndJitter(false)
    , globalChromatogramsAreMs1Only(false)
{
}

/// copy constructor
Reader::Config::Config(const Config& rhs)
{
    simAsSpectra = rhs.simAsSpectra;
    srmAsSpectra = rhs.srmAsSpectra;
	acceptZeroLengthSpectra = rhs.acceptZeroLengthSpectra;
    ignoreZeroIntensityPoints = rhs.ignoreZeroIntensityPoints;
    combineIonMobilitySpectra = rhs.combineIonMobilitySpectra;
    unknownInstrumentIsError = rhs.unknownInstrumentIsError;
    adjustUnknownTimeZonesToHostTimeZone = rhs.adjustUnknownTimeZonesToHostTimeZone;
    iterationListenerRegistry = rhs.iterationListenerRegistry;
    preferOnlyMsLevel = rhs.preferOnlyMsLevel;
    allowMsMsWithoutPrecursor = rhs.allowMsMsWithoutPrecursor;
    isolationMzAndMobilityFilter = rhs.isolationMzAndMobilityFilter;
    sortAndJitter = rhs.sortAndJitter;
    globalChromatogramsAreMs1Only = rhs.globalChromatogramsAreMs1Only;
}

// default implementation; most Readers don't need to worry about multi-run input files
PWIZ_API_DECL void Reader::readIds(const string& filename, const string& head, vector<string>& results, const Config& config) const
{
    MSData data;
    read(filename, head, data);
    results.push_back(data.id);
}


PWIZ_API_DECL std::string ReaderList::identify(const string& filename) const
{
    return identify(filename, read_file_header(filename, 512));
}


PWIZ_API_DECL std::string ReaderList::identify(const string& filename, const string& head) const
{
	std::string result;
    for (const_iterator it=begin(); it!=end(); ++it)
	{
		result = (*it)->identify(filename, head);
        if (result.length())
		{
			break;
		}
	}
    return result;
}


PWIZ_API_DECL void ReaderList::read(const string& filename, MSData& result, int sampleIndex /* = 0 */, const Config& config) const
{
    read(filename, read_file_header(filename, 512), result, sampleIndex, config);
}


PWIZ_API_DECL void ReaderList::read(const string& filename, const string& head, MSData& result, int sampleIndex /* = 0 */, const Config& config) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
        if ((*it)->accept(filename, head))
        {
            (*it)->read(filename, head, result, sampleIndex, config);
            return;
        }
    throw ReaderFail((" don't know how to read " +
                        filename).c_str());
}


PWIZ_API_DECL void ReaderList::read(const string& filename, vector<MSDataPtr>& results, const Config& config) const
{
    read(filename, read_file_header(filename, 512), results, config);
}


PWIZ_API_DECL void ReaderList::read(const string& filename, const string& head, vector<MSDataPtr>& results, const Config& config) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
        if ((*it)->accept(filename, head))
        {
            (*it)->read(filename, head, results, config);
            return;
        }
    throw ReaderFail((" don't know how to read " +
                        filename).c_str());
}


PWIZ_API_DECL void ReaderList::readIds(const string& filename, vector<string>& results, const Config& config) const
{
    readIds(filename, read_file_header(filename, 512), results, config);
}


PWIZ_API_DECL void ReaderList::readIds(const string& filename, const string& head, vector<string>& results, const Config& config) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
        if ((*it)->accept(filename, head))
        {
            (*it)->readIds(filename, head, results, config);
            return;
        }
    throw ReaderFail((" don't know how to read " +
                        filename).c_str());
}


PWIZ_API_DECL std::vector<std::string> ReaderList::getTypes() const
{
    vector<string> result;
    result.reserve(size());
    for (const ReaderPtr& reader : *this)
    {
        result.emplace_back(reader->getType());
    }
    return result;
}

PWIZ_API_DECL std::vector<CVID> ReaderList::getCvTypes() const
{
    vector<CVID> result;
    result.reserve(size());
    for (const ReaderPtr& reader : *this)
    {
        result.emplace_back(reader->getCvType());
    }
    return result;
}

PWIZ_API_DECL std::vector<std::string> ReaderList::getFileExtensions() const
{
    vector<string> result;
    result.reserve(size());
    for (const ReaderPtr& reader : *this)
    {
        auto exts = reader->getFileExtensions();
        result.insert(result.end(), exts.begin(), exts.end());
    }
    return result;
}

PWIZ_API_DECL std::map<std::string, std::vector<std::string>> ReaderList::getFileExtensionsByType() const
{
    map<string, vector<string>> result;
    for (const ReaderPtr& reader : *this)
        result[reader->getType()] = reader->getFileExtensions();
    return result;
}


PWIZ_API_DECL ReaderList& ReaderList::operator +=(const ReaderList& rhs)
{
    insert(end(), rhs.begin(), rhs.end());
    return *this;
}


PWIZ_API_DECL ReaderList& ReaderList::operator +=(const ReaderPtr& rhs)
{
    push_back(rhs);
    return *this;
}


PWIZ_API_DECL ReaderList ReaderList::operator +(const ReaderList& rhs) const
{
    ReaderList readerList(*this);
    readerList += rhs;
    return readerList;
}


PWIZ_API_DECL ReaderList ReaderList::operator +(const ReaderPtr& rhs) const
{
    ReaderList readerList(*this);
    readerList += rhs;
    return readerList;
}


PWIZ_API_DECL ReaderList operator +(const ReaderPtr& lhs, const ReaderPtr& rhs)
{
    ReaderList readerList;
    readerList.push_back(lhs);
    readerList.push_back(rhs);
    return readerList;
}



PWIZ_API_DECL ReaderPtr ReaderList::identifyAsReader(const std::string& filepath) const
{
    try
    {
        string head = read_file_header(filepath, 512);
        string type = identify(filepath, head);
        std::string result;
        for (const auto& readerPtr : *this)
        {
            string result = readerPtr->identify(filepath, head);
            if (result.empty())
                continue;
            if (result == readerPtr->getType())
                return readerPtr;

            // HACK: would be better to have individual format readers identify their files respectively but there would probably be some performance hit for that
            for (const auto& readerPtr2 : *this)
            {
                if (result == readerPtr2->getType())
                    return readerPtr2;
            }
        }
    }
    catch (exception&)
    {
        // the filepath is missing or inaccessible
    }
    return ReaderPtr();
}


} // namespace msdata
} // namespace pwiz

