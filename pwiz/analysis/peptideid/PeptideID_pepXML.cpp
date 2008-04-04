//
// PeptideID_pepXML.cpp
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

#include "PeptideID_pepXML.hpp"
#include "utility/minimxml/SAXParser.hpp"

namespace pwiz {
namespace peptideid {

using namespace pwiz::minimxml::SAXParser;

const char* PepXMLHandler::spectrum_query_tag = "spectrum_query";
const char* PepXMLHandler::peptideprophet_result_tag = "peptideprophet_result";
const char* PepXMLHandler::start_scan_attr = "start_scan";
const char* PepXMLHandler::end_scan_attr = "end_scan";
const char* PepXMLHandler::probability_attr = "probability";

Handler::Status PepXMLHandler::startElement(const std::string& name,
                             const Attributes& attributes,
                             stream_offset position)
{
    if (name == spectrum_query_tag)
    {
        current = std::auto_ptr<BriefFeature>(new BriefFeature());
        
        getAttribute(attributes, start_scan_attr, current->start_scan);
        getAttribute(attributes, end_scan_attr, current->end_scan);
    }
    else if (current.get() != NULL && name == peptideprophet_result_tag)
    {
        getAttribute(attributes, probability_attr, current->probability);
    }
    
    return Handler::Status::Ok;
}

Handler::Status PepXMLHandler::endElement(const std::string& name,
                           stream_offset position)
{
    if (current.get() != NULL && name == spectrum_query_tag)
    {
        features.push_back(current);
    }
    
    return Handler::Status::Ok;
}

} // namespace peptideid
} // namespace pwiz
