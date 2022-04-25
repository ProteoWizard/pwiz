//
// $Id$
//
//
// Original author: Kaipo Tamura <kaipot@uw.edu>
//
// Copyright 2016 University of Washington - Seattle, WA 98195
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

namespace BiblioSpec {

class ProxlXmlReader : public BuildParser {
public:
    ProxlXmlReader(BlibBuilder& maker, const char* filename, const ProgressIndicator* parentProgress);
    virtual ~ProxlXmlReader();
    
    virtual bool parseFile();
    std::vector<PSM_SCORE_TYPE> getScoreTypes();

    virtual void startElement(const XML_Char* name, const XML_Char** attr);
    virtual void endElement(const XML_Char* name);
    virtual void characters(const XML_Char *s, int len);

protected:
    enum STATE {
        INVALID_STATE, ROOT_STATE,
        REPORTED_PEPTIDES_STATE, REPORTED_PEPTIDE_STATE,
        PEPTIDES_STATE, PEPTIDE_STATE, MODIFICATIONS_STATE, LINKED_POSITIONS_STATE,
        PSMS_STATE, PSM_STATE, FILTERABLE_PSM_ANNOTATIONS_STATE,
        STATIC_MODIFICATIONS_STATE
    };

    enum class LinkType
    {
        Unlinked,
        Crosslink,
        Looplink,
        Other
    };

    class ProxlPeptide {
    public:
        ProxlPeptide() {}
        ProxlPeptide(const std::string& sequence): sequence_(sequence) {}
        ~ProxlPeptide() {}
        double mass() const { return ProxlXmlReader::calcMass(sequence_, mods_); }

        std::string sequence_;
        std::vector<SeqMod> mods_;
        std::vector<int> links_;
    };

    struct ProxlPsm : PSM {
        ProxlPsm() : PSM(), linkerMass_(0.0) {}
        double linkerMass_;
    };
    
    struct ProxlMatches {
        std::vector<ProxlPeptide> peptides_;
        std::map< std::string, vector<ProxlPsm*> > psms_;
        LinkType linkType_;
    };

    static double aaMasses_[128];
    static double calcMass(const std::string& sequence, const std::vector<SeqMod>& mods);

    void calcPsms();
    void applyStaticMods(const std::string& sequence, std::vector<SeqMod>& mods, int crosslinkPosition);

    std::vector<STATE> state_;
    std::map< std::string, vector<PSM*> > fileToPsms_;
    std::map< char, vector<double> > staticMods_;

    ProxlPsm* curProxlPsm_;
    std::vector<ProxlMatches> proxlMatches_;
    vector<std::string> dirs_;       ///< directories where spec files might be
    vector<std::string> extensions_; ///< possible extensions of spec files (.mzXML)
};

}
