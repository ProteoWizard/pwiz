//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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


#ifndef _INDEX_MZML_HPP_
#define _INDEX_MZML_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include <boost/shared_ptr.hpp>
#include <iosfwd>
#include <map>


namespace pwiz {
namespace msdata {

struct SpectrumIdentityFromXML; // forward ref

struct PWIZ_API_DECL Index_mzML
{
    Index_mzML(boost::shared_ptr<std::istream> is, const MSData& msd);

    void recreate();

    size_t spectrumCount() const;
    const SpectrumIdentityFromXML& spectrumIdentity(size_t index) const;
    size_t findSpectrumId(const std::string& id) const;
    IndexList findSpectrumBySpotID(const std::string& spotID) const;
    const std::map<std::string,std::string>& legacyIdRefToNativeId() const;

    size_t chromatogramCount() const;
    const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    size_t findChromatogramId(const std::string& id) const;

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
};


typedef boost::shared_ptr<Index_mzML> Index_mzML_Ptr;


} // namespace msdata
} // namespace pwiz


#endif // _INDEX_MZML_HPP_
