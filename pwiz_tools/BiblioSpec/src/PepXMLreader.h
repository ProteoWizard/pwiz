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
 * This program sequentially parses the interact.pep.xml file
 *
 *
 *$ PepXMLreader.h,v 1.0 2009/01/13 15:53:52 Ning Zhang Exp $
 */

#ifndef PEPXMLREADER_H
#define PEPXMLREADER_H

#include "BuildParser.h"
#include "AminoAcidMasses.h"

class BlibMaker;

namespace BiblioSpec {

/**
 * \class A class for reading pepXML files, result files from
 * PeptideProphet or SpectrumMill. 
 * Uses the sax handler inherited from BuildParser to read the XML file.
 */
class PepXMLreader : public BuildParser{

 public:
  PepXMLreader(BlibBuilder& maker,
               const char* xmlfilename,
               const ProgressIndicator* parentProgress);
  
  //destructor
  ~PepXMLreader();

  //parser methods
  virtual void startElement(const XML_Char* name, const XML_Char** attr);
  virtual void endElement(const XML_Char* name);
  bool parseFile();
  vector<PSM_SCORE_TYPE> getScoreTypes();
 
 private:
  enum ANALYSIS { UNKNOWN_ANALYSIS, // none of the following
                  PEPTIDE_PROPHET_ANALYSIS,
                  INTER_PROPHET_ANALYSIS,
                  SPECTRUM_MILL_ANALYSIS,
                  OMSSA_ANALYSIS,
                  PROTEIN_PROSPECTOR_ANALYSIS,
                  MORPHEUS_ANALYSIS,
                  MSGF_ANALYSIS,
                  PEAKS_ANALYSIS,
                  PROTEOME_DISCOVERER_ANALYSIS,
                  XTANDEM_ANALYSIS,
                  CRUX_ANALYSIS,
                  COMET_ANALYSIS,
                  MSFRAGGER_ANALYSIS};

  vector<SeqMod> mods;      ///< mods for the current spectrum being parsed
  vector<std::string> dirs;       ///< directories where spec files might be
  vector<std::string> extensions; ///< possible extensions of spec files (.mzXML)
  map<char,map<double,double> > aminoAcidModificationMasses;

  char mzXMLFile[1024];
  
  double probCutOff;  
  double aminoacidmass[128];
  int massType; //1 is mono, 0 is avg
  ANALYSIS analysisType_;  ///< e.g. Peptide Prophet
  ANALYSIS parentAnalysisType_; ///< e.g. MSFragger run through Peptide Prophet
  PSM_SCORE_TYPE scoreType_;
  int lastFilePosition_;
  map<PSM*, double> precursorMap_;
  string fileroot_;
  bool isScoreLookup_;

  //for each spectrum
  
  double pepProb;
  int modPosition;
  double modMass;

  int scanIndex;
  int scanNumber;
  double precursorMZ;
  int charge;
  double ionMobility;
  string spectrumName;
  char pepSeq[200];
  int state;
  int numFiles;

  void setScoreType(PSM_SCORE_TYPE scoreType);
  bool scorePasses(double score);
};

} // namespace

#endif
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
