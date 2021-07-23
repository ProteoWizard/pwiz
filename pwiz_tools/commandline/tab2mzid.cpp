//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/data/msdata/CVTranslator.hpp"
#include "pwiz/data/msdata/cv.hpp"
#include "pwiz/Version.hpp"
#include "pwiz/utility/proteome/Peptide.hpp"

#include <iostream>
#include <fstream>
#include <iterator>
#include <stdexcept>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>
#include <boost/lexical_cast.hpp>
#include <boost/tokenizer.hpp>

using boost::tokenizer;
using namespace std;
using namespace pwiz::identdata;
using namespace pwiz;
using namespace pwiz::msdata;
using namespace boost::filesystem;

// String constants
const char* DEFAULT_SEARCH_DATABASE_ID = "SDB_1";

// protein_id peptide mz scan#(nativeid) score
struct record
{
    record(const string& protein_id = "",
           const string& peptide = "",
           const string& mz = "",
           const string& scan = "",
           const string& score = "")
        : protein_id(protein_id), peptide(peptide), mz(mz),
          scan(scan),score(score)
    {}

    string protein_id;
    string peptide;
    string mz;
    string scan;
    string score;

    PeptidePtr peptidePtr;
    SpectrumIdentificationItemPtr sii;
};

struct Config
{
    Config(const string& id = DEFAULT_SEARCH_DATABASE_ID)
        : searchDatabaseId(id),
          searchDatabasePtr(SearchDatabasePtr(
                                new SearchDatabase(searchDatabaseId)))
    {
        searchDatabasePtr->location = "nofile";
        searchDatabasePtr->DatabaseName.userParams.push_back(
            UserParam("unknown"));
    }
    
    string inFile;
    string outFile;
    string searchDatabaseId;

    SearchDatabasePtr searchDatabasePtr;
    vector< pair<string, string> > peptideIdPairs;
    vector<record> records;
};


ostream& operator<<(ostream& os, const record& r)
{
    os << "[record]\n";
    os << "\tprotein_id: " << r.protein_id;
    os << "\n\tpeptide: " << r.peptide;
    os << "\n\tmz: " << r.mz;
    os << "\n\tscan: " << r.scan;
    os << "\n\tscore: " << r.score
       << "\n";

    return os;
}

bool loadFile(Config& config)
{
    bool success = true;
    
    ifstream in(config.inFile.c_str());

    if (in.bad())
        success = false;
    else
    {
        string line;
    
        while(getlinePortable(in, line))
        {
            record rec;
            boost::char_separator<char> sep("\t");
            tokenizer< boost::char_separator<char> > tok(line, sep);

            tokenizer< boost::char_separator<char> >::const_iterator t=tok.begin();
            
            rec.protein_id = *t++;
            rec.peptide = *t++;
            rec.mz = *t++;
            rec.scan = *t++;
            rec.score = *t++;

            config.records.push_back(rec);
        }
    }

    return success;
}

//
// predicates for the find_if functionoid.
//

struct peptide_predicate
{
    string peptide;

    peptide_predicate(const string& peptide = "") : peptide(peptide) {}

    bool operator()(const pair<string, string>& value) const
    {
        return value.second == peptide;
    }
};

struct first_predicate
{
    string key;

    first_predicate(const string& key) : key(key) {}

    bool operator()(const pair<string, SpectrumIdentificationResultPtr>& p)
    {
        return p.first == key;
    }
    
};


/// creates a peptide with a protein description, location, and score
/// in the param group
void copyPeptide(record& rec,
                 pair<string, string>& pep,
                 IdentData& mzid)
{
    PeptidePtr peptide(new Peptide(pep.first));
    peptide->peptideSequence = pep.second;
    rec.peptidePtr = peptide;
    mzid.sequenceCollection.peptides.push_back(peptide);
}


void sortPeptides(Config& config,
                  IdentData& mzid)
{
    typedef vector<record>::iterator record_it;
    typedef vector< pair<string, string> >::const_iterator const_spair_it;
    size_t pep_idx = 0;

    for(record_it r=config.records.begin(); r!=config.records.end(); r++)
    {
        const_spair_it previous = find_if(config.peptideIdPairs.begin(),
                                          config.peptideIdPairs.end(),
                                          peptide_predicate(r->peptide));
        if (previous == config.peptideIdPairs.end())
        {
            config.peptideIdPairs.push_back(
                make_pair("peptide_"+lexical_cast<string>(pep_idx++),
                          r->peptide));
            copyPeptide(*r, config.peptideIdPairs.back (), mzid);
        }
    }
}


void copyProteins(const Config& config,
                  IdentData& mzid)
{
    namespace prot = pwiz::proteome;
    
    typedef vector<record>::const_iterator const_iterator;
    typedef vector< pair<string, string> >::const_iterator const_spair_it;

    size_t siiIdx = 0;
    size_t sirIdx = 0;
    size_t dbsIdx = 0;
    size_t peIdx = 0;

    CVTranslator translator;

    SpectrumIdentificationListPtr sil(new SpectrumIdentificationList());
    sil->id = "SIL_0";

    vector< pair<string, SpectrumIdentificationResultPtr> > sir_pairs;
    typedef vector< pair<string, SpectrumIdentificationResultPtr> >::iterator Sir_It;

    for(const_iterator it=config.records.begin();
        it!=config.records.end(); it++)
    {
        prot::Peptide peptideInfo(it->peptide);
        
        // Adding a DBSequence element
        DBSequencePtr dbs(new DBSequence());
        dbs->id = "dbsequence_"+lexical_cast<string>(dbsIdx++);
        dbs->length = it->peptide.size();
        dbs->seq = it->peptide;
        dbs->paramGroup.set(MS_protein_description, it->protein_id);
        dbs->accession = it->protein_id;
        dbs->searchDatabasePtr = config.searchDatabasePtr;
        mzid.sequenceCollection.dbSequences.push_back(dbs);

        // Create a SpectrumIdentificationResult for each scan
        SpectrumIdentificationResultPtr sir(new SpectrumIdentificationResult());

        first_predicate p(it->scan);
        Sir_It sir_it = find_if(sir_pairs.begin(), sir_pairs.end(), p);

        if (sir_it != sir_pairs.end())
            sir = sir_it->second;
        else
        {
            sir->id = "SIR_"+lexical_cast<string>(sirIdx++);
            sir->spectrumID = "scan="+it->scan;
            sir_pairs.push_back(make_pair(it->scan, sir));
        }

        // Create a SpectrumIdentificationItem for the m/z value.
        SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());
        sii->id = "SII_"+lexical_cast<string>(siiIdx);
        sii->experimentalMassToCharge = lexical_cast<double>(it->mz);
        sii->chargeState = peptideInfo.monoisotopicMass() /
            sii->experimentalMassToCharge;
        sii->calculatedMassToCharge = lexical_cast<double>(it->mz);
        
        sii->paramGroup.set(MS_confidence_score, it->score);
        //sii->paramGroup.set(MS_search_engine_specific_score, it->score);

        sii->peptidePtr = it->peptidePtr;

        PeptideEvidencePtr pe(new PeptideEvidence());
        pe->id = "PE_"+lexical_cast<string>(peIdx++);
        pe->dbSequencePtr = dbs;

        sii->peptideEvidence.push_back(pe);
        
        
        sir->spectrumIdentificationItem.push_back(sii);
        sil->spectrumIdentificationResult.push_back(sir);
    }
    mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(sil);
}


void setInputFiles(const Config& config, IdentData& mzid)
{
    pwiz::identdata::SourceFilePtr sourceFile(new pwiz::identdata::SourceFile());
    sourceFile->location = config.inFile;
    sourceFile->fileFormat.set(MS_tab_delimited_text_file);
    mzid.dataCollection.inputs.sourceFile.push_back(sourceFile);

    mzid.dataCollection.inputs.searchDatabase.push_back(
        config.searchDatabasePtr);
}


int main(int argc, char* argv[])
{
    Config config;
    
    if (argc<3)
    {
        string usage = "usage: ";
        usage += argv[0];
        usage += " <in> <out>";
        throw runtime_error(usage.c_str());
    }

    config.inFile = argv[1];
    config.outFile = argv[2];

    bool success =  loadFile(config);

    if (success)
    {
        IdentData mzid;

        setInputFiles(config, mzid);
        
        // Make a list of id's/peptides
        sortPeptides(config, mzid);
        
        // copy data into mzIdentML
        mzid.cvs = defaultCVList();
        
        //copyPeptides(records, peps, mzid);
        copyProteins(config, mzid);

        // Write the file out
        IdentDataFile::write(mzid, config.outFile);
    }
    
    return 0;
}

