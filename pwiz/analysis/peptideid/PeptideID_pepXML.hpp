//
// PeptideId_pepXML.hpp
//
//
// Original author: Robert Burke <robert.burke@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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

#ifndef _PEPTIDEID_PEPXML_HPP_
#define _PEPTIDEID_PEPXML_HPP_

#include <proteome/auto_vector.h>
#include "minimxml/SAXParser.hpp"

namespace pwiz {
namespace peptideid {

struct BriefFeature
{
    int start_scan;
    int end_scan;
    double probability;
};

class PepXMLHandler : public pwiz::minimxml::SAXParser::Handler
{
public:
    static const char* spectrum_query_tag;
    static const char* start_scan_attr;
    static const char* end_scan_attr;
    static const char* peptideprophet_result_tag;
    static const char* probability_attr;

    virtual pwiz::minimxml::SAXParser::Handler::Status
        startElement(const std::string& name,
                     const Attributes& attributes,
                     stream_offset position);

    virtual pwiz::minimxml::SAXParser::Handler::Status
        endElement(const std::string& name,
                   stream_offset position);


    auto_vector<BriefFeature>& getFeatures()
    {
        return features;
    }
    
private:
    std::auto_ptr<BriefFeature> current;
    auto_vector<BriefFeature> features;

};

} // namespace peptideid
} // namespace pwiz

#endif // _PEPTIDEID_PEPXML_HPP_
