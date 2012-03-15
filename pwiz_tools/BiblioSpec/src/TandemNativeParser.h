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

using namespace std;
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
 
 private:
  enum STATE{ ROOT_STATE, PSM_GROUP_STATE, // highest level group
              DESCRIPTION_STATE, // filename, scan, charge
              DOMAIN_STATE, NESTED_GROUP_STATE, // within psm_group 
              PEAKS_STATE, // fragment ion mass spectrum nested group
              PEAKS_MZ_STATE, // Xdata of peaks_state
              PEAKS_INTENSITY_STATE // Ydata of peaks_state
  };

  void parseGroup(const XML_Char** attr);
  void endGroup();
  void parseDomain(const XML_Char** attr);
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
};

} // namespace


#endif //TANDEMPSMREADER_h
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
