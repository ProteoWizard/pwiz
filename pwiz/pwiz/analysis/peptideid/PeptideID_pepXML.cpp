//
// $Id$
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
#include "pwiz/utility/minimxml/SAXParser.hpp"


namespace {

using namespace std;
using namespace boost; // TODO: avoid this
using namespace pwiz::peptideid;

ostream& operator<<(ostream& os, const PeptideID::Record& rec)
{
    os << "\tscan: " << rec.nativeID << endl;
    os << "\tsequence: " << rec.sequence << endl;
    os << "\tprotein_descr: " << rec.protein_descr << endl;
    return os;
}

struct local_iterator : public PeptideID::Iterator
{
    local_iterator(map<string, PeptideID::Record>::const_iterator it,
                   map<string, PeptideID::Record>::const_iterator end)
        : it(it), end(end)
    {}
    
    virtual PeptideID::Record next()
    {
        PeptideID::Record record = (*it).second;
        it++;
        
        return record;
    }
    
    virtual bool hasNext()
    {
        map<string, PeptideID::Record>::const_iterator it2=it;
        it2++;
        
        return it2 != end;
    }

    map<string, PeptideID::Record>::const_iterator it;
    map<string, PeptideID::Record>::const_iterator end;
};

}

namespace pwiz {
namespace peptideid {

using namespace std;
using namespace boost; // TODO: avoid this
using namespace pwiz::minimxml::SAXParser;

typedef map<std::string, PeptideID::Record> record_map;
typedef multimap<double, shared_ptr<PeptideID::Record>, less<double> > double_multimap;


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
    double_multimap rtMap;
    
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
    
    PeptideID::Record record(const Location& location)
    {
        record_map::iterator rec = recordMap.find(location.nativeID);

        if (rec == recordMap.end())
            throw new range_error(location.nativeID.c_str());

        return (*rec).second;
    }

    double_multimap::const_iterator record(double retention_time_sec) 
    {
        double_multimap::const_iterator recs = rtMap.find(retention_time_sec);

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
    static const char* protein_descr_attr;
    static const char* peptideprophet_result_tag;
    static const char* start_scan_attr;
    static const char* retention_time_sec_attr;
    static const char* end_scan_attr;
    static const char* probability_attr;

    PepXMLHandler(record_map* recordMap,
                  double_multimap* rtMap)
    {
        if (recordMap == NULL)
            throw new runtime_error("null pointer");
        
        if (rtMap == NULL)
            throw new runtime_error("null pointer");
        
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

    // isntance variables
    record_map* recordMap;
    double_multimap* rtMap;
    string current;
};

// Tags and attributes we look for
const char* PepXMLHandler::spectrum_query_tag = "spectrum_query";
const char* PepXMLHandler::search_hit_tag = "search_hit";
const char* PepXMLHandler::peptide_attr = "peptide";
const char* PepXMLHandler::protein_descr_attr = "protein_descr";
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
        (*recordMap)[current].sequence = "";
        (*recordMap)[current].protein_descr = "";
        getAttribute(attributes, peptide_attr, (*recordMap)[current].sequence);
        getAttribute(attributes, protein_descr_attr, (*recordMap)[current].protein_descr);
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

    if (in.bad())
    {
        ostringstream oss;
        oss << "Unable to open file " << filename;
        throw std::ios_base::failure(oss.str());
    }
    
    PepXMLHandler pxh(&(pimpl->recordMap), &(pimpl->rtMap));

    parse(in, pxh);
}

PWIZ_API_DECL PeptideID_pepXml::PeptideID_pepXml(const string& filename)
    : pimpl(new Impl(filename))
{
    ifstream in(filename.c_str());

    if (in.bad())
    {
        ostringstream oss;
        oss << "Unable to open file " << filename;
        throw std::ios_base::failure(oss.str());
    }
    
    PepXMLHandler pxh(&(pimpl->recordMap), &(pimpl->rtMap));
    parse(in, pxh);
}

PWIZ_API_DECL PeptideID_pepXml::PeptideID_pepXml(istream* in)
    : pimpl(new Impl(in))
{
    if (in == NULL)
        throw runtime_error("null pointer");
    else if (in->bad())
        throw std::ios_base::failure("Unable to open input stream");

    PepXMLHandler pxh(&(pimpl->recordMap), &(pimpl->rtMap));

    parse(*in, pxh);
}

PWIZ_API_DECL PeptideID::Record PeptideID_pepXml::record(const Location& location) const
{
    return pimpl->record(location);
}

double_multimap::const_iterator PeptideID_pepXml::record(double retention_time_sec) const
{
    return pimpl->record(retention_time_sec);
}

PWIZ_API_DECL shared_ptr<PeptideID::Iterator> PeptideID_pepXml::iterator() const
{
    return shared_ptr<PeptideID::Iterator>(new local_iterator(pimpl->recordMap.begin(), pimpl->recordMap.end()));
}

} // namespace peptideid
} // namespace pwiz
