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
#include <boost/algorithm/string/join.hpp>
#include <boost/lexical_cast.hpp>
#include "pwiz/data/proteome/Peptide.hpp"

namespace BiblioSpec {

SslReader::SslReader(BlibBuilder& maker,
                       const char* sslname,
                       const ProgressIndicator* parent_progress,
                       const char *readerName)
    : BuildParser(maker, sslname, parent_progress), sslName_(sslname)
  {
    Verbosity::debug("Creating %s.", readerName);
    sslDir_ = getPath(sslName_);

    delete specReader_;  // delete base class reader
    specReader_ = this;

    hasHeader_ = true;
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

      if (newPSM.isPrecursorOnly())
      {
          newPSM.setPrecursorOnly(); // Set it again to ensure fully detailed lookup for precursor-only record
      }

      Verbosity::comment(V_DETAIL, 
                         "Adding new psm (scan %s) from delim file reader.",
                         newPSM.idAsString().c_str());
      // create a new mod to store
      sslPSM* curPSM = new sslPSM(newPSM);
      curPSM->modifiedSeq.clear();
      curPSM->mods.clear();

      if (curPSM->unmodSeq.find('@') == string::npos)
      {
          // parse the modified sequence
          parseModSeq(curPSM->mods, curPSM->unmodSeq);
          unmodifySequence(curPSM->unmodSeq);
      }
      else
      {
          curPSM->modifiedSeq = curPSM->unmodSeq;
      	  // parse the crosslinked modified sequence
          curPSM->unmodSeq = parseCrosslinkedSequence(curPSM->mods, curPSM->modifiedSeq);
      }

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
          vector<PSM*>& psms = mapAccess->second;
          psms.push_back(curPSM);
      }

  }

  void SslReader::setColumnsAndSeparators(DelimitedFileReader<sslPSM> &fileReader)
  {
      // add the required columns
      fileReader.addRequiredColumn("file", sslPSM::setFile);
      fileReader.addRequiredColumn("scan", sslPSM::setScanNumber);
      fileReader.addRequiredColumn("charge", sslPSM::setCharge);
      fileReader.addOptionalColumn("sequence", sslPSM::setModifiedSequence); // Formerly required, now optional (but if it's missing, this had better be small molecule data)

      // add the optional columns
      fileReader.addOptionalColumn("score-type", sslPSM::setScoreType);
      fileReader.addOptionalColumn("score", sslPSM::setScore);
      fileReader.addOptionalColumn("retention-time", sslPSM::setRetentionTime);
      fileReader.addOptionalColumn("start-time", sslPSM::setStartTime);
      fileReader.addOptionalColumn("end-time", sslPSM::setEndTime);

      fileReader.addOptionalColumn("ion-mobility", sslPSM::setIonMobility);
      fileReader.addOptionalColumn("ion-mobility-units", sslPSM::setIonMobilityUnits);
      fileReader.addOptionalColumn("ccs", sslPSM::setCCS);

      // add the optional small molecule columns
      fileReader.addOptionalColumn("inchikey", sslPSM::setInchiKey);
      fileReader.addOptionalColumn("adduct", sslPSM::setPrecursorAdduct);
      fileReader.addOptionalColumn("chemicalformula", sslPSM::setChemicalFormula);
      fileReader.addOptionalColumn("moleculename", sslPSM::setMoleculeName);
      fileReader.addOptionalColumn("otherkeys", sslPSM::setotherKeys);
      fileReader.addOptionalColumn("precursorMZ", sslPSM::setPrecursorMzDeclared);

      // use tab-delimited
      fileReader.defineSeparators('\t');

  }

  void SslReader::parse() {
    Verbosity::debug("Parsing File.");

    // create a new DelimitedFileReader, with self as the consumer
    DelimitedFileReader<sslPSM> fileReader(this, hasHeader_);

    setColumnsAndSeparators(fileReader);

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

      // look at first non-precursor-only psm for scanKey vs scanName
      lookUpBy_ = UNKNOWN;
      for (unsigned int i = 0; i < psms_.size(); i++) {
          sslPSM* psm = static_cast<sslPSM*>(psms_.at(i));
          if (psm->specIndex >= 0) // not default value means scan id is index=<index>
          {
              lookUpBy_ = INDEX_ID;
              break;
          }
          else if (psm->specKey <  0) // default value
          {
              lookUpBy_ = NAME_ID;
              break;
          }
          else if (!psm->isPrecursorOnly())
          {
              lookUpBy_ = SCAN_NUM_ID;
              break;
          }
      }
      if (lookUpBy_ == UNKNOWN)
      {
        lookUpBy_ = SCAN_NUM_ID;
      }
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

  bool SslReader::getSpectrum(PSM* psm,
      SPEC_ID_TYPE findBy,
      SpecData& returnData,
      bool getPeaks)
  {
      bool isMS1 = psm->isPrecursorOnly();
      if (isMS1)
      {
          getPeaks = false;
          if (psm->specKey < 0 && findBy == SPEC_ID_TYPE::SCAN_NUM_ID)
          {
              findBy = NAME_ID; // Look up by constructed ID since there's no actual spectrum associated
          }
      }
      bool success = true;
      if (getPeaks)
      {
          switch (findBy) {
          case NAME_ID:
              success = PwizReader::getSpectrum(psm->specName, returnData, getPeaks);
              break;
          case SCAN_NUM_ID:
              success = PwizReader::getSpectrum(psm->specKey, returnData, findBy, getPeaks);
              break;
          case INDEX_ID:
              success = PwizReader::getSpectrum(psm->specIndex, returnData, findBy, getPeaks);
              break;
          }
      }
      return success;
  };

  /**
   * Apply any values carried by subclassed psm(e.g.SSL RT column values) that override those found
   ** by spectrum lookup
   */
  void SslReader::applyPsmOverrideValues(PSM* psm, SpecData& specData)
  {
      auto ssl = dynamic_cast<sslPSM *>(psm);
      if (ssl != nullptr && ssl->rtInfo.retentionTime != 0)
      {
          specData.retentionTime = ssl->rtInfo.retentionTime;
          specData.startTime = ssl->rtInfo.startTime;
          specData.endTime = ssl->rtInfo.endTime;
      }
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
      } else if (c != '[' && c != ']' && c != '.' && !std::isdigit(c)) {
          throw BlibException(false, "Only uppercase letters (amino acids) and bracketed modifications ('[123.4]') are allowed in peptide sequences: %s", modSeq.c_str());
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
   * Parses a crosslinked peptide sequence, adds the modifications from the first crosslinked peptide 
   * to the "mods" vector, and returns the unmodified sequence.
   * The modifications that get added to the mods vector will include a modification representing the
   * mass of the crosslinker plus the masses of all of the linked peptides.
   *
   * An example of a crosslinked peptide sequence is:
   * KC[+57.021464]DDK-EC[+57.021464]PKC[+57.021464]HEK-[+138.06808@1,4]
   * where "138.06808" is the mass of the crosslinker, and the numbers after the "@" symbol are the
   * one-based indexes of the residues that the crosslinker is attached to.
   *
   * There could potentially be multiple crosslinkers specified at the end of the sequence, each inside of its
   * own set of square brackets.
   *
   * If a crosslinker attaches to the same peptide at two locations, those indexes would be separated by hyphen.
   * The usual case would be a loop-link: [+138.06808@1-2]
   * If a particular crosslinker does not attach to a particular peptide, then there would be an asterisk:
   * [+138.06808@*,1-2]
   */
  string SslReader::parseCrosslinkedSequence(vector<SeqMod>& mods, const string& crosslinkedSequence) {
      vector<string> peptideSequences;
      string currentPeptideSequence = "";
      double massOfCrosslinkedPeptides = 0;
      int positionOfFirstCrosslinkerInFirstPeptide = -1;
      for (size_t i = 0; i < crosslinkedSequence.length(); i++) {
          char c = crosslinkedSequence[i];
          if (c >= 'A' && c <= 'Z') {
              currentPeptideSequence += c;
          } else if (c == '-') {
              peptideSequences.push_back(currentPeptideSequence);
              currentPeptideSequence = "";
          } else if (c == '[') {
              size_t closePos = crosslinkedSequence.find(']', ++i);
              if (closePos == string::npos) {
                  throw BlibException(false, "Sequence had opening bracket without closing bracket: %s", crosslinkedSequence.c_str());
              }
              if (currentPeptideSequence.length() == 0) {
                  size_t atSignPos = crosslinkedSequence.find('@', i);
                  if (atSignPos == string::npos) {
                      throw BlibException(false, "Unable to find crosslinker mass in sequence: %s", crosslinkedSequence.c_str());
                  }
                  massOfCrosslinkedPeptides += boost::lexical_cast<double>(crosslinkedSequence.substr(i, atSignPos - i).c_str());
                  size_t numberEnd = crosslinkedSequence.find_first_of("-,]", atSignPos + 1);
                  if (numberEnd == string::npos) {
                      throw BlibException(false, "Unable to interpret crosslink positions in sequence: %s", crosslinkedSequence.c_str());
                  }
                  if (positionOfFirstCrosslinkerInFirstPeptide == -1) {
                      if (crosslinkedSequence.at(atSignPos + 1) != '*') {
                          positionOfFirstCrosslinkerInFirstPeptide = boost::lexical_cast<int>(crosslinkedSequence.substr(atSignPos + 1, numberEnd));
                      }
                  }
              } else {
                  double modificationMass = boost::lexical_cast<double>(crosslinkedSequence.substr(i, closePos - i));
                  if (peptideSequences.size() == 0) {
                      mods.push_back(SeqMod(static_cast<int>(currentPeptideSequence.length()), modificationMass));
                  } else {
                      massOfCrosslinkedPeptides += modificationMass;
                  }
              }
              i = closePos;
          } else {
              throw BlibException(false, "Unexpected character '%c' at position %i in crosslinked peptide: %s", c, i + 1, crosslinkedSequence.c_str());
          }
      }
	  if (positionOfFirstCrosslinkerInFirstPeptide == -1) {
          throw BlibException(false, "Crosslinked peptide is not connected: %s", crosslinkedSequence.c_str());
	  }
      for (size_t iPeptide = 1; iPeptide < peptideSequences.size(); iPeptide++) {
          massOfCrosslinkedPeptides += pwiz::proteome::Peptide(peptideSequences[iPeptide].c_str()).monoisotopicMass();
      }
      mods.push_back(SeqMod(positionOfFirstCrosslinkerInFirstPeptide, massOfCrosslinkedPeptides));
      return boost::algorithm::join(peptideSequences, "-");
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
