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
#include "smart_stmt.h"
#include "BlibUtils.h"
#include "Verbosity.h"

using namespace std;

namespace BiblioSpec {

/*
struct sqlite3;
struct sqlite3_stmt;
#include "smart_stmt.h"
*/

class BlibMaker
{
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

    // Property accessors
    sqlite3* getDb() const { return db; }
    const char* getLibName() const { return lib_name; }

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

    void insertPeaks(int spectraID, int levelCompress, int peaksCount, 
                     double* pM, float* pI);
    void beginTransaction();
    void endTransaction();
    void undoActiveTransaction();

protected:
    virtual int parseNextSwitch(int i, int argc, char* argv[]);

    virtual void attachAll() {}
    virtual void createTables();
    virtual void createTable(const char* tableName);
    virtual void updateTables();
    virtual void updateLibInfo();
    
    void setLibName(const string& name);
    virtual string getLSID();
    virtual void getNextRevision(int* dataRev);

    bool tableExists(const char* schmaTmp, const char* tableName);
    bool tableColumnExists(const char* schmaTmp, const char* tableName, 
                           const char* columnName);
    int getNewFileId(const char* libName, int specId);
    int getUnknownFileId();
    int transferSpectrum(const char* schemaTmp, 
                         int spectraTmpID, 
                         int copies,
                         bool tmpHasAdditionalColumns = true);
    void transferModifications(const char* schemaTmp, int spectraID, int spectraTmpID);
    void transferPeaks(const char* schemaTmp, int spectraID, int spectraTmpID);
    void transferSpectrumFiles(const char* schmaTmp);
    void transferTable(const char* schemaTmp, const char* tableName);

    int getSpectrumCount(const char* schemaName = NULL);
    int countSpectra(const char* schemaName = NULL);
    void getRevisionInfo(const char* schemaName, int* major, int* minor);

    // Property accessors
    bool isOverwrite() const { return overwrite; };
    void setOverwrite(bool value) { overwrite = value; }

    bool isRedundant() const { return redundant; };
    void setRedundant(bool value) { redundant = value; }

    bool isStdinput() const { return stdinput;};
    void setStdinput(bool value) { stdinput = value;}

protected:
    char zSql[8192];
    bool verbose;

private:
    const char* libIdFromName(const char* name);

private:
    sqlite3* db;
    const char* authority;
    const char* lib_name;
    const char* lib_id;
    int cache_size; 
    bool redundant;
    bool overwrite;
    bool stdinput;
    string message;
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
