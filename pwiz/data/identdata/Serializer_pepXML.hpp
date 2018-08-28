//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SERIALIZER_PEPXML_HPP_
#define _SERIALIZER_PEPXML_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "IdentData.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace identdata {


/// MZIDData <-> pepXML stream serialization
class PWIZ_API_DECL Serializer_pepXML
{
    public:

    /// Serializer_pepXML configuration
    struct PWIZ_API_DECL Config
    {
        bool readSpectrumQueries;

        Config(bool readSpectrumQueries = true) : readSpectrumQueries(readSpectrumQueries) {}
    };

    Serializer_pepXML(const Config& config = Config()) : config_(config) {}

    /// write MZIDData object to ostream as pepXML
    void write(std::ostream& os, const IdentData& mzid, const std::string& filepath,
               const pwiz::util::IterationListenerRegistry* = 0) const;

    /// read in MZIDData object from a pepXML istream
    void read(boost::shared_ptr<std::istream> is, IdentData& mzid,
              const pwiz::util::IterationListenerRegistry* = 0) const;

    private:
    const Config config_;
    Serializer_pepXML(Serializer_pepXML&);
    Serializer_pepXML& operator=(Serializer_pepXML&);
};


struct PWIZ_API_DECL PepXMLSpecificity
{
    std::string cut, no_cut, sense;
};

/// converts an identdata::Enzyme into a pepXML cut/no_cut/sense tuple
PWIZ_API_DECL PepXMLSpecificity pepXMLSpecificity(const Enzyme& ez);


/// strips charge state from known conventions of the pepXML spectrum attribute;
/// used to find a unique identifier for a spectrum in order to merge charge states
PWIZ_API_DECL std::string stripChargeFromConventionalSpectrumId(const std::string& id);


/// converts a software name stored in pepXML software element into its corresponding CVID, or CVID_Unknown if no mapping was found
PWIZ_API_DECL CVID pepXMLSoftwareNameToCVID(const std::string& softwareName);

/// converts a software CVID to the preferred name for that software in pepXML; an unrecognized software name will return an empty string
PWIZ_API_DECL const std::string& softwareCVIDToPepXMLSoftwareName(CVID softwareCVID);


/// for a given software CVID, converts a pepXML score name into its corresponding CVID, or CVID_Unknown if no mapping was found
PWIZ_API_DECL CVID pepXMLScoreNameToCVID(CVID softwareCVID, const std::string& scoreName);

/// for a given software CVID, converts a score CVID into the preferred name for that score in pepXML; an invalid combination of software/score will return an empty string
PWIZ_API_DECL const std::string& scoreCVIDToPepXMLScoreName(CVID softwareCVID, CVID scoreCVID);


/// attempts to convert a period-delimited id into a nativeID format (e.g. "1.0.123" appears to be a Thermo nativeID)
PWIZ_API_DECL CVID nativeIdStringToCVID(const std::string& id);


} // namespace identdata
} // namespace pwiz 

#endif // _SERIALIZER_PEPXML_HPP_
