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

namespace BiblioSpec {

// Class for parsing .osw files
class OSWReader : public BuildParser, public SpecFileReader {

public:
    OSWReader(BlibBuilder& maker, const char* filename, const ProgressIndicator* parentProgress);
    ~OSWReader();

    bool parseFile();
    std::vector<PSM_SCORE_TYPE> getScoreTypes();
    // these inherited from SpecFileReader
    virtual void openFile(const char*, bool) {}
    virtual void setIdType(SPEC_ID_TYPE) {}  
    virtual bool getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks) { return false; }
    virtual bool getSpectrum(std::string identifier, SpecData& returnData, bool getPeaks);
    virtual bool getNextSpectrum(SpecData&, bool) { return false; }

private:
    std::string filename_;
    sqlite3* osw_;
    double scoreThreshold_;
    UnimodParser unimod_;
    std::map< std::string, vector<PSM*> > fileMap_; // store psms by filename
    std::map<std::string, SpecData> spectra_;

    void transferPeaks(SpecData* dst, std::vector<double>& mzs, std::vector<float>& intensities);
};

} // namespace
