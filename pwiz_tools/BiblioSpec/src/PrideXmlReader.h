//
// $Id$
//
//
// Original author: Kaipo Tamura <kaipot@uw.edu>
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

/*
 * This program sequentially parses PRIDE XML files.
 */

#ifndef PRIDEXMLREADER_H
#define PRIDEXMLREADER_H

#include "BuildParser.h"
#include "pwiz/data/msdata/BinaryDataEncoder.hpp"
#include <assert.h>
#include <algorithm>
#include <cctype>

namespace BiblioSpec {

class BlibMaker;

/**
 * \class A class for reading PRIDE XML files.
 * Uses the sax handler inherited from BuildParser to read the XML file.
 */
class PrideXmlReader : public BuildParser, public SpecFileReader
{

public:
  PrideXmlReader(BlibBuilder& maker,
                 const char* xmlfilename,
                 const ProgressIndicator* parentProgress);
  
  //destructor
  ~PrideXmlReader();

  //parser methods
  virtual void startElement(const XML_Char* name, const XML_Char** attr);
  virtual void endElement(const XML_Char* name);
  virtual void characters(const XML_Char *s, int len);
  bool parseFile();
  vector<PSM_SCORE_TYPE> getScoreTypes();
 
private:
  enum STATE { ROOT_STATE,
               ION_SELECTION_STATE,
               PEAKS_MZ_STATE, PEAKS_MZ_DATA_STATE,
               PEAKS_INTENSITY_STATE, PEAKS_INTENSITY_DATA_STATE,
               PEPTIDE_ITEM_STATE,
               PEPTIDE_SEQUENCE_STATE,
               SPECTRUM_REFERENCE_STATE,
               MOD_LOCATION_STATE,
               MOD_MONO_DELTA_STATE };

  void parseSpectrum(const XML_Char** attr);
  void parseCvParam(const XML_Char** attr);
  void parseData(const XML_Char** attr);
  void endData();
  size_t getDecodedNumBytes(string base64);
  void parsePeptideItem();
  void endPeptideItem();
  void endSequence();
  void endSpectrumReference();
  void parseModificationItem(const XML_Char** attr);
  void endModLocation();
  void endModMonoDelta();
  void prepareCharRead(STATE dataState);
  void newState(STATE nextState);
  void lastState();
  void saveSpectrum();
  void setThreshold(BUILD_INPUT type, bool isMax);

  // for SpecFileReader interface
  virtual void openFile(const char*, bool mzSort = false);
  virtual void setIdType(SPEC_ID_TYPE type);
  virtual bool getSpectrum(int identifier, SpecData& returnData, 
                           SPEC_ID_TYPE findBy, bool getPeaks = true);
  virtual bool getSpectrum(string identifier, SpecData& returnData, 
                           bool getPeaks = true);
  virtual bool getNextSpectrum(BiblioSpec::SpecData&, bool getPeaks = true);


  // for parsing results
  double threshold_;
  bool thresholdIsMax_;
  double foundMz_;
  PSM_SCORE_TYPE scoreType_;
  vector<STATE> stateHistory_;  ///< a stack for keeping states
  STATE curState_;              ///< current state (not on the stack)
  bool isScoreLookup_;

  // for parsing and storing spectra
  SpecData* curSpec_;
  SeqMod curMod_;
  BinaryDataEncoder::Config curBinaryConfig_;
  size_t numMzs_;
  size_t numIntensities_;
  string charBuf_;
  map<int, SpecData*> spectra_;
  map<int, int> spectraChargeStates_;
};

} // namespace


#endif //PRIDEXMLREADER_H
