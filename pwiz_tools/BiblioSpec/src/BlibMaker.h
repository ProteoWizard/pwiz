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

#include <fstream>
#include <cstring>
#include <cstdio>
#include <cstdlib>
#include <iostream>
#include <map>
#include <utility>
#include "smart_stmt.h"
#include "BlibUtils.h"
#include "Verbosity.h"
#include <boost/optional.hpp>


namespace BiblioSpec {

/*
struct sqlite3;
struct sqlite3_stmt;
#include "smart_stmt.h"
*/

class BlibMaker
{

    // Due to original omission of a schema version number, and the unused
    // integer values minorVersion, the minorVersion field has been taken
    // for use as a schemaVersion
#define MAJOR_VERSION_CURRENT 0
#define MINOR_VERSION_CURRENT 10

    // Make sure you update version_history_comment (below) when bumping the schema version
#define MIN_VERSION_TIC       10 // Version 10 adds TIC as a column
#define MIN_VERSION_PROTEINS   9 // Version 9 adds Proteins and RefSpectraProteins tables
#define MIN_VERSION_RT_BOUNDS  8 // Version 8 adds startTime and endTime
#define MIN_VERSION_PEAK_ANNOT 7 // Version 7 adds peak annotations
#define MIN_VERSION_IMS_UNITS  6 // Version 6 generalizes ion mobility to value, high energy offset, and type (currently drift time msec, and inverse reduced ion mobility Vsec/cm2)
#define MIN_VERSION_SMALL_MOL  5 // Version 5 adds small molecule columns
#define MIN_VERSION_CCS        4 // Version 4 adds collisional cross section, removes ion mobility type (which distinguished CCS vs DT as value type), supports drift time only
#define MIN_VERSION_IMS_HEOFF  3 // Version 3 adds product ion mobility offset information for Waters Mse IMS
#define MIN_VERSION_IMS        2 // Version 2 adds ion mobility information
const char * version_history_comment =  // This gets written to the output db
        "-- Schema version number:\n"
        "-- Version 10 adds TIC as a column\n"
        "-- Version 9 adds Proteins and RefSpectraProteins tables\n"
        "-- Version 8 adds startTime and endTime\n"
        "-- Version 7 adds peak annotations\n"
        "-- Version 6 generalized ion mobility to value, high energy offset, and type (currently drift time msec, and inverse reduced ion mobility Vsec/cm2)\n"
        "-- Version 5 added small molecule columns\n"
        "-- Version 4 added collisional cross section for ion mobility, still supports drift time only\n"
        "-- Version 3 added product ion mobility offset information for Waters Mse IMS\n"
        "-- Version 2 added ion mobility information\n";

public:
    BlibMaker(void);
    virtual ~BlibMaker(void);

    int parseCommandArgs(int argc, char* argv[]);

    //virtual void usage() = 0;
    // !!!UNDO ME
    virtual void usage();

    virtual void init();
    virtual void commit();
    virtual bool is_empty();
    virtual void abort_current_library();

    static void verifyFileExists(string file);
    void openDb(const char* file);

    // Property accessors
    sqlite3* getDb() const { return db; }
    const char* getLibName() const { return lib_name; }
    bool isScoreLookupMode() const { return scoreLookupMode_; }

    // Utility functions
    void setMessage(const char* value) { message = value; }
    void sql_stmt(const char* stmt, bool ignoreFailure = false) const;
    bool check_rc(int rc, const char* stmt, 
                  const char* msg = NULL, bool dieOnFailure = true) const;
    bool just_check_step(int rc, sqlite3_stmt *pStmt, const char* stmt, 
                    const char* msg = NULL) const;
    void check_step(int rc, sqlite3_stmt *pStmt, const char* stmt, 
                    const char* msg = NULL) const;
    void fail_sql(int rc, const char* stmt, const char* err, 
                  const char* msg = NULL) const;

    int getFileId(const std::string& file, double cutoffScore);
    int addFile(const std::string& file, double cutoffScore, const std::string& idFile);
    void insertPeaks(int spectraID, int levelCompress, int peaksCount, 
                     double* pM, float* pI);
    void beginTransaction();
    void endTransaction();
    void undoActiveTransaction();
    
    bool ambiguityMessages() const { return ambiguityMessages_; }
    bool keepAmbiguous() const { return keepAmbiguous_; }
    bool isHighPrecisionModifications() const { return highPrecisionModifications_;}
    boost::optional<bool> preferEmbeddedSpectra() const { return preferEmbeddedSpectra_; }

protected:
    virtual int parseNextSwitch(int i, int argc, char* argv[]);

    virtual void attachAll() {}
    virtual void createTables(vector<string> &commands, bool execute); //  Generates all the "CREATE TABLE" commands and optionally executes them in the current open library.
    virtual void createTable(const char* tableName, vector<string> &commands, bool execute); //Generates the SQLite commands to create and initialize the named table, and optionally execute them while doing so
    virtual void updateTables();
    virtual void updateLibInfo();
    
    void setLibName(const string& name);
    virtual string getLSID();
    virtual void getNextRevision(int* dataRev);

    void createUpdatedRefSpectraView(const char* schemaTmp);
    bool tableExists(const char* schmaTmp, const char* tableName);
    bool tableColumnExists(const char* schmaTmp, const char* tableName, 
                           const char* columnName);
    int getNewFileId(const char* libName, int specId);
    int getUnknownFileId();
    int transferSpectrum(const char* schemaTmp, 
                         int spectraTmpID, 
                         int copies,
                         int tableVersion = 0);
    int transferSpectra(const char* schemaTmp,
                        vector<pair<int, int>>& bestSpectraIdAndCount,
                        int tableVersion = 0);
    void transferModifications(const char* schemaTmp, int spectraID, int spectraTmpID);
    void transferPeaks(const char* schemaTmp, int spectraID, int spectraTmpID);
    void transferPeakAnnotations(const char* schemaTmp, int spectraID, int spectraTmpID);
    void transferRefSpectraProteins(const char* schemaTmp, int spectraID, int spectraTmpID);
    void transferSpectrumFiles(const char* schmaTmp);
    void transferProteins(const char* schemaTmp);
    void transferTable(const char* schemaTmp, const char* tableName);

    int getSpectrumCount(const char* schemaName = NULL);
    int countSpectra(const char* schemaName = NULL);
    void getRevisionInfo(const char* schemaName, int* major, int* minor);
    virtual double getCutoffScore() const;

    // Property accessors
    bool isOverwrite() const { return overwrite; };
    void setOverwrite(bool value) { overwrite = value; }

    bool isRedundant() const { return redundant; };
    void setRedundant(bool value) { redundant = value; }

    void setHighPrecisionModifications(bool value) { highPrecisionModifications_ = value; }

protected:
#define ZSQLBUFLEN 8192
    char zSql[ZSQLBUFLEN];
    bool verbose;
    bool ambiguityMessages_;
    bool keepAmbiguous_;
    bool highPrecisionModifications_;
    boost::optional<bool> preferEmbeddedSpectra_;

private:
    const char* libIdFromName(const char* name);

private:
    sqlite3* db;
    bool scoreLookupMode_;
    const char* authority;
    const char* lib_name;
    const char* lib_id;
    int cache_size; 
    bool redundant;
    bool overwrite;
    string message;
    map< string, pair<int, double> > fileIdCache_; // file -> id, cutoff
    map<int,int> oldToNewFileID_;
    int unknown_file_id; // if incoming libs don't have file ids,
                         // use this id in new library

    static const int pages_per_meg;
};

    } // namespace
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
