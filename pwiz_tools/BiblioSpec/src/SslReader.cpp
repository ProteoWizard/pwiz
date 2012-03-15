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
  }

  SslReader::~SslReader()
  {
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
      PSM* curPSM = new PSM();
      curPSM->charge = newPSM.charge;
      curPSM->unmodSeq = newPSM.unmodSeq;
      curPSM->specKey = newPSM.specKey;
      curPSM->score = newPSM.score;
      curPSM->specName = newPSM.specName;

      // parse the modified sequence
      parseModSeq(curPSM->mods, curPSM->unmodSeq);
      unmodifySequence(curPSM->unmodSeq);


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
 
  }

  bool SslReader::parseFile(){
    Verbosity::debug("Parsing File.");

    // create a new DelimitedFileReader, with self as the consumer
    DelimitedFileReader<sslPSM> fileReader(this);

    // add the required columns
    fileReader.addRequiredColumn("file", sslPSM::setFile);
    fileReader.addRequiredColumn("scan", sslPSM::setScanNumber);
    fileReader.addRequiredColumn("charge", sslPSM::setCharge);
    fileReader.addRequiredColumn("sequence", sslPSM::setModifiedSequence);

    // add the optional columns
    fileReader.addOptionalColumn("score-type", sslPSM::setScoreType);
    fileReader.addOptionalColumn("score", sslPSM::setScore);

    // use tab-delimited
    fileReader.defineSeparators('\t');

    // parse, getting each line with addDataLine
    fileReader.parseFile(sslName_.c_str());

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
      if( psms_.front()->specKey == -1 ){ // default value
        lookUpBy_ = NAME_ID;
      } else {
        lookUpBy_ = SCAN_NUM_ID;
      }

      buildTables(fileScoreTypes_[fileIterator->first]);
    }

    
    return true;
  }



  /**
   * Finds modifications of the form [+/-float] and for each for each inserts
   * one SeqMod into the given vector.
   */
  void SslReader::parseModSeq(vector<SeqMod>& mods, 
                              string& modSeq){
    // find next [
    size_t nonAaChars = 0;// for finding aa position
    size_t openBracket = modSeq.find_first_of("[");
    while( openBracket != string::npos ){
      SeqMod mod;
      // get mass diff
      size_t closeBracket = modSeq.find_first_of("]", openBracket);
      string mass = modSeq.substr(openBracket + 1, 
                                  closeBracket - openBracket -1);
      mod.deltaMass = atof( mass.c_str() );
      // get position
      mod.position = openBracket - nonAaChars;
      nonAaChars += (closeBracket - openBracket + 1);
      // add to mods
      mods.push_back(mod);
      // find next
      openBracket = modSeq.find_first_of("[", openBracket + 1);
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
  }

} // namespace



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
