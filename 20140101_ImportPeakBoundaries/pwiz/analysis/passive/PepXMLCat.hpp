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


#ifndef _PEPXMLCAT_HPP_
#define _PEPXMLCAT_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <ostream>
#include <vector>
#include <utility>

namespace pwiz {
namespace analysis {


class PWIZ_API_DECL PepxmlRecordReader
{
public:

    struct PWIZ_API_DECL Config
    {
        std::string pepxmlfile;
        char   delim;
        char   record;
        char   quote;
        bool   headers;

        Config();
    };
    
    struct PWIZ_API_DECL record
    {
        int    index;
        int    hit_rank;
        std::string spectrum;
        double mz;

        // Tandem
        double hyperscore;
        double nextscore;
        double expect;
	
        
        // SEQUEST
        double xcorr;
        double deltacn;
        double deltacnstar;
        double spscore;
        int    sprank;

        // PHENYX
        double zscore;
        double origScore;

        // MASCOT
        double ionscore;
        double id_score;
        double homology_score;

        // COMET
        double dot_product;
        double delta;
        //double zscore; covered above

        std::vector<std::pair< std::string, std::string> > search_scores;

        // PROBID
        double bays_score;
        // double zscore; covered above
        
        std::string m_ions;
        std::string peptide;
        std::string protein;
        int    ntt;

        record();
    };

    typedef std::vector<record> set;
    typedef std::vector<record>::iterator iterator;
    typedef std::vector<record>::const_iterator const_iterator;

    PepxmlRecordReader(const Config& _config);
    virtual ~PepxmlRecordReader();

    bool open(const std::string& file);
    void close();
    
    void print_headers(std::ostream& os) const;
    
    const_iterator begin() const;
    const_iterator end() const;
    
    iterator begin();
    iterator end();
    
    friend PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, PepxmlRecordReader& prr);
private:
    Config config;
    set recordset;
    std::string search_engine;
};

PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, PepxmlRecordReader& prr);


} // namespace analysis 
} // namespace pwiz 

#endif // _PEPXMLCAT_HPP_


