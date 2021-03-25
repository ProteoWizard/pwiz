//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2020 Matt Chambers
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

#include "SpectrumListBase.hpp"
#include <boost/thread/lock_guard.hpp>
#include <boost/thread/mutex.hpp>


namespace {
    boost::mutex m;
}

PWIZ_API_DECL void pwiz::msdata::SpectrumListBase::warn_once(const char * msg) const
{
    boost::lock_guard<boost::mutex> g(m);
    if (warn_msg_hashes_.insert(hash(msg)).second) // .second is true iff value is new
        std::cerr << msg << std::endl;
}


PWIZ_API_DECL size_t pwiz::msdata::SpectrumListBase::checkNativeIdFindResult(size_t result, const std::string& id) const
{
    if (result < size() || size() == 0)
        return result;

    if (id.empty())
        return size();

    {
        boost::lock_guard<boost::mutex> g(m);

        // early exit if warning already issued, to avoid potentially doing these calculations for thousands of ids
        if (!warn_msg_hashes_.insert(spectrum_id_mismatch_hash_).second)
            return size();
    }

    try
    {
        const auto& firstId = spectrumIdentity(0).id;
        auto actualId = pwiz::msdata::id::parse(firstId);
        auto actualIdKeys = actualId | boost::adaptors::map_keys;
        auto actualIdKeySet = std::set<std::string>(actualIdKeys.begin(), actualIdKeys.end());

        auto expectedId = pwiz::msdata::id::parse(id);
        auto expectedIdKeys = expectedId | boost::adaptors::map_keys;
        auto expectedIdKeySet = std::set<std::string>(expectedIdKeys.begin(), expectedIdKeys.end());

        std::vector<std::string> missingIdKeys;
        std::set_symmetric_difference(expectedIdKeySet.begin(), expectedIdKeySet.end(),
            actualIdKeySet.begin(), actualIdKeySet.end(),
            std::back_inserter(missingIdKeys));

        if (!missingIdKeys.empty())
            warn_once(("[SpectrumList::find] mismatch between spectrum id format of the file (" + firstId + ") and the looked-up id (" + id + ")").c_str());
        return size();
    }
    catch (std::exception& e)
    {
        warn_once((std::string("[SpectrumList::find] error checking for spectrum id conformance: ") + e.what()).c_str()); // TODO: log exception
        return size();
    }
}

size_t pwiz::msdata::SpectrumListBase::hash(const char* msg) const
{
    return boost::hash_range(msg, msg + strlen(msg));
}