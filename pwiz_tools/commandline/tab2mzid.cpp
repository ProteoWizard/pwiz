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


#include "pwiz/data/mziddata/MzIdentMLFile.hpp"
#include "pwiz/data/msdata/CVTranslator.hpp"
#include "pwiz/data/msdata/cv.hpp"
#include "pwiz/Version.hpp"

#include <iostream>
#include <fstream>
#include <iterator>
#include <stdexcept>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>
#include <boost/lexical_cast.hpp>
#include <boost/tokenizer.hpp>

using boost::shared_ptr;
using boost::tokenizer;
using boost::lexical_cast;
using namespace std;
using namespace pwiz::mziddata;
using namespace pwiz;
using namespace pwiz::msdata;
using namespace boost::filesystem;

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

/*
vector<CV> defaultCVList()
{
    vector<CV> result;
    result.resize(3);

    result[0] = cv("MS");
    result[1] = cv("UNIMOD");
    result[2] = cv("UO");
    
    return result;
}
*/
bool loadFile(const string& filename, vector<record>& records)
{
    bool success = true;
    
    ifstream in(filename.c_str());

    if (in.bad())
        success = false;
    else
    {
        string line;
    
        while(getline(in, line))
        {
            record rec;
            boost::char_separator<char> sep("\t");
            tokenizer<boost::char_separator<char> > tok(line, sep);

            tokenizer<boost::char_separator<char> >::const_iterator t=tok.begin();
            
            rec.protein_id = *t++;
            rec.peptide = *t++;
            rec.mz = *t++;
            rec.scan = *t++;
            rec.score = *t++;

            records.push_back(rec);
        }
    }

    return success;
}

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


void sortPeptides(const vector<record>& records,
                  vector< pair<string, string> >& peps)
{
    typedef vector<record>::const_iterator const_record_it;
    typedef vector< pair<string, string> >::const_iterator const_spair_it;
    size_t pep_idx = 0;

    for(const_record_it r=records.begin(); r!=records.end(); r++)
    {
        const_spair_it previous = find_if(peps.begin(), peps.end(),
                                          peptide_predicate(r->peptide));
        if (previous == peps.end())
        {
            peps.push_back(make_pair("peptide_"+lexical_cast<string>(pep_idx++),
                                     r->peptide));
        }
    }
}

void copyPeptides(const vector< pair<string, string> >& peps,
                  MzIdentML& mzid)
{
    typedef vector< pair<string, string> >::const_iterator const_iterator;

    for (const_iterator it=peps.begin(); it!=peps.end(); it++)
    {
        PeptidePtr peptide(new Peptide(it->first));
        peptide->peptideSequence = it->second;
        mzid.sequenceCollection.peptides.push_back(peptide);
    }
}

/// creates a peptide with a protein description, location, and score
/// in the param group
void copyPeptides(const vector<record>& records,
                  const vector< pair<string, string> >& peps,
                  MzIdentML& mzid)
{
    typedef vector<record>::const_iterator const_record_it;

    size_t idx = 0;
    
    for(const_record_it r=records.begin(); r!=records.end(); r++)
    {
        PeptidePtr peptide(new Peptide("peptide_"+lexical_cast<string>(idx++)));
        peptide->peptideSequence = r->peptide;
        peptide->paramGroup.set(MS_protein_description, r->protein_id);
        peptide->paramGroup.set(MS_scan, r->scan);
        peptide->paramGroup.set(MS_m_z, r->mz);
        peptide->paramGroup.set(MS_search_engine_specific_score,
                                r->score);
        mzid.sequenceCollection.peptides.push_back(peptide);

        
    }
}


void copyProteins(const vector<record>& records,
                  const vector< pair<string, string> >& peps,
                  MzIdentML& mzid)
{
    typedef vector<record>::const_iterator const_iterator;
    typedef vector< pair<string, string> >::const_iterator const_spair_it;

    size_t siiIdx = 0;

    CVTranslator translator;

    SpectrumIdentificationListPtr sil(new SpectrumIdentificationList());

    vector< pair<string, SpectrumIdentificationResultPtr> > sir_pairs;
    typedef vector< pair<string, SpectrumIdentificationResultPtr> >::iterator Sir_It;
    for(const_iterator it=records.begin(); it!=records.end(); it++)
    {
        SpectrumIdentificationResultPtr sir(new SpectrumIdentificationResult());

        first_predicate p(it->scan);
        Sir_It sir_it = find_if(sir_pairs.begin(), sir_pairs.end(), p);

        if (sir_it != sir_pairs.end())
            sir = sir_it->second;
        else
        {
            sir->spectrumID = "scan="+it->scan;
            sir_pairs.push_back(make_pair(it->scan, sir));
        }
        
        SpectrumIdentificationItemPtr sii(new SpectrumIdentificationItem());
        sii->id = "SII_"+lexical_cast<string>(siiIdx);
        sii->experimentalMassToCharge = lexical_cast<double>(it->mz);

        peptide_predicate pp(it->peptide);
        const_spair_it ppair = find_if(peps.begin(), peps.end(), pp);

        sii->peptidePtr = PeptidePtr(new Peptide(ppair->first));
        sir->spectrumIdentificationItem.push_back(sii);
        sil->spectrumIdentificationResult.push_back(sir);
            /*
        DBSequencePtr dbs(new DBSequence());
        dbs->id = "dbsequence_"+lexical_cast<string>(idx++);
        dbs->length = it->peptide.size();
        dbs->seq = it->peptide;
        dbs->accession = it->protein_id;
        mzid.sequenceCollection.dbSequences.push_back(dbs);
            */
    }
    mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(sil);
}


void setSourceFile(const string& filename, MzIdentML& mzid)
{
    pwiz::mziddata::SourceFilePtr sourceFile(new pwiz::mziddata::SourceFile());
    sourceFile->location = filename;
    sourceFile->fileFormat.set(MS_tab_delimited_text_file);
    mzid.dataCollection.inputs.sourceFile.push_back(sourceFile);
}


int main(int argc, char* argv[])
{
    string inFile, outFile;
    vector<record> records;
    
    if (argc<3)
    {
        string usage = "usage: ";
        usage += argv[0];
        usage += " <in> <out>";
        throw runtime_error(usage.c_str());
    }

    inFile = argv[1];
    outFile = argv[2];

    bool success =  loadFile(inFile, records);

    if (success)
    {
        // Make a list of id's/peptides
        vector< pair<string, string> > peps;
        sortPeptides(records, peps);
        
        // copy data into mzIdentML
        MzIdentML mzid;
        mzid.cvs = defaultCVList();
        
        copyPeptides(records, peps, mzid);
        //copyProteins(records, peps, mzid);

        // Write the file out
        MzIdentMLFile::write(mzid, outFile);
    }
    
    return 0;
}

