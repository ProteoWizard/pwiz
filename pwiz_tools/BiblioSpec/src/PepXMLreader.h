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
 * This program sequentially parses the interact.pep.xml file
 *
 *
 *$ PepXMLreader.h,v 1.0 2009/01/13 15:53:52 Ning Zhang Exp $
 */

#ifndef PEPXMLREADER_H
#define PEPXMLREADER_H

#include "BuildParser.h"
#include "AminoAcidMasses.h"
using namespace std;

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
 
 private:
  enum ANALYSIS { UNKNOWN_ANALYSIS, // none of the following
                  PEPTIDE_PROPHET_ANALYSIS,
                  INTER_PROPHET_ANALYSIS,
                  SPECTRUM_MILL_ANALYSIS,
                  OMSSA_ANALYSIS,
                  PROTEIN_PROSPECTOR_ANALYSIS};

  vector<SeqMod> mods;      ///< mods for the current spectrum being parsed
  vector<const char*> dirs;       ///< directories where spec files might be
  vector<const char*> extensions; ///< possible extensions of spec files (.mzXML)

  char mzXMLFile[1024];
  
  double probCutOff;  
  double aminoacidmass[128];
  int massType; //1 is mono, 0 is avg
  ANALYSIS analysisType_;  ///< e.g. Peptide Prophet
  PSM_SCORE_TYPE scoreType_;
  int lastFilePosition_;

  //for each spectrum
  
  double pepProb;
  int modPosition;
  double modMass;

  int scanNumber;
  double precursorMZ;
  int charge;
  string spectrumName;
  char pepSeq[200];
  int state;
  int numFiles;


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
