//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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

#include "PepXMLCat.hpp"

#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"

#include <iostream>
#include <string>
#include <fstream>
#include <sstream>

#include <boost/algorithm/string.hpp>

namespace {

using namespace std;
using namespace pwiz::data::pepxml;
using namespace pwiz::analysis;

using pwiz::minimxml::SAXParser::Handler;
using pwiz::minimxml::SAXParser::parse;

    
struct SearchHitHandler : public Handler
{
    PepxmlRecordReader::set* set;
    SearchHitHandler(PepxmlRecordReader::set* _set = 0)
        : set(_set), reject(false)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!set)
            throw runtime_error("[SearchHitHandler::startElement] NULL set");
        
        if (reject)
            return Status::Ok;

        if (name == "search_hit")
        {
            string hit_rank;
            
            getAttribute(attributes, "hit_rank", hit_rank);
            
            if (hit_rank == "1")
            {
                string peptide_prev_aa, peptide_next_aa, peptide, ntt;
                istringstream iss;
                ostringstream oss;
                
                getAttribute(attributes, "peptide_prev_aa", peptide_prev_aa);
                getAttribute(attributes, "peptide_next_aa", peptide_next_aa);
                getAttribute(attributes, "peptide", peptide);
                getAttribute(attributes, "protein", set->back().protein);
                getAttribute(attributes, "num_tol_term", ntt);

                iss.str(hit_rank);
                iss >> set->back().hit_rank;

                if (peptide_prev_aa.size())
                    oss << peptide_prev_aa << ".";
                oss << peptide;
                if (peptide_next_aa.size())
                    oss << "." << peptide_next_aa;
                set->back().peptide = oss.str();

                iss.str(ntt);
                iss.clear();
                iss >> set->back().ntt;

                return Status::Ok;
            }
            else
            {
                reject = true;
                return Status::Ok;
            }
        }
        else if (name == "search_score")
        {
            istringstream iss;
            string name, value;
            getAttribute(attributes, "name", name);
            getAttribute(attributes, "value", value);
            iss.str(value);

            // SEQUEST
            if (name == "xcorr")
            {
                iss >> set->back().xcorr;
            }
            else if (name == "deltacn")
            {
                iss >> set->back().deltacn;
            }
            else if (name == "deltacnstar")
            {
                iss >> set->back().deltacnstar;
            }
            else if (name == "spscore")
            {
                iss >> set->back().spscore;
            }
            else if (name == "sprank")
            {
                iss >> set->back().sprank;
            }

            // Tandem
            else if (name == "hyperscore")
            {
                iss >> set->back().hyperscore;
            }
            else if (name == "nextscore")
            {
                iss >> set->back().nextscore;
            }
            else if (name == "expect")
            {
                iss >> set->back().expect;
            }
            
            // PHENYX
            else if (name == "zscore")
            {
                iss >> set->back().zscore;
            }
            else if (name == "origScore")
            {
                iss >> set->back().origScore;
            }
            
            // MASCOT
            else if (name == "ionscore")
            {
                iss >> set->back().ionscore;
            }
            else if (name == "id_score")
            {
                iss >> set->back().id_score;
            }
            else if (name == "homology_score")
            {
                iss >> set->back().homology_score;
            }
            
            // COMET
            else if (name == "dot_product")
            {
                iss >> set->back().dot_product;
            }
            else if (name == "delta")
            {
                iss >> set->back().delta;
            }
            else if (name == "zscore")
            {
                iss >> set->back().zscore;
            }
            else if (name == "bays_score")
            {
                iss >> set->back().bays_score;
            }
            else
            {
                set->back().search_scores
                    .push_back(pair<string,string>(name, value));
            }
            
            return Status::Ok;
        }
        
        return Status::Ok;
    }


    virtual Status endElement(const std::string& name,
                              stream_offset position)
    {
        if (name == "search_hit")
        {
            reject = false;
        }
        
        return Status::Ok;
    }

    bool reject;
};

struct PepXMLHandler : public Handler
{
    PepxmlRecordReader::set *set;
    PepXMLHandler(PepxmlRecordReader::set* _set = 0)
        : set(_set)
    {}

    virtual Status startElement(const string& name, 
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (!set)
            throw runtime_error("[PepXMLHandler::startElement] NULL set.");
        
        if (name == "spectrum_query")
        {
            set->push_back(PepxmlRecordReader::record());
            
            string indexStr, precursor_neutral_mass, assumed_charge;
            getAttribute(attributes, "index", indexStr);
            getAttribute(attributes, "precursor_neutral_mass",
                         precursor_neutral_mass);
            getAttribute(attributes, "assumed_charge", assumed_charge);
            getAttribute(attributes, "spectrum", set->back().spectrum);

            double mass, charge;

            istringstream iss(indexStr.c_str());
            iss >> set->back().index;
            
            iss.str(precursor_neutral_mass.c_str());
            iss >> mass;
            
            iss.str(assumed_charge.c_str());
            iss >> charge;

            set->back().mz = mass / charge;
        }
        else if (name == "search_hit")
        {
            handlerSearchHit.set = set;
            return Status(Status::Delegate, &handlerSearchHit);
        } 
        else if (name == "spectrum_query")
        {
            getAttribute(attributes, "search_engine", search_engine);
            cout << "search_engine: " << search_engine << endl;
        }
        
        return Status::Ok;
    }
    
    string search_engine;
    SearchHitHandler handlerSearchHit;
};


bool parsePepXML(const string& pepxmlfile, PepxmlRecordReader::set& set)
{
    ifstream is(pepxmlfile.c_str());

    if (is.good())
    {
        PepXMLHandler handler(&set);
        parse(is, handler);
        return true;
    }

    return false;
    
}

const string header_array[] =
{
    "index",
    "hit_rank",
    "spectrum",
    "mz",
    "xcorr",
    "deltacn",
    "deltacnstar",
    "spscore",
    "sprank",
    "m_ions",
    "peptide",
    "protein",
    "ntt",
    ""
};

}

namespace pwiz {
namespace analysis {

PWIZ_API_DECL PepxmlRecordReader::Config::Config()
    : delim('\t'), record('\n'), quote('"'), headers(true)
{
}

PWIZ_API_DECL PepxmlRecordReader::record::record()
    : index(0), mz(0.), xcorr(0.), deltacn(0.),
      deltacnstar(0.), spscore(0.), sprank(0.),
      ntt(0.)
{
}


PWIZ_API_DECL PepxmlRecordReader::PepxmlRecordReader(const Config& _config)
    : config(_config)
{
    if (!config.pepxmlfile.empty())
        parsePepXML(config.pepxmlfile, recordset);
}

PWIZ_API_DECL PepxmlRecordReader::~PepxmlRecordReader()
{
}

PWIZ_API_DECL bool PepxmlRecordReader::open(const string& file)
{
    config.pepxmlfile = file;
    return parsePepXML(file, recordset);
}

PWIZ_API_DECL void PepxmlRecordReader::close()
{
    recordset.clear();
    search_engine.clear();
}

PWIZ_API_DECL void PepxmlRecordReader::print_headers(std::ostream& os) const
{
    string delim(&config.delim, 1);
    string record(&config.record, 1);
    
    size_t index=0;
    while(!header_array[index].empty())
    {
        os << header_array[index];
        if (!header_array[index+1].empty())
            os << delim;
        index++;
    }
    os << record;
}

PWIZ_API_DECL PepxmlRecordReader::const_iterator PepxmlRecordReader::begin() const
{
    return recordset.begin();
}

PWIZ_API_DECL PepxmlRecordReader::const_iterator PepxmlRecordReader::end() const
{
    return recordset.end();
}

PWIZ_API_DECL PepxmlRecordReader::iterator PepxmlRecordReader::begin()
{
    return recordset.begin();
}

PWIZ_API_DECL PepxmlRecordReader::iterator PepxmlRecordReader::end()
{
    return recordset.end();
}

PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, PepxmlRecordReader& prr)
{
    string delim(&prr.config.delim, 1);
    string record(&prr.config.record, 1);
    
    if (prr.config.headers)
        prr.print_headers(os);
    
    PepxmlRecordReader::const_iterator ci;

    for (ci=prr.begin(); ci!=prr.end(); ci++)
    {
        os << ci->index << delim
           << ci->hit_rank << delim
           << prr.config.quote << ci->spectrum << prr.config.quote << delim
           << ci->mz << delim;

        // SEQUEST
        if (boost::iequals(prr.search_engine, "SEQUEST"))
            os << ci->xcorr << delim
               << ci->deltacn << delim
               << ci->deltacnstar << delim
               << ci->spscore << delim
               << ci->sprank << delim;
            
        // Tandem
        if (boost::iequals(prr.search_engine, "Tandem"))
            os << ci->hyperscore << delim
               << ci->nextscore << delim
               << ci->expect << delim;
        
        // PHENYX
        if (boost::iequals(prr.search_engine, "PHENYX"))
            os << ci->zscore << delim
               << ci->origScore << delim;

        // MASCOT
        if (boost::iequals(prr.search_engine, "MASCOT"))
            os << ci->ionscore << delim
               << ci->id_score << delim
               << ci->homology_score << delim;

        // COMET
        if (boost::iequals(prr.search_engine, "COMET"))
            os << ci->dot_product << delim
               << ci->delta << delim
               << ci->zscore << delim;

        // PROBID
        if (boost::iequals(prr.search_engine, "PROBID"))
            os << ci->bays_score << delim
               << ci->zscore << delim;

        os << prr.config.quote << ci->m_ions << prr.config.quote << delim
           << prr.config.quote << ci->peptide << prr.config.quote << delim
           << prr.config.quote << ci->protein << prr.config.quote << delim
           << ci->ntt << record;
    }
    
    return os;
}

} // namespace analysis 
} // namespace pwiz 
