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
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include <boost/thread/lock_guard.hpp>
#include <boost/thread/mutex.hpp>
#include <boost/functional/hash.hpp>


namespace {
    boost::mutex m;
}

PWIZ_API_DECL void pwiz::msdata::ListBase::warn_once(const char * msg) const
{
    boost::lock_guard<boost::mutex> g(m);
    if (warn_msg_hashes_.insert(hash(msg)).second) // .second is true iff value is new
        cerr << msg << std::endl;
}


PWIZ_API_DECL size_t pwiz::msdata::SpectrumListBase::checkNativeIdFindResult(size_t result, const std::string& id) const
{
    if (result < size() || size() == 0)
        return result;

    if (id.empty())
        return size();

    try
    {
        const auto& firstId = spectrumIdentity(0).id;

        bool triedToFindScanByIndex = bal::starts_with(firstId, "scan=") && bal::starts_with(id, "index=");
        bool triedToFindIndexByScan = bal::starts_with(firstId, "index=") && bal::starts_with(id, "scan=");

        // HACK: special behavior if actual ids are scan/index and searched ids are index/scan (respectively)
        if (triedToFindScanByIndex)
            return find("scan=" + pwiz::util::toString(lexical_cast<int>(pwiz::msdata::id::value(id, "index")) + 1));
        else if (triedToFindIndexByScan)
            return find("index=" + pwiz::util::toString(lexical_cast<int>(pwiz::msdata::id::value(id, "scan")) - 1));
        else
        {
            boost::lock_guard<boost::mutex> g(m);

            // early exit if warning already issued, to avoid potentially doing these calculations for thousands of ids
            if (!impl_.warn_msg_hashes().insert(spectrum_id_mismatch_hash_).second)
                return size();
        }

        if (!checkNativeIdMatch(firstId, id))
            warn_once(("[SpectrumList::find] mismatch between spectrum id format of the file (" + firstId + ") and the looked-up id (" + id + ")").c_str());
        return size();
    }
    catch (std::exception& e)
    {
        warn_once((std::string("[SpectrumList::find] error checking for spectrum id conformance: ") + e.what()).c_str()); // TODO: log exception
        return size();
    }
}

size_t pwiz::msdata::ListBase::hash(const char* msg) const
{
    return boost::hash_range(msg, msg + strlen(msg));
}
