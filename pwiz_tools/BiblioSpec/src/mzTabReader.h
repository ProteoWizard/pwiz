//
// Original author: Kaipo Tamura <kaipot@uw.edu>
//
// Copyright 2018 University of Washington - Seattle, WA 98195
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
#include "UnimodParser.h"
#include <boost/tokenizer.hpp>
#include <boost/tuple/tuple.hpp>

namespace BiblioSpec {

// Class for parsing .mzTab files
class mzTabReader : public BuildParser {

public:
    mzTabReader(BlibBuilder& maker, const char* filename, const ProgressIndicator* parentProgress);
    ~mzTabReader();

    bool parse();
    bool parseFile();
    vector<PSM_SCORE_TYPE> getScoreTypes();

private:
    std::string filename_;
    ifstream file_;
    int lineNum_;
    UnimodParser unimod_;
    std::vector< boost::tuple<std::string, bool, PSM_SCORE_TYPE, BUILD_INPUT> > scoreTypes_;
    size_t scoreIdxVector_; // the index of the chosen score type scoreTypes_
    size_t scoreIdxFile_; // the psm_search_engine_score index in the file
    std::map<int, std::string> runs_; // run number -> filename
    map< std::string, vector<PSM*> > fileMap_; // store psms by filename
    std::map<std::string, size_t> psh_; // column label -> index

    void collectPsms();
    void parseLine(const std::string& line);
    void parseCharge(const std::vector<std::string>& fields, int& outCharge);
    bool parseSequence(const std::vector<std::string>& fields, std::string& outSequence, std::vector<SeqMod>& outMods);
    void parseSpectrum(const std::vector<std::string>& fields, std::vector< std::pair<std::string, std::string> >& outSpectra);
    bool parseScore(const std::vector<std::string>& fields, double& outScore);

    static const std::string NULL_FIELD;
    static const std::string PSM_CHARGE_FIELD;
    static const std::string PSM_SEQ_FIELD;
    static const std::string PSM_MODS_FIELD;
    static const std::string PSM_SPEC_FIELD;
    static const std::string PSM_SCORE_FIELD;
};

} // namespace
