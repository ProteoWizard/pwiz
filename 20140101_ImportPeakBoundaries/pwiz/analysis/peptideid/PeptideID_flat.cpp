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

#include "pwiz/utility/misc/Std.hpp"
#include <cmath>
#include <boost/tokenizer.hpp>

#include "PeptideID_flat.hpp"

namespace pwiz {
namespace peptideid {

using boost::tokenizer;
using boost::char_separator;
using boost::shared_ptr;
using namespace std;

namespace {

typedef multimap<const std::string, const PeptideID::Record*> nativeID_map;

struct redirect
{
    boost::shared_ptr<FlatRecordBuilder> builder;

    redirect(const redirect& rd)
        : builder(rd.builder)
    {}
    
    redirect(boost::shared_ptr<FlatRecordBuilder> builder)
        : builder(builder)
    {}
    
    bool operator()(const  PeptideID::Record& a, const  PeptideID::Record& b) const
    {
        return (*builder)(a, b);
    }
};

struct local_iterator : public pwiz::peptideid::PeptideID::IteratorInternal
{
public:
    local_iterator(vector<PeptideID::Record>::const_iterator it)
        : it(it)
    {}


    void increment() { it++; }

    bool equal(const boost::shared_ptr<PeptideID::IteratorInternal>& impl) const
    {
        local_iterator* li = dynamic_cast<local_iterator*>(impl.get());
        if (li)
            return it == li->it;

        return false;
    }

    const PeptideID::Record& dereference() const { return *it; }

private:
    vector<PeptideID::Record>::const_iterator it;
};

typedef boost::shared_ptr<local_iterator> plocal_iterator;

}

////////////////////////////////////////////////////////////////////////////
// PeptideID_flat::Impl 

class PeptideID_flat::Impl
{
public:
    string filename;
    istream* in;
    enum Source {Source_file, Source_stream};
    Source source;
    boost::shared_ptr<FlatRecordBuilder> builder;

    vector<PeptideID::Record> records;
    nativeID_map nativeIDMap;
    
    Impl(const string& filename,
         boost::shared_ptr<FlatRecordBuilder> builder)
    {
        source = Source_file;
        this->filename = filename;
        this->builder = builder;
        in = NULL;
    }
    
    Impl(istream* in,
         boost::shared_ptr<FlatRecordBuilder> builder)
    {
        source = Source_stream;
        filename.empty();
        this->in = in;
        this->builder = builder;
    }

    PeptideID::Record record(const Location& location)
    {
        //PeptideID::Record loc;
        //loc.nativeID = location.nativeID;
        //loc.mz = location.mz;
        //loc.retentionTimeSec = location.retentionTimeSec;

        PeptideID::Record result;

        // if there's only one record w/ given nativeID, check and
        // return if match
        nativeID_map::iterator nid_it = nativeIDMap.find(location.nativeID);

        // There should never be a key w/ count 0. But we'll check for
        // complete error checking.
        if (nid_it == nativeIDMap.end() || nativeIDMap.count((*nid_it).first) == 0)
            throw range_error(location.nativeID.c_str());
            
        bool found = false;
        do
        {
            if ((*builder)(*(*nid_it).second, location))
            {
                result = *(*nid_it).second;
                found = true;
            }
            nid_it++;
        }
        while (!found && nid_it != nativeIDMap.upper_bound(location.nativeID));
        
        if (!found)
            throw range_error(location.nativeID.c_str());
        
        return result;
    }

    bool parse()
    {
        bool success = true;
        
        if(source == Source_file)
            in = new ifstream(filename.c_str());
        
        string line;
        bool first=true;
        
        while (getline(*in, line))
        {
            if (first)
            {
                first = false;
                if (builder->header())
                    continue;
            }
            
            vector<string> fields;
            char_separator<char> sep(" \t");
            tokenizer< char_separator<char> > tok(line, sep);
            for (tokenizer<char_separator<char> >::iterator i=tok.begin(); i!=tok.end(); i++)
                fields.push_back(*i);
            
            try
            {
                PeptideID::Record record = builder->build(fields);
                records.push_back(record);
                
                Location loc(record.nativeID, record.mz, record.retentionTimeSec);
            }
            catch(invalid_argument ia)
            {
                success = false;
            }
        }

        redirect rd(builder);
        sort(records.begin(), records.end(), rd);

        // Map the records to the nativeID's
        for (vector<PeptideID::Record>::iterator i=records.begin(); i!=records.end(); i++)
            nativeIDMap.insert(pair<const string, const PeptideID::Record*>((*i).nativeID, &(*i)));
        
        return success;
    }


    size_t size() const
    {
        return records.size();
    }

    Record record(size_t index) const
    {
        return records.at(index);
    }
};

////////////////////////////////////////////////////////////////////////////
// PeptideID_flat

PeptideID_flat::PeptideID_flat(const std::string& filename,
                                boost::shared_ptr<FlatRecordBuilder> builder)
    : pimpl(new Impl(filename, builder))
{
    pimpl->parse();
}

PeptideID_flat::PeptideID_flat(std::istream* in,
                               boost::shared_ptr<FlatRecordBuilder> builder)
    : pimpl(new Impl(in, builder))
{
    pimpl->parse();
}

PeptideID::Record PeptideID_flat::record(const Location& location) const
{
    return pimpl->record(location);
}

PeptideID::Iterator PeptideID_flat::begin() const
{
    return PeptideID::Iterator(plocal_iterator(new local_iterator(pimpl->records.begin())));
}

PeptideID::Iterator PeptideID_flat::end() const
{
    return PeptideID::Iterator(plocal_iterator(new local_iterator(pimpl->records.end())));
}

////////////////////////////////////////////////////////////////////////////
// class FlatRecordBuilder

PeptideID::Record FlatRecordBuilder::build(const vector<string>& fields) const
{
    PeptideID::Record record;

    if (fields.size() == 0)
        throw invalid_argument("zero length fields vector.");
    
    if (fields.size() >= 1)
        record.nativeID = fields.at(0);

    if (fields.size() >= 2)
        record.retentionTimeSec = atof(fields.at(1).c_str());
    
    if (fields.size() >= 3)
        record.mz = atof(fields.at(2).c_str());
    
    if (fields.size() >= 4)
        record.normalizedScore = atof(fields.at(3).c_str());
    
    if (fields.size() >= 5)
        record.sequence = fields.at(4);

    return record;
}

bool  FlatRecordBuilder::header() const
{
    return false;
}

bool FlatRecordBuilder::operator()(const PeptideID::Record& a, const  PeptideID::Record& b) const
{
    bool result = false;

    result = a.nativeID.compare(b.nativeID) < 0;

    return result;
}

bool FlatRecordBuilder::operator()(const PeptideID::Record& a, const  PeptideID::Location& b) const
{
    bool result = false;

    result = a.nativeID.compare(b.nativeID) == 0;

    return result;
}

double FlatRecordBuilder::epsilon() const
{
    return 1e-14;
}

////////////////////////////////////////////////////////////////////////////
// class MSInspectRecordBuilder

PeptideID::Record MSInspectRecordBuilder::build(const vector<string>& fields) const
{
    PeptideID::Record record;

    if (fields.size() == 0)
        throw invalid_argument("zero length fields vector.");
    
    if (fields.size() >= 1)
        record.nativeID = fields.at(0);

    if (fields.size() >= 2)
        record.retentionTimeSec = atof(fields.at(1).c_str());
    
    if (fields.size() >= 3)
        record.mz = atof(fields.at(2).c_str());
    
    if (fields.size() >= 8)
        record.normalizedScore = atof(fields.at(7).c_str());
    
    if (fields.size() >= 15)
        record.sequence = fields.at(14);
    
    return record;
}

bool MSInspectRecordBuilder::header() const
{
    return true;
}

bool  MSInspectRecordBuilder::operator()(const  PeptideID::Record& a, const  PeptideID::Record& b) const
{
    bool result = false;

    result = a.retentionTimeSec < b.retentionTimeSec && a.mz < b.mz;

    return result;
}

bool MSInspectRecordBuilder::operator()(const  PeptideID::Record& a, const  PeptideID::Location& b) const
{
    bool result = false;

    result = fabs(a.retentionTimeSec - b.retentionTimeSec) < epsilon()
                  && fabs(a.mz - b.mz) < epsilon();

    return result;
}

double MSInspectRecordBuilder::epsilon() const
{
    return 1e-14;
}

} // namespace peptideid
} // namespace pwiz
