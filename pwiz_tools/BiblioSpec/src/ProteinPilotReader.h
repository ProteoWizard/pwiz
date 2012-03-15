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
#pragma once

/**
 * Header file for the ProteinPilotReader class.  A class to parse
 * .group.xml files, the output of ABI's group2xml tool, part of
 * Protein Pilot.
 */

#include <sstream>
#include <algorithm>
#include "BuildParser.h"
#include "PeakProcess.h"

namespace BiblioSpec {

    /**
     * Class for reading .group.xml files from Protein Pilot's group2xml
     * tool.
     */
    class ProteinPilotReader : public BuildParser, public SpecFileReader
    {
        
    public:
        ProteinPilotReader(BlibBuilder& maker,
                           const char* xmlFileName,
                           const ProgressIndicator* parentProgress);
        ~ProteinPilotReader();
        
        // BuildParser functions
        virtual bool parseFile();
        virtual void startElement(const XML_Char* name, const XML_Char** attr);
        virtual void endElement(const XML_Char* name);
        virtual void characters(const XML_Char *s, int len);
        
        // SpecFileReader functions
        virtual void openFile(const char* filename, bool mzSort = false);
        virtual void setIdType(SPEC_ID_TYPE type);
        virtual bool getSpectrum(int scanNumber, SpecData& spectrum, 
                                 SPEC_ID_TYPE findBy, bool getPeaks);
        virtual bool getSpectrum(string scanName, SpecData& spectrum, bool);
        virtual bool getNextSpectrum(SpecData& spectrum, bool);

    private:
        enum STATE {ROOT_STATE, SEARCH_STATE, ELEMENT_STATE,
                    MOD_STATE, SPECTRUM_STATE, 
                    MATCH_STATE, PEAKS_STATE};
        struct MOD {
            string name;
            double deltaMass;
        };
        STATE state_;
        PSM* curPSM_;
        Spectrum* curSpec_;
        double retentionTime_;
        vector<PEAK_T> curPeaks_;
        string peaksStr_;
        size_t expectedNumPeaks_;
        double curSpecMz_;
        double probCutOff_;
        bool skipMods_;
        map<string, SpecData*> spectrumMap_;
        string nextWord_;  // tmp holder for element values
        map<string, double> elementTable_;
        map<string, double> modTable_;
        MOD curMod_;
        string curElement_;
        string curSearchID_;
        map<string, string> searchIdFileMap_; // filename for each id
        map<string, vector<PSM*> > searchIdPsmMap_; // PSMs stored by search ID
        vector<PSM*>* curSearchPSMs_;// pointer to map element for current match

        void parseSearchID(const XML_Char** attr);
        void parseSpectrumFilename(const XML_Char** attr);
        void parseSpectrumElement(const XML_Char** attr);
        void parseMatchElement(const XML_Char** attr);
        void parseMatchModElement(const XML_Char** attr);
        double getModMass(const string& name);
        void parsePeaks(const XML_Char** attr);
        void saveMatch();
        void saveSpectrum();
        void getElementName();
        void getElementMass();
        void addElement(double& mass, string element, int count = 1);
        void getModName();
        void getModFormula(bool addMasses = true);
        void addMod();

    };
    
} // namespace


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
