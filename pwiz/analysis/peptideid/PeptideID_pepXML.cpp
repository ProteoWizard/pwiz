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

#define PWIZ_SOURCE

#include <iostream>
#include <fstream>
#include <vector>
#include <stdexcept>
#include <boost/shared_ptr.hpp>

#include "PeptideID_pepXML.hpp"
#include "utility/minimxml/SAXParser.hpp"

namespace pwiz {
namespace peptideid {

using namespace std;
using namespace boost;
using namespace pwiz::minimxml::SAXParser;

typedef map<std::string, PeptideID::Record> record_map;
typedef multimap<double, shared_ptr<PeptideID::Record>, less<double> > rt_multimap;


////////////////////////////////////////////////////////////////////////////
// PeptideID_pepXml::Impl 

class PeptideID_pepXml::Impl
{
public:
    string filename;
    istream* in;
    enum Source {Source_file, Source_stream};
    Source source;
    
    record_map recordMap;
    rt_multimap rtMap;
    
    Impl(const string& filename)
    {
        source = Source_file;
        this->filename = filename;
        in = NULL;
    }
    
    Impl(const char* filename)
    {
        source = Source_file;
        this->filename = filename;
        in = NULL;
    }

    Impl(istream* in)
    {
        source = Source_stream;
        filename.empty();
        this->in = in;
    }
    
    PeptideID::Record record(const string& nativeID)
    {
        record_map::iterator rec = recordMap.find(nativeID);

        if (rec == recordMap.end())
            throw new range_error(nativeID.c_str());

        return (*rec).second;
    }

    rt_multimap::const_iterator record(double retention_time_sec) 
    {
        rt_multimap::const_iterator recs = rtMap.find(retention_time_sec);

        if (recs == rtMap.end())
        {
            ostringstream error;
            error << "No records found for " << retention_time_sec;
            throw new range_error(error.str());
        }

        return recs;
    }
};

////////////////////////////////////////////////////////////////////////////
// class PepXMLHandler

class PepXMLHandler : public pwiz::minimxml::SAXParser::Handler
{
public:
    static const char* spectrum_query_tag;
    static const char* search_hit_tag;
    static const char* peptide_attr;
    static const char* peptideprophet_result_tag;
    static const char* start_scan_attr;
    static const char* retention_time_sec_attr;
    static const char* end_scan_attr;
    static const char* probability_attr;

    PepXMLHandler(record_map* recordMap,
                  rt_multimap* rtMap)
    {
        if (recordMap == NULL)
            throw new exception();
        
        if (rtMap == NULL)
            throw new exception();
        
        this->recordMap = recordMap;
        this->rtMap = rtMap;
    }

    virtual Handler::Status
    startElement(const string& name,
                 const Attributes& attributes,
                 stream_offset position);

    virtual Handler::Status
    endElement(const string& name,
               stream_offset position);

private:
    record_map* recordMap;
    rt_multimap* rtMap;
    string current;
};

// Tags and attributes we look for
const char* PepXMLHandler::spectrum_query_tag = "spectrum_query";
const char* PepXMLHandler::search_hit_tag = "search_hit";
const char* PepXMLHandler::peptide_attr = "peptide";
const char* PepXMLHandler::peptideprophet_result_tag = "peptideprophet_result";
const char* PepXMLHandler::start_scan_attr = "start_scan";
const char* PepXMLHandler::retention_time_sec_attr = "retention_time_sec";
const char* PepXMLHandler::end_scan_attr = "end_scan";
const char* PepXMLHandler::probability_attr = "probability";


Handler::Status PepXMLHandler::startElement(const std::string& name,
                                            const Attributes& attributes,
                                            stream_offset position)
{
    if (name == spectrum_query_tag)
    {
        getAttribute(attributes, start_scan_attr, current);
        (*recordMap)[current].nativeID = current;
        (*recordMap)[current].sequence = "";
        (*recordMap)[current].normalizedScore = 0.;
        getAttribute(attributes, retention_time_sec_attr, (*recordMap)[current].retentionTimeSec);
    }
    else if (name == search_hit_tag)
    {
        getAttribute(attributes, peptide_attr, (*recordMap)[current].sequence);
    }
    else if (name == peptideprophet_result_tag)
    {
        getAttribute(attributes, probability_attr,
                     (*recordMap)[current].normalizedScore);
    }
    
    return Handler::Status::Ok;
}

Handler::Status PepXMLHandler::endElement(const std::string& name,
                                          stream_offset position)
{
    return Handler::Status::Ok;
}


////////////////////////////////////////////////////////////////////////////
// class PeptideID_pepXML

PWIZ_API_DECL PeptideID_pepXml::PeptideID_pepXml(const char* filename)
    : pimpl(new Impl(filename))
{
    ifstream in(filename);

    PepXMLHandler pxh(&(pimpl->recordMap), &(pimpl->rtMap));

    parse(in, pxh);
}

PWIZ_API_DECL PeptideID_pepXml::PeptideID_pepXml(const string& filename)
    : pimpl(new Impl(filename))
{
    ifstream in(filename.c_str());

    PepXMLHandler pxh(&(pimpl->recordMap), &(pimpl->rtMap));
    parse(in, pxh);
}

PWIZ_API_DECL PeptideID_pepXml::PeptideID_pepXml(istream* in)
    : pimpl(new Impl(in))
{
    if (in == NULL)
        throw new exception();

    PepXMLHandler pxh(&(pimpl->recordMap), &(pimpl->rtMap));

    parse(*in, pxh);
}

PWIZ_API_DECL PeptideID::Record PeptideID_pepXml::record(const std::string& nativeID) const
{
    return pimpl->record(nativeID);
}

rt_multimap::const_iterator PeptideID_pepXml::record(double retention_time_sec) const
{
    return pimpl->record(retention_time_sec);
}

} // namespace peptideid
} // namespace pwiz
