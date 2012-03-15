/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
/**
 * The MzIdentMLReader class definition.  A class to parse the
 * mzIdentML files, particularly those produced by Scaffold.
 */

#include "MzIdentMLReader.h"
#include <iostream>

using namespace std;
using namespace pwiz;
using namespace identdata;

namespace BiblioSpec {

MzIdentMLReader::MzIdentMLReader
    (BlibBuilder& maker,
     const char* mzidFileName,
     const ProgressIndicator* const parent_progress)
    : BuildParser(maker, mzidFileName, parent_progress)
{
    pwizReader_ = new IdentDataFile(mzidFileName);
    list_iter_ = pwizReader_->dataCollection.analysisData.spectrumIdentificationList.begin();
    list_end_ = pwizReader_->dataCollection.analysisData.spectrumIdentificationList.end();

    lookUpBy_ = NAME_ID;
    scoreThreshold_ = getScoreThreshold(SCAFFOLD);
}

MzIdentMLReader::~MzIdentMLReader(){}
    
/**
 * Implementation of BuildParser virtual method.  Reads the .mzid file,
 * stores psms, organized by spectrum file, and imports all spectra.
 */
bool MzIdentMLReader::parseFile(){
    Verbosity::debug("Reading psms from the file.");
    collectPsms();
    
    // for each file
    if( fileMap_.size() > 1 ){
        initSpecFileProgress((int)fileMap_.size());
    }
    
    map<string, vector<PSM*> >::iterator fileIterator = fileMap_.begin();
    for(; fileIterator != fileMap_.end(); ++fileIterator) {
        string fileroot = getFileRoot(fileIterator->first);
        setSpecFileName(fileroot.c_str(), vector<const char*>(1, ".MGF"));

        // move from map to psms_
        psms_ = fileIterator->second;
        buildTables(SCAFFOLD_SOMETHING);
    }

    return true;
}

/**
 *  Read through the whole file to find all PSMs.  Save those that
 *  pass the score threshold and are not decoys.  Nested data
 *  structure is
 *  SpectrumIdentificationList -- lists of spectra, one list per file
 *     SpectrumIdentificationResult -- the spectra in each list
 *         SpectrumIdenficiationItem -- specific peptide match to the spec
 *             PeptideEvidencePtr -- one for each prot in which pep is found
 */
void MzIdentMLReader::collectPsms(){
    // 1 SpectrumIdentificationList = 1 .MGF file
    for(; list_iter_ != list_end_; ++list_iter_){
        
        // 1 SpectrumIdentifiationResult = 1 spectrum
        for(result_iter_ = (**list_iter_).spectrumIdentificationResult.begin(); 
            result_iter_ != (**list_iter_).spectrumIdentificationResult.end();
            ++result_iter_)
        {
            SpectrumIdentificationResult& result = **result_iter_;
            string idStr = result.spectrumID;
            string filename = result.spectraDataPtr->location;
            
            // 1 SpectrumIdentificationItem = 1 psm
            for(item_iter_ = result.spectrumIdentificationItem.begin(); 
                item_iter_ != result.spectrumIdentificationItem.end();
                ++item_iter_)
            {
                SpectrumIdentificationItem& item = **item_iter_;

                // only include top-ranked PSMs, skip decoys
                if( item.rank != 1 || item.peptideEvidencePtr.front()->isDecoy ){ 
                    continue; 
                }

                // skip if it doesn't pass score threshold
                double score = getScore(item);
                if( score < scoreThreshold_ ){
                    continue;
                }

                // now get the psm info
                curPSM_ = new PSM();
                curPSM_->specName = idStr;
                curPSM_->score = score;
                curPSM_->charge = item.chargeState;
                extractModifications(item.peptidePtr->id, curPSM_);
                
                // add the psm to the map
                Verbosity::comment(V_DETAIL, "For file %s adding PSM: "
                                   "scan '%s', charge %d, sequence '%s'.",
                                   filename.c_str(), curPSM_->specName.c_str(),
                                   curPSM_->charge, curPSM_->unmodSeq.c_str());
                map<string, vector<PSM*> >::iterator mapAccess = 
                    fileMap_.find(filename);
                if( mapAccess == fileMap_.end() ){ // not found, add the file
                    vector<PSM*> tmpPsms(1, curPSM_);
                    fileMap_[filename] = tmpPsms;
                } else {  // add this psm to existing file entry
                    (mapAccess->second).push_back(curPSM_);
                }
                curPSM_ = NULL;
            } // next item (PSM)
        } // next result (spectrum)
    } // next list (file)
}


/**
 * Using the modified peptide sequence, with modifications of the form
 * +mass or -mass, set the unmodSeq and mods fields of the psm.
 */
void MzIdentMLReader::extractModifications(string modPepSeq, PSM* psm){

    size_t modPos = modPepSeq.find_first_of("+-");
    while( modPos < modPepSeq.size() ){
        // parse and save the mod
        double mass = atof(modPepSeq.c_str() + modPos);
        psm->mods.push_back(SeqMod(modPos, mass));

        // delete the mod from the sequence
        size_t modEnd = modPepSeq.find_first_of("ACDEFGHIKLMNPQRSTVWY", modPos);
        modPepSeq.erase(modPos, modEnd - modPos);

        // find next mod
        modPos = modPepSeq.find_first_of("+-", modPos + 1);
    }

    psm->unmodSeq = modPepSeq;
}

/**
 * Look through the CVParams of the item and return the score for the
 * peptide probability.
 */
double MzIdentMLReader::getScore(const SpectrumIdentificationItem& item){

    // look through all params to find the probability
    vector<CVParam>::const_iterator it=item.cvParams.begin(); 
    for(; it!=item.cvParams.end(); ++it){
        string name = cvTermInfo((*it).cvid).name;
        if( name == "Scaffold: Peptide Probability" // ": " in file but being
            || name == "Scaffold:Peptide Probability" ){// returned as ":P"
            return boost::lexical_cast<double>(it->value);
        }
    }

    return 0; // shouldn't get to here, warning?
}

} // namespace



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
