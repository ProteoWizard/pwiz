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
 * The SslReader parses the scan number, charge, sequence and spectrum
 * file name from the ssl file and stores each record.  Records are
 * sorted and grouped by file.  Spectra are then retrieved from the
 * spectrum files. 
 */

#include "SslReader.h"

namespace BiblioSpec {

SslReader::SslReader(BlibBuilder& maker,
                       const char* sslname,
                       const ProgressIndicator* parent_progress)
    : BuildParser(maker, sslname, parent_progress), sslName_(sslname)
  {
    Verbosity::debug("Creating SslReader.");
    sslDir_ = getPath(sslName_);

    delete specReader_;  // delete base class reader
    specReader_ = this;
  }

  SslReader::~SslReader()
  {
      specReader_ = NULL;  // avoid deleting this
  }

  /**
   * Given data from one line of the ssl file, store it for insertion
   * into the library.  First, translate the modified sequence into an
   * unmodified one and a vector of mods.
   */
  void SslReader::addDataLine(sslPSM& newPSM){
      Verbosity::comment(V_DETAIL, 
                         "Adding new psm (scan %d) from delim file reader.",
                         newPSM.specKey);
      // create a new mod to store
      PSM* curPSM = new PSM(static_cast<PSM &>(newPSM));
      curPSM->modifiedSeq.clear();
      curPSM->mods.clear();

      // parse the modified sequence
      parseModSeq(curPSM->mods, curPSM->unmodSeq);
      unmodifySequence(curPSM->unmodSeq);

      if (!curPSM->IsCompleteEnough())
      {
          std::string err = std::string("Incomplete description: ") + curPSM->idAsString();
          throw BlibException(false, err.c_str());
      }

      // look for this file in the map
      map<string, vector<PSM*> >::iterator mapAccess 
          = fileMap_.find(newPSM.filename);
      if( mapAccess == fileMap_.end()) { // add the file
          vector<PSM*> tmpPsms(1, curPSM);
          fileMap_[newPSM.filename] = tmpPsms;
          fileScoreTypes_[newPSM.filename] = newPSM.scoreType;
      } else {
          (mapAccess->second).push_back(curPSM);
      }

      if (newPSM.retentionTime >= 0)
      {
          int identifier = newPSM.specKey;
          if (newPSM.specIndex != -1) // not default value means scan id is index=<index>
              identifier = newPSM.specIndex;
          else if (newPSM.specKey == -1) // default value
              identifier = std::hash<string>()(newPSM.specName);

          overrideRt_[identifier] = newPSM.retentionTime;
      }
  }

  void SslReader::parse() {
    Verbosity::debug("Parsing File.");

    // create a new DelimitedFileReader, with self as the consumer
    DelimitedFileReader<sslPSM> fileReader(this);

    // add the required columns
    fileReader.addRequiredColumn("file", sslPSM::setFile);
    fileReader.addRequiredColumn("scan", sslPSM::setScanNumber);
    fileReader.addRequiredColumn("charge", sslPSM::setCharge);
    fileReader.addOptionalColumn("sequence", sslPSM::setModifiedSequence); // Formerly required, now optional (but if it's missing, this had better be small molecule data)

    // add the optional columns
    fileReader.addOptionalColumn("score-type", sslPSM::setScoreType);
    fileReader.addOptionalColumn("score", sslPSM::setScore);
    fileReader.addOptionalColumn("retention-time", sslPSM::setRetentionTime);

    // add the optional small molecule columns
    fileReader.addOptionalColumn("inchikey", sslPSM::setInchiKey);
    fileReader.addOptionalColumn("adduct", sslPSM::setPrecursorAdduct);
    fileReader.addOptionalColumn("chemicalformula", sslPSM::setChemicalFormula);
    fileReader.addOptionalColumn("moleculename", sslPSM::setMoleculeName);
    fileReader.addOptionalColumn("otherkeys", sslPSM::setotherKeys);

    // use tab-delimited
    fileReader.defineSeparators('\t');

    // parse, getting each line with addDataLine
    fileReader.parseFile(sslName_.c_str());
  }

  bool SslReader::parseFile(){
    parse();

    // mark progress of each file
    if( fileMap_.size() > 1 ){
      initSpecFileProgress((int)fileMap_.size());
    }

    // for each ms2 file
    map<string, vector<PSM*> >::iterator fileIterator = fileMap_.begin();
    for(; fileIterator != fileMap_.end(); ++fileIterator) {
        string filename = fileIterator->first;

        try { // first try the file name relative to cwd
            setSpecFileName(filename.c_str());
        } catch (BlibException){ // then try it relative to ssl dir
            filename = sslDir_ + filename;
            setSpecFileName(filename.c_str());
        }

      // move from map to psms_
      psms_ = fileIterator->second;

      // look at first psm for scanKey vs scanName
      if (psms_.front()->specIndex != -1) // not default value means scan id is index=<index>
          lookUpBy_ = INDEX_ID;
      else if (psms_.front()->specKey == -1) // default value
          lookUpBy_ = NAME_ID;
      else
          lookUpBy_ = SCAN_NUM_ID;

      buildTables(fileScoreTypes_[fileIterator->first]);
    }

    return true;
  }

  vector<PSM_SCORE_TYPE> SslReader::getScoreTypes() {
    parse();

    set<PSM_SCORE_TYPE> allScoreTypes;
    for (map<string, PSM_SCORE_TYPE>::const_iterator i = fileScoreTypes_.begin(); i != fileScoreTypes_.end(); i++) {
      allScoreTypes.insert(i->second);
    }
    return vector<PSM_SCORE_TYPE>(allScoreTypes.begin(), allScoreTypes.end());
  }
  
  bool SslReader::getSpectrum(int identifier,
                              SpecData& returnData,
                              SPEC_ID_TYPE type,
                              bool getPeaks) {
    if (PwizReader::getSpectrum(identifier, returnData, type, getPeaks))
    {
      map<int, double>::const_iterator i = overrideRt_.find(identifier);
      if (i != overrideRt_.end()) {
        returnData.retentionTime = i->second;
      }
      return true;
    }
    return false;
  }

  bool SslReader::getSpectrum(string identifier,
                              SpecData& returnData,
                              bool getPeaks) {
    if (PwizReader::getSpectrum(identifier, returnData, getPeaks))
    {
        map<int, double>::const_iterator i = overrideRt_.find(std::hash<string>()(identifier));
        if (i != overrideRt_.end()) {
            returnData.retentionTime = i->second;
        }
        return true;
    }
    return false;
  }

  /**
   * Finds modifications of the form [+/-float] and for each for each inserts
   * one SeqMod into the given vector.
   */
  void SslReader::parseModSeq(vector<SeqMod>& mods, 
                              string& modSeq){
    int pos = 0; // SeqMod pos is 1 based
    for (size_t i = 0; i < modSeq.length(); i++) {
      char c = modSeq[i];
      if (c >= 'A' && c <= 'Z') {
        ++pos;
      } else if (c == '[') {
        size_t closePos = modSeq.find(']', ++i);
        if (closePos == string::npos) {
          throw BlibException(false, "Sequence had opening bracket without closing bracket: %s", modSeq.c_str());
        }
        string mass = modSeq.substr(i, closePos - i);
        mods.push_back(SeqMod(max(1, pos), atof(mass.c_str())));

        // Move iterator past mod
        i = closePos;
      }
    }
  }

  /**
   * Remove all modifications from the peptide sequence, in place.
   */
  void SslReader::unmodifySequence(string& seq){

    size_t openBracket = seq.find_first_of("[");
    while( openBracket != string::npos ){
      seq.replace(openBracket, seq.find_first_of("]") - openBracket + 1, "");
      openBracket = seq.find_first_of("[");
    }

    if (!seq.empty() && seq[0] == '-') {
        seq.erase(0, 1);
    }
    if (!seq.empty() && seq[seq.length() - 1] == '-') {
        seq.erase(seq.length() - 1, 1);
    }
  }

} // namespace



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
