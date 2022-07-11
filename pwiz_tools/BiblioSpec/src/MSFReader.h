//
// $Id$
//
//
// Original author: Kaipo Tamura <kaipot@u.washington.edu>
//
// Copyright 2013 University of Washington - Seattle, WA 98195
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

#ifndef MSF_READER_H
#define MSF_READER_H

#include <cstdio>
#include <cstring>
#include <fstream>
#include <set>

#include "BuildParser.h"
#include "MSFSpecReader.h"
#include "sqlite3.h"
#include "zlib.h"
#include "contrib/minizip/unzip.h"


namespace BiblioSpec
{
    class MSFReader : public BuildParser, public SpecFileReader
    {
    public:
        MSFReader(BlibBuilder& maker,
                  const char* msfFile,
                  const ProgressIndicator* parent_progress);
        ~MSFReader();
        void openFile();
        bool parseFile();   // BuildParser virtual function
        vector<PSM_SCORE_TYPE> getScoreTypes();   // BuildParser virtual function

    private:
        /* These are for decompressing the spectrum archives */
        static const int CHUNK_SIZE = 16384;
        static const int MAX_FILENAME = 1024;
        typedef struct zlib_mem
        {
            char* base;
            uLong size;
            uLong cur_offset;

            zlib_mem()
            {
                base = NULL;
                size = 0;
                cur_offset = 0;
            }
        } zlib_mem;

        struct ProcessedMsfSpectrum {
            PSM* psm;
            double qvalue;
            double altScore;
            bool ambiguous;

            ProcessedMsfSpectrum():
                psm(NULL), qvalue(std::numeric_limits<double>::max()), altScore(-std::numeric_limits<double>::max()), ambiguous(false) {
            };

            ProcessedMsfSpectrum(PSM* psmPtr, double qvalueScore, double alt):
                psm(psmPtr), qvalue(qvalueScore), altScore(alt), ambiguous(false) {
            };
        };

        class ModSet {
        public:
            ModSet(sqlite3* db, bool filtered);
            ~ModSet() {}

            const vector<SeqMod>& getMods(int peptideId);
            const vector<SeqMod>& getMods(int workflowId, int peptideId);
        private:
            map< int, map< int, vector<SeqMod> > > mods_; // workflowId -> peptideId -> mods
            vector<SeqMod> dummy_;

            map< int, vector<SeqMod> >& getWorkflowMap(int workflowId);
            void addMod(int workflowId, int peptideId, int position, double mass);
        };

        sqlite3* msfFile_;
        const char* msfName_;
        int schemaVersionMajor_, schemaVersionMinor_;
        bool filtered_; // msf is complete, unfiltered; pdResult is filtered and persistent version of msf
        map< string, map< PSM_SCORE_TYPE, vector<PSM*> > > fileMap_;
        map<int, string> fileNameMap_;
        map<string, SpecData*> spectra_;

        bool versionLess(int major, int minor) const;
        static string uniqueSpecId(int specId, int workflowId);
        void collectSpectra();
        string unzipSpectrum(const string& specId, const void* src, size_t srcLen);
        void readSpectrum(const string& specId, string& spectrumXml, int* numPeaks, double** mzs, float** intensities);
        void collectPsms();
        void getScoreInfo(sqlite3_stmt** outStmt, int* outResultCount, PSM_SCORE_TYPE* outScoreType, int* outPepConfidence, int* outProtConfidence);
        void initFileNameMap();
        void removeFromFileMap(PSM* psm);
        string fileIdToName(int fileId);
        bool hasQValues();
        map< int, vector<SeqMod> > getMods();
        map<int, int> getFileIds();

        // unzip memory functions
        unzFile openMemZip(const void* src, size_t srcLen);
        static voidpf ZCALLBACK fopenMem(voidpf opaque, const char* filename, int mode);
        static uLong ZCALLBACK freadMem(voidpf opaque, voidpf stream, void* buf, uLong size);
        static long ZCALLBACK ftellMem(voidpf opaque, voidpf stream);
        static long ZCALLBACK fseekMem(voidpf opaque, voidpf stream, uLong offset, int origin);
        static int ZCALLBACK fcloseMem(voidpf opaque, voidpf stream);
        static int ZCALLBACK ferrorMem(voidpf opaque, voidpf stream);

        // sqlite helper functions
        sqlite3_stmt* getStmt(const string& query);
        static sqlite3_stmt* getStmt(sqlite3* handle, const string& query);
        static bool hasNext(sqlite3_stmt** statement);
        int getRowCount(string table);
        static bool tableExists(sqlite3* handle, string table);
        static bool columnExists(sqlite3* handle, string table, string columnName);

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