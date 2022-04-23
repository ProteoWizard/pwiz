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

/*
 * This program sequentially parses the native X! Tandem files.
 *
 * This reader collects the PSMs and the TandemSpecReader parses the
 * spectra. Since both are in one file, the result and spec parsing
 * are done together, with each parser storing all the results before
 * building tables.
 */

#ifndef TANDEMPSMREADER_H
#define TANDEMPSMREADER_H

#include "BuildParser.h"
#include "AminoAcidMasses.h"
#include <assert.h>

namespace BiblioSpec {

class BlibMaker;

/**
 * \class A class for reading pepXML files, result files from
 * PeptideProphet. 
 * Uses the sax handler inherited from BuildParser to read the XML file.
 */
class TandemNativeParser : public BuildParser, public SpecFileReader
{

 public:
  TandemNativeParser(BlibBuilder& maker,
                      const char* xmlfilename,
                      const ProgressIndicator* parentProgress);
  
  //destructor
  ~TandemNativeParser();

  //parser methods
  virtual void startElement(const XML_Char* name, const XML_Char** attr);
  virtual void endElement(const XML_Char* name);
  virtual void characters(const XML_Char *s, int len);
  bool parseFile();
  vector<PSM_SCORE_TYPE> getScoreTypes();
 
 private:
  enum STATE{ ROOT_STATE, PSM_GROUP_STATE, // highest level group
              DESCRIPTION_STATE, // filename, scan, charge
              DOMAIN_STATE, NESTED_GROUP_STATE, // within psm_group 
              PEAKS_STATE, // fragment ion mass spectrum nested group
              PEAKS_MZ_STATE, // Xdata of peaks_state
              PEAKS_INTENSITY_STATE, // Ydata of peaks_state
              RESIDUE_MASS_PARAMETERS_STATE
  };

  void parseGroup(const XML_Char** attr);
  void endGroup();
  void parseDomain(const XML_Char** attr);
  void parseSpectraFile(const XML_Char** attr);
  void parseNote(const XML_Char** attr);
  void endNote();
  void endDomain();
  void parseMod(const XML_Char** attr);
  void parsePSM(const XML_Char** attr);
  void parseValues(const XML_Char** attr);
  void getPeaks(istringstream& tokenizer, double* array, int maxSize);
  void getPeaks(istringstream& tokenizer, float* array, int maxSize);
  void stringsToPeaks();
  void newState(STATE nextState);
  STATE getLastState();
  void clearCurPeaks();
  void saveSpectrum();
  void applyResidueMassParameters(PSM* psm);

  // for SpecFileReader interface
  virtual void openFile(const char*, bool mzSort = false);
  virtual void setIdType(SPEC_ID_TYPE type);
  virtual bool getSpectrum(int identifier, SpecData& returnData, 
                           SPEC_ID_TYPE findBy, bool getPeaks = true);
  virtual bool getSpectrum(string identifier, SpecData& returnData, 
                           bool getPeaks = true);
  virtual bool getNextSpectrum(BiblioSpec::SpecData&, bool getPeaks = true);


  // for parsing results
  double probCutOff_;  
  vector<STATE> stateHistory_;  ///< a stack for keeping states
  STATE curState_;              ///< current state (not on the stack)
  double mass_;        // precursor m/z doesn't appear to be stored in file
  int seqStart_;  // mod positions are given relative to the protein
  double retentionTime_;
  string retentionTimeStr_;
  string descriptionStr_; // filename, scan, charge
  string curFilename_; // taken from description
  map<string, vector<PSM*> > fileMap_; // psms stored by spec filename

  // for parsing and storing spectra
  double* mzs_;        // array of m/z values for cur scan
  float* intensities_; // array of intensity values for cur scan
  int numMzs_;         // in the GAML:values element, size of mzs_
  int numIntensities_; // in the GAML:values element, size of intensities_
  string mzStr_;       // collect char representation of peaks here for parsing
  string intensityStr_;
  map<int,SpecData*> spectra_;  // key = specKey

  // residue mass parameters
  double aaMasses_[128];
  map<char, double> aaMods_;
};

} // namespace


#endif //TANDEMPSMREADER_h
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
