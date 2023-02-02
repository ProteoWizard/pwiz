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

#include "BuildParser.h"
#include "Verbosity.h"
#include "pwiz/data/identdata/IdentDataFile.hpp"

namespace BiblioSpec{
    
    /**
     * Class for parsing mzIdentML files.
     */
    class MzIdentMLReader : public BuildParser {
        
    public:
        MzIdentMLReader(BlibBuilder& maker,
                        const char* mzidFileName,
                        const ProgressIndicator* parent_progress);
        ~MzIdentMLReader();
        
        bool parseFile();
        vector<PSM_SCORE_TYPE> getScoreTypes();

    private:
        enum ANALYSIS { UNKNOWN_ANALYSIS,
                        SCAFFOLD_ANALYSIS,
                        BYONIC_ANALYSIS,
                        MSGF_ANALYSIS,
                        PEPTIDESHAKER_ANALYSIS,
                        MASCOT_ANALYSIS,
                        PEAKS_ANALYSIS,
                        PROT_PILOT_ANALYSIS,
                        GENERIC_QVALUE_ANALYSIS };

        ANALYSIS analysisType_;
        pwiz::identdata::IdentDataFile* pwizReader_;
        map< string, vector<PSM*> > fileMap_; // vector of PSMs for each file
        double scoreThreshold_;
        bool isScoreLookup_;

        // name some file accessors to make the code more readable
        vector<pwiz::identdata::SpectrumIdentificationListPtr>::const_iterator list_iter_;
        vector<pwiz::identdata::SpectrumIdentificationListPtr>::const_iterator list_end_;
        vector<pwiz::identdata::SpectrumIdentificationResultPtr>::const_iterator result_iter_;
        vector<pwiz::identdata::SpectrumIdentificationItemPtr>::const_iterator item_iter_;


        void collectPsms(std::map<pwiz::identdata::DBSequencePtr, Protein>& proteins);
        void extractModifications(pwiz::identdata::PeptidePtr peptide, PSM* psm);
        void extractIonMobility(const pwiz::identdata::SpectrumIdentificationResult& result, const pwiz::identdata::SpectrumIdentificationItem& item, PSM* psm);
        double getScore(const pwiz::identdata::SpectrumIdentificationItem& item);
        void setAnalysisType(ANALYSIS analysisType);
        static PSM_SCORE_TYPE analysisToScoreType(ANALYSIS analysisType);
        bool passThreshold(double score);
        static bool stringToScan(const std::string& name, PSM* psm);
    };
} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */

