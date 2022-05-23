//
// Original author: Matt Chambers <matt.chambers42@gmail.com>
//
// Copyright 2020 Matt Chambers
//
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

#ifndef DIANNSPECLIB_READER_H
#define DIANNSPECLIB_READER_H

#include "BuildParser.h"


namespace BiblioSpec
{
    class DiaNNSpecLibReader : public BuildParser, public SpecFileReader
    {
        public:
        DiaNNSpecLibReader(BlibBuilder& maker, const char* specLibFile, const ProgressIndicator* parent_progress);
        ~DiaNNSpecLibReader();
        bool parseFile();   // BuildParser virtual function
        std::vector<PSM_SCORE_TYPE> getScoreTypes();   // BuildParser virtual function

        private:
        class Impl;
        std::unique_ptr<Impl> impl_;

        // SpecFileReader interface
        virtual void openFile(const char*, bool mzSort = false);
        virtual void setIdType(SPEC_ID_TYPE type);
        virtual bool getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks = true);
        virtual bool getSpectrum(string identifier, SpecData& returnData, bool getPeaks = true);
        virtual bool getNextSpectrum(BiblioSpec::SpecData&, bool getPeaks = true);
    };
}

#endif // DIANNSPECLIB_READER_H
