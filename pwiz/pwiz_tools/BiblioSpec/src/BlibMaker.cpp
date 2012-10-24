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

#include <sqlite3.h>
#include <time.h>

#include "zlib.h"
#include "BlibMaker.h"

namespace BiblioSpec {

static const char ERROR_GENERIC[] = "Unexpected failure.";


// SQLite uses 1.5K pages, and PRAGMA cache_size is specified in these pages
//     see http://www.sqlite.org/pragma.html
const int BlibMaker::pages_per_meg = (int) (1024.0 / 1.5);
void BlibMaker::usage() {}


BlibMaker::BlibMaker(void)
: message(ERROR_GENERIC)
{
    db = NULL;
    lib_name = NULL;
    lib_id = NULL;
    authority = "proteome.gs.washington.edu";
    cache_size = 250 * pages_per_meg;

    redundant = true;
    overwrite = false;
    verbose = false;
    stdinput = false;
    unknown_file_id = -1; // none entered yet
}

BlibMaker::~BlibMaker(void)
{
    if (db != NULL)
        sqlite3_close(db);
}

int BlibMaker::parseCommandArgs(int argc, char* argv[])
{
    // Consume all options switches
    int i = 1;
    while (i < argc) {
        char* arg = argv[i];
        if (arg[0] != '-' || strlen(arg) != 2)
            break;
        else
            i = parseNextSwitch(i, argc, argv);
    }
    
    // Must at least have the library name left
    if (argc - i < 1)
        usage();
    
    lib_name = argv[argc - 1];
    if (lib_id == NULL)
        lib_id = libIdFromName(lib_name);
    
    return i;
}

int BlibMaker::parseNextSwitch(int i, int argc, char* argv[])
{
    char* arg = argv[i];
    char switchName = arg[1];

    if (switchName == 'v')
        verbose = true;
    else if (switchName == 'm' && ++i < argc) {
        cache_size = atoi(argv[i]) * pages_per_meg;
        if (cache_size == 0) {
            Verbosity::error("Invalid cache size specified.");
        }
    } else if (switchName == 'a' && ++i < argc) {
        authority = argv[i];
    } else if (switchName == 'i' && ++i < argc) {
        lib_id = argv[i];
    } else {
        usage();
    }
    
    return min(argc, i + 1);
}

void BlibMaker::init()
{
    // Check whether library already exists
    ifstream libName(lib_name);
    if(!libName.good()) {
        overwrite = true; // No library exists to append to
    } else {
        // Remove it, if this is to be an overwrite
        if (overwrite) {
            libName.close();
            remove(lib_name);
            ifstream libName2(lib_name);
            if (libName2.good()) {
                Verbosity::error("Failed to remove existing redundant "
                                 "library '%s'.", lib_name);
            }
        } else {
            // Overwrite, if it is a zero length file
            char ch;
            libName.get(ch);
            if (!libName.good())
                overwrite = true;
        }
    }
    
    if(libName.is_open())
        libName.close();
    
    sqlite3_open(lib_name, &db);
    if (db == 0) {
        Verbosity::error("Failed to create '%s'. Make sure the directory "
                         "exists with write permissions.", lib_name);
    }
    
    message = "Failed to initialize ";
    message += lib_name;
    
    sql_stmt("PRAGMA synchronous=OFF");
    sprintf(zSql, "PRAGMA cache_size=%d", cache_size);
    sql_stmt(zSql);
    sql_stmt("PRAGMA temp_store=MEMORY");
    
    attachAll();
    
    if (overwrite){
        createTables();
    } else {
        // Drop indexes for large numbers of insertions
        sql_stmt("DROP INDEX idxPeptide", true);
        sql_stmt("DROP INDEX idxPeptideMod", true);
        sql_stmt("DROP INDEX idxRefIdPeaks", true);

        // Add any missing tables or columns
        updateTables();
    }

    // these sql statments succeeded, return message to default    
    message = ERROR_GENERIC;
}

/**
 * Return true if there are no spectra in the current library.
 */
bool BlibMaker::is_empty(){
    if( getSpectrumCount() == 0 ){
        return true;
    }
    return false;
}

/**
 * Close the open library and delete the file.
 */
void BlibMaker::abort_current_library(){
    Verbosity::debug("Deleting current library.");

    // close db
    if (db != NULL){
        sqlite3_close(db);
        db = NULL;
    }

    // delete file
    remove(lib_name);
}

/**
 * Marks the beginning of a transaction, a set of statements to be
 * committed together.  Explicit transitions cannot be nested.  Commits
 * any open transactions.  Beginning a transaction tursn off
 * autocommit mode.
 */
void BlibMaker::beginTransaction(){

    // use autocommit mode to check for existing explicit transaction
    // autocommit mode is on by default, turned off by a BEGIN
    if( sqlite3_get_autocommit(db) == 0 ){ // auto commit off
        sql_stmt("COMMIT");
    }

    sql_stmt("BEGIN");
}

/**
 * Marks the end of the transaction by committing any active
 * statements submitted since the last "BEGIN" statement.  If no
 * active statements (autocommit mode is on), no action is taken.
 */
void BlibMaker::endTransaction(){

    if( sqlite3_get_autocommit(db) != 0 ){
        Verbosity::debug("No open transaction to end.");
        return;
    }

    sql_stmt("COMMIT");
}

/**
 * Rolls back any active transaction.  If no transaction is active
 * (i.e. the most recent changes were committed), no action is taken.
 */
void BlibMaker::undoActiveTransaction(){
    if( sqlite3_get_autocommit(db) != 0 ){
        Verbosity::debug("No open transaction to roll back.");
        return;
    }
    // else
    sql_stmt("ROLLBACK");
}

void BlibMaker::commit()
{
    updateLibInfo();

    sql_stmt("BEGIN");

    // Add indexes
    sql_stmt("CREATE INDEX idxPeptide ON RefSpectra (peptideSeq, precursorCharge)");
    sql_stmt("CREATE INDEX idxPeptideMod ON RefSpectra (peptideModSeq, precursorCharge)");
    sql_stmt("CREATE INDEX idxRefIdPeaks ON RefSpectraPeaks (RefSpectraID)");

    // And commit all changes
    sql_stmt("COMMIT");
}

string BlibMaker::getLSID()
{
    char blibLSID[2048];
    const char* libType = (redundant ? "redundant" : "nr");
    sprintf(blibLSID,"urn:lsid:%s:spectral_library:bibliospec:%s:%s",
            authority,libType,lib_id);
    string result(blibLSID);
    return result;
}

/**
 * Executes all the "CREATE TABLE" commands in the current open
 * library.  Adds information to LibInfo and to SpectrumScoreTypes.
 */
void BlibMaker::createTables()
{
    strcpy(zSql,
           "CREATE TABLE LibInfo(libLSID TEXT, "
           "createTime TEXT, "
           "numSpecs INTEGER, "
           "majorVersion INTEGER, "
           "minorVersion INTEGER)");
    sql_stmt(zSql);

    //fill in the LibInfo first
    time_t t= time(NULL);
    char* date = ctime(&t);

    string blibLSID = getLSID();
    sprintf(zSql, "INSERT INTO LibInfo values('%s','%s',%i,0,1)", // schemaVersion = 1
            blibLSID.c_str(), date, 
            -1);// init count as -1 to mean 'not counted', 0 could be 'no spec'
    sql_stmt(zSql);

    strcpy(zSql,
           "CREATE TABLE RefSpectra (id INTEGER primary key autoincrement not null, "
           "peptideSeq VARCHAR(150), "
           "precursorMZ REAL, "
           "precursorCharge INTEGER, " // tiny int?
           "peptideModSeq VARCHAR(200), "
           "prevAA CHAR(1), "
           "nextAA CHAR(1), "
           "copies INTEGER, "
           "numPeaks INTEGER, "
           "retentionTime REAL, "
           "fileID INTEGER, "
           "SpecIDinFile VARCHAR(256), "// spec label (id) in source file
           "score REAL, "
           "scoreType TINYINT)");
    sql_stmt(zSql);

    strcpy(zSql,
           "CREATE TABLE Modifications (id INTEGER primary key autoincrement not null,"
           "RefSpectraID INTEGER, "
           "position INTEGER, "
           "mass REAL)");
    sql_stmt(zSql);

    strcpy(zSql,
           "CREATE TABLE RefSpectraPeaks(RefSpectraID INTEGER, "
           "peakMZ BLOB, "
           "peakIntensity BLOB)");
    sql_stmt(zSql);

    createTable("SpectrumSourceFiles");
    createTable("ScoreTypes");
}

/**
 * Create specific tables.  May be added to new libraries (as part of
 * createTables()) or to existing libraries (as part of updateTables()).
 */
void BlibMaker::createTable(const char* tableName){

    if( strcmp(tableName, "SpectrumSourceFiles") == 0 ){
        strcpy(zSql,
               "CREATE TABLE SpectrumSourceFiles (id INTEGER PRIMARY KEY "
               "autoincrement not null,"
               "fileName VARCHAR(512) )" );
        sql_stmt(zSql);

    } else if( strcmp(tableName, "ScoreTypes") == 0 ){
        // set key = enum value of score type; don't autoincrement
        strcpy(zSql,
               "CREATE TABLE ScoreTypes (id INTEGER PRIMARY KEY, "
               "scoreType VARCHAR(128) )" );
        sql_stmt(zSql);
        // insert all score types
        for(int i=0; i < NUM_PSM_SCORE_TYPES; i++){
            sprintf(zSql, 
                    "INSERT INTO ScoreTypes(id, scoreType) VALUES(%d, '%s')",
                    i, scoreTypeToString((PSM_SCORE_TYPE)i));//scoreTypeNames[i]);
            sql_stmt(zSql);
        }
    } else {
        Verbosity::error("Cannot create '%s' table. Unknown name.",
                         tableName);
    }
}

/**
 * Checks the schema of the library in which we are saving spectra and
 * adds any missing tables and columns.
 */
void BlibMaker::updateTables(){
    // SpectrumSourceFiles table
    if( !tableExists("main", "SpectrumSourceFiles") ){
        createTable("SpectrumSourceFiles");

        // add an unknown source file id
        strcpy(zSql, "INSERT INTO SpectrumSourceFiles (fileName) "
               "VALUES ('UNKNOWN')");
        sql_stmt(zSql); 
        unknown_file_id = (int)sqlite3_last_insert_rowid(db);

    } else {
        // if the table already exists, look for the UNKNOWN file
        unknown_file_id = getUnknownFileId();
    }
    
    // ScoreTypes table
    if( !tableExists("main", "ScoreTypes") ){
        createTable("ScoreTypes");
    }

    const char* newColumns[] = {"retentionTime", 
                                "fileID", 
                                "SpecIDinFile",
                                "score",
                                "scoreType" };
    const char* newTypes[] = {"REAL", 
                              "INTEGER", 
                              "VARCHAR(256)", 
                              "REAL", 
                              "TINYINT" };
    int numCols = sizeof(newColumns) / sizeof(char*);
    for(int i = 0; i < numCols; i++){
        if( ! tableColumnExists("main", "RefSpectra", newColumns[i]) ){
            sprintf(zSql,
                    "ALTER TABLE RefSpectra ADD %s %s",
                    newColumns[i], newTypes[i]);
            sql_stmt(zSql);
        }
    }
    // update fileID and scoreType to be unknown in all existing spec
    sprintf(zSql, "UPDATE RefSpectra SET fileID = '%d' "
            "WHERE fileID IS NULL", unknown_file_id );
    sql_stmt(zSql);
    sprintf(zSql, "UPDATE RefSpectra SET scoreType = '%d' "
            "WHERE scoreType IS NULL", UNKNOWN_SCORE_TYPE );
    sql_stmt(zSql);
}

/**
 * Query the SpectrumSourceFiles table for the entry 'UNKNOWN'.
 * Return its id if present, else -1.
 */
int BlibMaker::getUnknownFileId(){
    if( ! tableExists("main", "SpectrumSourceFiles") ){
        return -1;
    }

    sprintf(zSql, "SELECT id FROM SpectrumSourceFiles "
            "WHERE fileName = 'UNKNOWN'");
    smart_stmt pStmt;
    int return_code = sqlite3_prepare(db, zSql, -1, &pStmt, 0);
    check_rc(return_code, zSql, "Failed looking for spectrum file 'UNKNOWN'");
    return_code = sqlite3_step(pStmt);
    if( return_code != SQLITE_ROW ){ // then it's not there
        return -1;
    } // else
    return sqlite3_column_int(pStmt, 0);
}

/**
 * Check if the incoming library contains the given table name.
 * \returns True if table exists or false if it does not.
 */
bool BlibMaker::tableExists(const char* schemaTmp, const char* tableName){
    sprintf(zSql,
            "SELECT name FROM %s.sqlite_master WHERE name = \"%s\"",
            schemaTmp, tableName);
    smart_stmt pStmt;
    int return_code = sqlite3_prepare(db, zSql, -1, &pStmt, 0);
    check_rc(return_code, zSql, "Failed checking for the existance of a table");
    return_code = sqlite3_step(pStmt);
    // if a row exists, the table exists
    return ( return_code == SQLITE_ROW );
}

/**
 * Check if the given table in the incoming library contains the given
 * column name.
 * \returns True if the column exists or false if it does not.
 */
bool BlibMaker::tableColumnExists(const char* schemaTmp, 
                                  const char* tableName, 
                                  const char* columnName){
    sprintf(zSql,
            "PRAGMA %s.table_info(%s)",
            schemaTmp, tableName);
    smart_stmt pStmt;
    int return_code = sqlite3_prepare(db, zSql, -1, &pStmt, 0);
    check_rc(return_code, zSql, "Failed checking for existance of a column");
    return_code = sqlite3_step(pStmt);

    while(return_code == SQLITE_ROW){
        string cur_col = 
            reinterpret_cast<const char*>(sqlite3_column_text(pStmt,1));
        if( cur_col.compare(columnName) == 0 ){
            return true;
        }
        return_code = sqlite3_step(pStmt);
    }
    return false;
}

/**
 * Copy all of the spectrum source files from the incoming library to
 * the new library.  Create a map from the old fileID to the new one
 * for future spectrum inserts.
 */
// todo if multiple tmp dbs are attached, make a map for each
// schemaTmp name.  map of maps?  map of vectors?
void BlibMaker::transferSpectrumFiles(const char* schemaTmp){ //i.e. db name

    // first check to see if the incoming library has spectrum source files
    if( ! tableExists(schemaTmp, "SpectrumSourceFiles") ){
        // add "unknown" source file if we haven't already
        if( unknown_file_id == -1 ){
            Verbosity::warn("Orignal library does not contain filenames for "
                            "the  library spectra");
            strcpy(zSql, "INSERT INTO SpectrumSourceFiles (fileName) "
                   "VALUES ('UNKNOWN')");
            sql_stmt(zSql); 
            unknown_file_id = (int)sqlite3_last_insert_rowid(db);
        }

        return;
    }
    sprintf(zSql, "SELECT id, fileName FROM %s.SpectrumSourceFiles",
            schemaTmp);
    smart_stmt pStmt;
    int rc = sqlite3_prepare(db, zSql, -1, &pStmt, 0);
    check_rc(rc, zSql, "Failed selecting file names from tmp db.");
    rc = sqlite3_step(pStmt);
    while( rc == SQLITE_ROW ){
        // if fileName doesn't exist in main db...
        sprintf(zSql, 
                "INSERT INTO SpectrumSourceFiles (fileName) VALUES ('%s')",
                sqlite3_column_text(pStmt, 1));
        sql_stmt(zSql);

        // map old id (looked up) to new (current row number)
        oldToNewFileID_[sqlite3_column_int(pStmt, 0)] = 
            (int)sqlite3_last_insert_rowid(db);

        // else oldToNewFileID_[sqlite3_column_int(pStmt, 0)] = id of existing entry
        rc = sqlite3_step(pStmt);
    }
}

/**
 * In preparation for transfering spectra from one library to another,
 * get what will be the fileID for the new library.  Look up the
 * fileID of the given spectrum in the temp library.  From that look
 * up the id of the same file in the new library in the cached map of
 * file IDs.  If not in the cache, add this file to the new library.
 * \returns The fileID field to be used when entering this spectrum in
 * the new library.
 */
int BlibMaker::getNewFileId(const char* libName, int specID){
    // get the fileID in the temp library
    sprintf(zSql, "SELECT fileID FROM %s.RefSpectra WHERE id = %d", 
            libName, specID);

    smart_stmt pStmt;
    int rc = sqlite3_prepare(db, zSql, -1, &pStmt, 0);
    if( ! check_rc(rc, zSql, "", false)) { // false=don't die on failure
        return unknown_file_id; // assume failure b/c fileID doesn't exist
    } // else, find the new id from the old

    rc = sqlite3_step(pStmt);
    int oldFileID = sqlite3_column_int(pStmt, 0);

    int newID = -1;

    // first look for the newID in the map
    map<int,int>::iterator it = oldToNewFileID_.find(oldFileID);
    if( it != oldToNewFileID_.end() ){
        newID = it->second;
    } else {
        // insert it into the new db
        sprintf(zSql, "INSERT INTO main.SpectrumSourceFiles(fileName) "
                "SELECT fileName FROM %s.SpectrumSourceFiles "
                "WHERE %s.SpectrumSourceFiles.id = %d",
                libName, libName, oldFileID);
        sql_stmt(zSql);
        newID = (int)sqlite3_last_insert_rowid(getDb());

        // add it to map
        oldToNewFileID_[oldFileID] = newID;
    }


    return newID;
}

/**
 * Extract a spectrum from the given database name (i.e. schemaTmp) (must
 * currently be attached) and insert it into the default database.
 * \returns ID of spectrum in new table (db into which it was transfered).
 */
int BlibMaker::transferSpectrum(const char* schemaTmp, 
                                int spectraTmpID, 
                                int copies,
                                bool tmpHasAdditionalColumns)
{
    int newFileID = getNewFileId(schemaTmp, spectraTmpID);

    // find out if the source library has the same columns as the new
    const char* alternate_cols ="retentionTime, specIDinFile, score, scoreType";
    if( ! tmpHasAdditionalColumns ){
        alternate_cols = "'0', '0', '0', '0'";
    }

    sprintf(zSql,
            "INSERT INTO RefSpectra(peptideSeq, precursorMZ, "
            "precursorCharge, peptideModSeq, prevAA, nextAA, copies, numPeaks, "
            "fileID, retentionTime, specIDinFile, score, scoreType) "
            "SELECT peptideSeq, precursorMZ, precursorCharge, "
            "peptideModSeq, prevAA, nextAA, %d, numPeaks, %d, %s "
            "FROM %s.RefSpectra "
            "WHERE id = %d",
            copies, newFileID, alternate_cols, schemaTmp, spectraTmpID);
    sql_stmt(zSql);

    int spectraID = (int)sqlite3_last_insert_rowid(getDb());

    transferPeaks(schemaTmp, spectraID, spectraTmpID);
    transferModifications(schemaTmp, spectraID, spectraTmpID);
    return spectraID;
}

void BlibMaker::transferModifications(const char* schemaTmp, 
                                      int spectraID, 
                                      int spectraTmpID)
{
    sprintf(zSql,
            "SELECT RefSpectraID, position, mass "
            "FROM %s.Modifications "
            "WHERE RefSpectraID=%d", schemaTmp, spectraTmpID);
    smart_stmt pStmt;
    int rc = sqlite3_prepare(db, zSql, -1, &pStmt, 0);

    check_rc(rc, zSql, "Failed getting modifications.");

    rc = sqlite3_step(pStmt);

    while(rc==SQLITE_ROW) {
        sprintf(zSql,
                "INSERT INTO Modifications(RefSpectraID, position,mass) "
                "VALUES(%d, %d, %f)",
                spectraID,
                sqlite3_column_int(pStmt,1),
                sqlite3_column_double(pStmt,2));
        sql_stmt(zSql);
        
        rc = sqlite3_step(pStmt);
    }
}

void BlibMaker::transferPeaks(const char* schemaTmp, 
                              int spectraID, 
                              int spectraTmpID)
{
    typedef unsigned char Byte;

    sprintf(zSql,
            "SELECT RefSpectraID, peakMZ, peakIntensity "
            "FROM %s.RefSpectraPeaks "
            "WHERE RefSpectraID=%d", schemaTmp, spectraTmpID);
    smart_stmt pStmt;

    int rc = sqlite3_prepare(db, zSql, -1, &pStmt, 0);

    check_step(rc, pStmt, zSql, "Failed getting peaks.");

    int numBytes1=sqlite3_column_bytes(pStmt,1);
    Byte* comprM = (Byte*)sqlite3_column_blob(pStmt,1);
    int numBytes2=sqlite3_column_bytes(pStmt,2);
    Byte* comprI = (Byte*)sqlite3_column_blob(pStmt,2);

    sprintf(zSql, "INSERT INTO RefSpectraPeaks VALUES(%d,?,?)", spectraID);
    smart_stmt piStmt;
    rc = sqlite3_prepare(db, zSql, -1, &piStmt, 0);

    check_rc(rc, zSql, "Failed importing peaks.");

    sqlite3_bind_blob(piStmt, 1, comprM, numBytes1, SQLITE_STATIC);
    sqlite3_bind_blob(piStmt, 2, comprI, numBytes2, SQLITE_STATIC);

    rc = sqlite3_step(piStmt);

    if (rc != SQLITE_DONE)
        fail_sql(rc, zSql, NULL, "Failed importing peaks.");
}

void BlibMaker::insertPeaks(int spectraID, int levelCompress, int peaksCount, 
                            double* pM, float* pI)
{
    const uLong sizeM = (uLong) peaksCount*sizeof(double);
    const uLong sizeI = (uLong) peaksCount*sizeof(float);

    Byte *comprM, *comprI;
    uLong comprLenM, comprLenI;

    if (levelCompress == 0) {
        // No compression
        comprM = (Byte*)pM;
        comprLenM = sizeM;
        
        comprI = (Byte*)pI;
        comprLenI = sizeI;
    } else {
        // compress mz
        comprLenM = compressBound(sizeM);
        comprM = (Byte*)calloc((uInt)comprLenM, 1);
        int err = compress(comprM, &comprLenM, (const Bytef*)pM, sizeM);
        if (comprLenM >= sizeM) {
            // no mz compression
            free(comprM);
            comprM = (Byte*)pM;
            comprLenM = sizeM;
        }
        
        // compress intensity
        comprLenI = compressBound(sizeI);
        comprI = (Byte*)calloc((uInt)comprLenI, 1);
        err = compress(comprI, &comprLenI, (const Bytef*)pI, sizeI);
        if (comprLenI >= sizeI) {
            // no intensity compression
            free(comprI);
            comprI = (Byte*)pI;
            comprLenI = sizeI;
        }
    }
    
    sprintf(zSql, "INSERT INTO RefSpectraPeaks VALUES(%d, ?,?)", spectraID);
    
    smart_stmt pStmt;
    int rc = sqlite3_prepare(getDb(), zSql, -1, &pStmt, 0);
    
    check_rc(rc, zSql, "Failed importing peaks.");
    
    sqlite3_bind_blob(pStmt, 1, comprM, (int)comprLenM, SQLITE_STATIC);
    sqlite3_bind_blob(pStmt, 2, comprI, (int)comprLenI, SQLITE_STATIC);
    
    rc = sqlite3_step(pStmt);
    
    if (rc != SQLITE_DONE)
        fail_sql(rc, zSql, NULL, "Failed importing peaks.");
    
    if (comprLenM != sizeM)
        free(comprM);
    if (comprLenI != sizeI)
        free(comprI);
}

void BlibMaker::updateLibInfo()
{
    int dataRev;
    getNextRevision(&dataRev);
    int spectrum_count = countSpectra();

    sprintf(zSql, 
            "UPDATE LibInfo SET numSpecs=%d, majorVersion=%d",
            spectrum_count, dataRev);
    sql_stmt(zSql);
}

/**
 * Get the number of spectra in the given database name or in the
 * default database (the one least recently attached).  First tries to 
 * look up the value in the LibInfo table and if it is not available,
 * counts the spectra.
 * \returns The number of spectra in the database.
 */
int BlibMaker::getSpectrumCount(const char* databaseName /* = null */)
{
    // first try getting the count
    if (databaseName != NULL && *databaseName != '\0')
        sprintf(zSql, "SELECT numSpecs FROM %s.LibInfo", databaseName);
    else
        strcpy(zSql, "SELECT numSpecs FROM LibInfo");
    smart_stmt pStmt1;
    int rc1 = sqlite3_prepare(getDb(), zSql, -1, &pStmt1, 0);
    int numSpec = -1;

    if( !just_check_step(rc1, pStmt1, zSql, "") ){
        Verbosity::debug("Failed to get spectrum count, so count them.");
    } else {
        numSpec = sqlite3_column_int(pStmt1,0);// table init to -1
    }

    if( numSpec == -1 ){
        numSpec = countSpectra(databaseName);
    }

    return numSpec;
}

/**
 * Query the given database name or the default database (the one
 * least recently attached) for the number of spectra in the
 * RefSpectra table.  Does not check the value in LibInfo.
 * \returns The number of spectra in the database.
 */
int BlibMaker::countSpectra(const char* databaseName /* = NULL */)
{
    if (databaseName != NULL && *databaseName != '\0')
        sprintf(zSql, "SELECT count(*) FROM %s.RefSpectra", databaseName);
    else
        strcpy(zSql, "SELECT count(*) FROM RefSpectra");

    Verbosity::debug("About to submit count statement.");
    smart_stmt pStmt;
    int rc = sqlite3_prepare(getDb(), zSql, -1, &pStmt, 0);
    Verbosity::debug("Done counting.");
    check_step(rc, pStmt, zSql, "Failed getting spectrum count.");

    return sqlite3_column_int(pStmt,0);
}

void BlibMaker::getNextRevision(int* dataRev)
{
    int schemaVer;
    getRevisionInfo(NULL, dataRev, &schemaVer);

    // Increment revision by one.
    (*dataRev)++;
}

void BlibMaker::getRevisionInfo(const char* schemaName, int* dataRev, int* schemaVer)
{
    // Due to original omission of a schema version number, and the unused
    // integer values minorVersion, the minorVersion field has been taken
    // for use as a schemaVersion
    if (schemaName == NULL)
        strcpy(zSql, "SELECT majorVersion, minorVersion FROM LibInfo");
    else
        sprintf(zSql, "SELECT majorVersion, minorVersion FROM %s.LibInfo", 
                schemaName);

    int iRow, iCol;
    char** result;

    int rc = sqlite3_get_table(db, zSql, &result, &iRow, &iCol, 0);

    check_rc(rc, zSql, message.c_str());

    *dataRev = atoi(result[2]);
    *schemaVer = atoi(result[3]);

    sqlite3_free_table(result);
}

const char* BlibMaker::libIdFromName(const char* name)
{
    const char* slash = max(strrchr(name, '/'), strrchr(name, '\\'));
    if (slash == NULL)
        return name;

    return (slash + 1);
}

void BlibMaker::setLibName(const string& name)
{
    lib_name = name.c_str();
    lib_id = libIdFromName(name.c_str());
}

/**
 * Look at the return code and either die on failure with a detailed
 * message or return false on failure.
 */
bool BlibMaker::check_rc(int rc,             // return code
                         const char* stmt,   // statement attempted
                         const char* msg,    // additional message
                         bool die_on_failure // die or return
                         ) const
{
    if(rc != SQLITE_OK && die_on_failure)
        fail_sql(rc, stmt, NULL, msg);

    return (rc == SQLITE_OK);
}

/**
 * Process and check the statement.  If it was not processed
 * successfully, fail.
 */
void BlibMaker::check_step(int rc, sqlite3_stmt *pStmt, 
                           const char* stmt, const char* msg) const
{
    check_rc(rc, stmt, msg);
    if (sqlite3_step(pStmt) != SQLITE_ROW)
        fail_sql(rc, stmt, NULL, msg);
}

/**
 * Process and check the statement, but just return the value, don't
 * fail.
 */
bool BlibMaker::just_check_step(int rc, sqlite3_stmt *pStmt, 
                           const char* stmt, const char* msg) const
{
    check_rc(rc, stmt, msg);
    if (sqlite3_step(pStmt) != SQLITE_ROW)
        return false;
    else 
        return true;
}

/**
 * Execute the given SQL statement and check the return code.  If
 * ignoreFailure is true, return regardless of outcome.  Otherwise,
 * if an error was encountered, exit with a detailed message.  Use for
 * statements for which you do not need the results (i.e. not SELECT
 * statements). 
 */
void BlibMaker::sql_stmt(const char* stmt, bool ignoreFailure) const
{
    char *err = NULL;
    int rc = sqlite3_exec(db, stmt, 0, 0, &err);

    if (!ignoreFailure && rc != SQLITE_OK)
        fail_sql(rc, stmt, err);
}

/**
 * Exit after printing a detailed message about the return code, the
 * statment that generated the error and any additional message passed
 * by the caller.
 */
void BlibMaker::fail_sql(int rc,                // sqlite return code
                         const char* stmt,      // sql statement attempted
                         const char* err,       // sqlite generated error
                         const char* msg) const // additional message
{
    const char* first_msg = (msg == NULL? "SQL failure. " : msg);
    const char* sql_msg = (err == NULL? " " : err);

    Verbosity::error("%s %s [SQL statement '%s', return code %d]", 
                     first_msg, sql_msg, stmt, rc);
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
