//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

/**
 * The MzIdentMLReader class definition.  A class to parse the
 * mzIdentML files, particularly those produced by Scaffold.
 */

#include "MzIdentMLReader.h"
#include <iostream>
#include <boost/algorithm/string.hpp>

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

MzIdentMLReader::~MzIdentMLReader()
{
    delete pwizReader_;
}
    
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
    
    map<string, string> mapSourceFiles;
    vector<pwiz::identdata::SourceFilePtr>& sourceFiles = pwizReader_->dataCollection.inputs.sourceFile;
    for(size_t i = 0; i < sourceFiles.size(); i++){
        string location = sourceFiles[i]->location;
        if (!location.empty())
        {
            size_t dot = location.find_last_of('.');
            size_t slash = location.find_last_of("\\/");
            string key = (dot != string::npos && slash != string::npos && slash < dot)
                ? location.substr(slash + 1, dot - slash - 1)
                : location;
            mapSourceFiles[key] = location;
        }
    }

    map<string, vector<PSM*> >::iterator fileIterator = fileMap_.begin();
    for(; fileIterator != fileMap_.end(); ++fileIterator) {
        vector<string> pathParts;
        boost::split(pathParts, fileIterator->first, boost::is_any_of(";"));
        string mgfFileroot = getFileRoot(pathParts[0]);
        setSpecFileName(mgfFileroot.c_str(), vector<const char*>(1, ".MGF"));

        string sourceFile = pathParts[1];
        if (!mapSourceFiles[sourceFile].empty())
            sourceFile = mapSourceFiles[sourceFile];

        // move from map to psms_
        psms_ = fileIterator->second;
        if (sourceFile.empty())
            buildTables(SCAFFOLD_SOMETHING);
        else
            buildTables(SCAFFOLD_SOMETHING, sourceFile);
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
            filename += ";";
            filename += getFilenameFromID(idStr);
            
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
