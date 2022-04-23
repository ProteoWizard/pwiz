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
        vector<PSM_SCORE_TYPE> getScoreTypes();
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
        int lastFilePosition_;
        ProgressIndicator* readSpecProgress_; // each spec read from file
        double curSpecMz_;
        double probCutOff_;
        bool skipMods_;
        bool skipNTermMods_;
        bool skipCTermMods_;
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
        void parseMatchModElement(const XML_Char** attr, bool termMod);
        double getModMass(const string& name);
        void parsePeaks(const XML_Char** attr);
        void saveMatch();
        void saveSpectrum();
        void getElementName();
        void initializeMod();
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
