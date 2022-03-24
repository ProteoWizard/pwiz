//
// $Id: ShimadzuMLBReader.h 9898 2016-07-13 22:38:39Z kaipot $
//
//
// Original author: Brian Pratt <bspratt@u.washington.edu>
// Based on msfReader by Kaipo Tamura <kaipot@u.washington.edu>
//
// Copyright 2016 University of Washington - Seattle, WA 98195
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

#ifndef SHIMADZU_MLB_READER_H
#define SHIMADZU_MLB_READER_H

#include "BuildParser.h"
#include "sqlite3.h"


namespace BiblioSpec
{
    class ShimadzuMLBReader : public BuildParser, public SpecFileReader
    {
    public:
        ShimadzuMLBReader(BlibBuilder& maker,
                  const char* msfFile,
                  const ProgressIndicator* parent_progress);
        ~ShimadzuMLBReader();
        bool parseFile();   // BuildParser virtual function
        vector<PSM_SCORE_TYPE> getScoreTypes();

    private:
        sqlite3* mlbFile_;
        const char* mlbName_;
        int schemaVersion_;
        map< string, vector<PSM*> > fileMap_;
        map<int, string> fileNameMap_;
        map<int, SpecData*> spectra_;
        map<int, int> spectraChargeStates_;

        void readMSMSSP();

        static double ReadDoubleFromBuffer(const char *& buf);
        static const char* getAdduct(int adductType, int& charge);
        // sqlite helper functions
        sqlite3_stmt* getStmt(const string& query) const;
        static sqlite3_stmt* getStmt(sqlite3* handle, const string& query);
        static bool hasNext(sqlite3_stmt* statement);
        int getRowCount(string table) const;

        // SpecFileReader interface
        virtual void openFile(const char*, bool mzSort = false);
        virtual void setIdType(SPEC_ID_TYPE type);
        virtual bool getSpectrum(int identifier, SpecData& returnData, 
                                 SPEC_ID_TYPE findBy, bool getPeaks = true);
        virtual bool getSpectrum(string identifier, SpecData& returnData, bool getPeaks = true);
        virtual bool getNextSpectrum(BiblioSpec::SpecData&, bool getPeaks = true);
    };
}

#endif