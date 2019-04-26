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


/**
 * A class for manipulating the sqlite3 format for search results.
 */

#include "PsmFile.h"

namespace BiblioSpec {

/**
 * Create a PsmFile object and open a database to be stored as filename.
 *
 * Requires that filename end in .psm.  Will overwrite any existing
 * .psm file. Opens a sqlite3 database, creates necessary tables, and
 * sets the search ID.
 */
PsmFile::PsmFile(const char* filename,
                 const ops::variables_map& options_table)
: blibRunSearchID_(0),
    reportMatches_(options_table["report-matches"].as<int>())
{

    if( !hasExtension(filename, ".psm") ){
        Verbosity::error("Filename '%s' does not end with .psm.", filename);
    }

    sqlite3_open(filename, &db_);
    
    if(db_ == 0) {
        Verbosity::error("Couldn't open .psm results file %s.", filename);
    }
    SqliteRoutine::SQL_STMT("PRAGMA synchronous=OFF", db_);
    SqliteRoutine::SQL_STMT("PRAGMA cache_size=750000", db_);
    SqliteRoutine::SQL_STMT("PRAGMA temp_store=MEMORY", db_);

    createTables(options_table);

    SqliteRoutine::SQL_STMT("BEGIN", db_);

}


PsmFile::~PsmFile(){}

// NOTE these functions were taken as is from BlibSearch.cpp and have
// not been checked for accuracy.

/**
 * Helper function for constructor.  Creates all the new tables for a
 * psm file.  Requires that the database has been successfully opened.
 * Sets the _blibSearchID.
 */
void PsmFile::createTables(const ops::variables_map& options_table){

    SqliteRoutine::SQL_STMT("BEGIN", db_);

    char zSql[2048];

    Verbosity::status("Creating results tables.");

    //create msExperiment and msExperimentRun
    if(!SqliteRoutine::TABLE_EXISTS("msExperiment", db_)) {
        strcpy(zSql, "create table msExperiment"
               "(id INTEGER primary key autoincrement not null,"
               "serverAddress TEXT, serverDirectory TEXT, uploadDate TEXT, "
               "lastUpdate TEXT)");
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    }
    
    if(!SqliteRoutine::TABLE_EXISTS("msExperimentRun", db_)) {
        strcpy(zSql,"create table msExperimentRun(experimentID INTEGER, "
               "runID INTEGER, primary key(experimentID, runID))");
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    }
    
    //create msSearch table
    if(!SqliteRoutine::TABLE_EXISTS("msSearch", db_)) {
        strcpy(zSql, "create table msSearch(id INTEGER primary key autoincrement "
               "not null, "
               "experimentID INTEGER, expDate TEXT, serverDirectory TEXT, "
               "analysisProgramName TEXT, analysisProgramVersion TEXT, "
               "uploadDate TEXT)");
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    } else { 
        // delete existing results
        reorgDB();
        
    }

    //fill in the msSearch table
    //get path
    char szFile[1024];
    char* result = getcwd(szFile,1024);

    //get search time
    time_t t= time(NULL);
    char* date = ctime(&t);

    sprintf(zSql,"insert into msSearch(serverDirectory, analysisProgramName,"
            "analysisProgramVersion, uploadDate) "
            "values('%s','BlibSearch','1.0','%s')", result, date);
    SqliteRoutine::SQL_STMT(zSql, db_);
    zSql[0]='\0';

    int searchID = (int)sqlite3_last_insert_rowid(db_);

    //create msRunSearch table
    if(!SqliteRoutine::TABLE_EXISTS("msRunSearch", db_)) {
        strcpy(zSql, "create table msRunSearch(id INTEGER primary key "
               "autoincrement not null, "
               "runID INTEGER, searchID INTEGER, originalFileType TEXT, "
               "searchDate TEXT, "
               "searchDuration INTEGER, uploadDate TEXT)");
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0] = '\0';
    }
    
    //insert into msRunSearch
    sprintf(zSql, "insert into msRunSearch(runID,searchID, searchDate, "
            "uploadDate) values (1,%d,'%s','%s')",
            searchID,
            date,
            date);
    SqliteRoutine::SQL_STMT(zSql, db_);
    zSql[0]='\0';

    int runSearchID = (int)sqlite3_last_insert_rowid(db_);

    //create BiblioLibrary table
    if(!SqliteRoutine::TABLE_EXISTS("BiblioLibrary", db_)) {
        strcpy(zSql, "create table BiblioLibrary(id INTEGER primary key "
               "autoincrement not null,"
               "searchID INTEGER, name VARCHAR(255))");
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    } else {
        Verbosity::error("The BiblioLibrary table exists and it shouldn't.");
    }
    

    //insert into BiblioLibrary table
    vector<string> libfilenames = options_table["library"].as< vector<string> >();
    for(size_t i = 0; i < libfilenames.size(); i++) {
        string libname = getAbsoluteFilePath(libfilenames.at(i));
        sprintf(zSql, "insert into BiblioLibrary(searchID,name) values(%d,'%s')",
                searchID, libname.c_str());
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    }
    
    
    //create BiblioParams table
    if(!SqliteRoutine::TABLE_EXISTS("BiblioParmas", db_)) {
        strcpy(zSql, "create table BiblioParams(id INTEGER primary key "
               "autoincrement not null, "
               "searchID INTEGER,param VARCHAR(255),value REAL)");
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    } else {
        Verbosity::error("The BiblioParams table exists and it shouldn't.");
    }
    
    // replace with other options
    //fill in BiblioParams table
    ops::variables_map::const_iterator i;
    for(i = options_table.begin(); i != options_table.end(); ++i) {

        const ops::variable_value& v = i->second;
        if( ! v.empty() ) {
            const auto& type = v.value().type();
            if( type == typeid(int) ) {
                sprintf(zSql, "insert into BiblioParams(searchID, param, value) "
                        "values(%d,'%s',%d)",
                        searchID, (i->first).c_str(), v.as<int>());

            } else if( type == typeid(bool) ) {
                sprintf(zSql, "insert into BiblioParams(searchID, param, value) "
                        "values(%d,'%s',%d)",
                        searchID, (i->first).c_str(), (int)v.as<bool>());

            } else if( type == typeid(double) ) {
                sprintf(zSql, "insert into BiblioParams(searchID, param, value) "
                        "values(%d,'%s',%f)",
                        searchID, (i->first).c_str(), v.as<double>());

            }  // else don't use values of any other types

            SqliteRoutine::SQL_STMT(zSql, db_);
            zSql[0] = '\0';
            
        } // else don't insert empty values
    } // next value


    //create table BiblioSpecSpectrumData
    if(!SqliteRoutine::TABLE_EXISTS("BiblioSpecSpectrumData", db_)) {
        strcpy(zSql, "create table BiblioSpecSpectrumData (scanID INTERGER,"
               "runSearchID INTEGER, numLibraryMatches REAL, binSize REAL, "
               "shape REAL, scale REAL, fraction2fit REAL, "
               "weibullHistogram BLOB, primary key(scanID,runSearchID))");
        
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    } else {
        Verbosity::error( "Table BiblioSpecSpectrumData exists and shouldn't.");
    }
    
    
    //create table msRunSearchResult
    if(!SqliteRoutine::TABLE_EXISTS("msRunSearchResult", db_)) {
        strcpy(zSql, "create table msRunSearchResult(id INTEGER primary key "
               "autoincrement not null, "
               "runSearchID INTEGER, scanID INTEGER, charge INTEGER, "
               "peptide VARCHAR(255), preResidue CHAR(1), postResidue CHAR(1), "
               "validationStatus CHAR(1))");
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    }
    
    
    if(!SqliteRoutine::TABLE_EXISTS("BiblioSpecSearchResult", db_)) {
        strcpy(zSql, "create table BiblioSpecSearchResult(resultID INTEGER "
               "primary key, "
               "bsSpectrumID INTEGER, bslibraryID INTEGER, rank INTEGER, "
               "dotProductScore REAL, "
               "weibullPValue REAL, PeptideModSeq VARCHAR(255))");
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    } else {
        Verbosity::error("Table BiblioSpecSearchResult exists and shouldn't.");
    }
    
    SqliteRoutine::SQL_STMT("COMMIT", db_);
    
    blibRunSearchID_ = runSearchID;

}

/**
 * When the .psm file already exists, remove the existing search
 * results and replace with new tables.
 */
void PsmFile::reorgDB(){

    char zSql[2048];
    int rc,iRow, iCol;
    char** result;

    strcpy(zSql, 
           "select count(*) from msSearch where analysisProgramName='BlibSearch'");
    rc = sqlite3_get_table(db_, zSql, &result, &iRow, &iCol, 0);
    int count;

    if(rc == SQLITE_OK) {
        count = atoi(result[1]);
    } else {
        Verbosity::error("Can't get count from msSearch in reorgDB.");
    }
    
    zSql[0]='\0';

    int msSearchID=0;
    if(count>0) {
        Verbosity::warn("Deleting existing BlibSearch results from .psm file.");
        
        strcpy(zSql, 
               "select id from msSearch where analysisProgramName='BlibSearch'");
        rc = sqlite3_get_table(db_,zSql, &result, &iRow, &iCol,0);
        
        if(rc == SQLITE_OK) {
            msSearchID = atoi(result[1]);
        } else {
            Verbosity::error("Can't get msSearchID from msSearch in reorgDB.");
        }
        
    }
    
    zSql[0]='\0';
    if(msSearchID > 0) { //drop other tables 
        if(SqliteRoutine::TABLE_EXISTS("BiblioLibrary",db_)) {
            SqliteRoutine::SQL_STMT("drop table BiblioLibrary",db_);
        }
        
        if(SqliteRoutine::TABLE_EXISTS("BiblioParams",db_)) {
            SqliteRoutine::SQL_STMT("drop table BiblioParams", db_);
        }
        
        if(SqliteRoutine::TABLE_EXISTS("BiblioSpecSpectrumData",db_)) {
            SqliteRoutine::SQL_STMT("drop table BiblioSpecSpectrumData",db_);
        }
        
        if(SqliteRoutine::TABLE_EXISTS("BiblioSpecSearchResult",db_)) {
            SqliteRoutine::SQL_STMT("drop table BiblioSpecSearchResult",db_);
        }
        
        //needs to get msRunSearchID from msRunSearch table and then delete the record
        int msRunSearchID=0;
        if(SqliteRoutine::TABLE_EXISTS("msRunSearch",db_)) {
            sprintf(zSql, "select id from msRunSearch where searchID=%d",msSearchID);
            
            rc = sqlite3_get_table(db_, zSql, &result, &iRow, &iCol, 0);
            
            if(rc == SQLITE_OK) {
                msRunSearchID = atoi(result[1]);
            } else {
                cout<<"Can't execute the SQL statement="<<zSql<<endl;
                exit(1);
            }
            zSql[0]='\0';
            
            
            //delete the record
            sprintf(zSql, "delete from msRunSearch where searchID=%d",msSearchID);
            SqliteRoutine::SQL_STMT(zSql,db_);
            zSql[0]='\0';
        }
        
        //delete records from msRunSearchResult
        if(msRunSearchID > 0 && SqliteRoutine::TABLE_EXISTS("msRunSearchResult",db_)) {
            sprintf(zSql, "delete from msRunSearchResult where runSearchID=%d",msRunSearchID);
            SqliteRoutine::SQL_STMT(zSql,db_);
            zSql[0]='\0';
        }
    }
    //finally delete record from msSearch
    if(count>0) {
        strcpy(zSql, "delete from msSearch where analysisProgramName='BlibSearch'");
        SqliteRoutine::SQL_STMT(zSql,db_);
        zSql[0]='\0';
    }
    
    
}

void PsmFile::insertSpecData(Spectrum& s, 
                             const vector<Match>& matches, 
                             SearchLibrary& szLib)
{

    char zSql[2048];
    
    /*
    sprintf(zSql, 
            "insert into BiblioSpecSpectrumData values(%d,%d,%d,%f,%f,%f,%f,?)",
            s.getScanID(),
            blibRunSearchID_, 
            (int)matches.size(),
            0.01, //??
            szLib.getShape(),
            szLib.getScale(),
            szLib.getFraction2Fit());
    */

    sqlite3_stmt *pStmt;

    int rc = sqlite3_prepare(db_, zSql, -1, &pStmt, 0);
    if( rc!=SQLITE_OK ) {
        //        Verbosity::error("Can't insert spectrum %d into psm file.", 
        //                         s.getScanID());
    }

    //compress weibullHistogram
    int weibullHis[101];
    szLib.getWeibullHistogram(weibullHis,101);
    uLong len = (uLong)101*sizeof(int);
    //    uLong sizeM = len;
    uLong comprLenM = compressBound(len);
    Byte* comprM = (Byte*)calloc((uInt)comprLenM, 1);
    compress(comprM, &comprLenM, (const Bytef*)weibullHis, len);

    sqlite3_bind_blob(pStmt, 1, comprM, (int)comprLenM, SQLITE_STATIC);

    rc = sqlite3_step(pStmt);
    rc = sqlite3_finalize(pStmt);
    zSql[0]='\0';

    //clean up memory
    free(comprM);
}

void PsmFile::insertMatches(const vector<Match>& matches){

    // insert the result into tables msRunSearchResult, BiblioSpecSearchResult
    char zSql[2048];
    double pValue = -1;

    for(int i = 0; i < reportMatches_; i++) {
        Match tmpMatch = matches.at(i);
        const RefSpectrum* tmpRefSpec = tmpMatch.getRefSpec();
        //        const Spectrum* s = tmpMatch.getExpSpec();

       pValue = -1 * log(tmpMatch.getScore(BONF_PVAL));
        if(isinf( pValue ))
            pValue = 1000;

        sprintf(zSql,
                "insert into msRunSearchResult(runSearchID,scanID,charge,"
                "peptide,preResidue, postResidue, validationStatus) "
                "values(%d,%d,%d,'%s','%s','%s','=')",
                blibRunSearchID_,
                0,//                s->getScanID(),
                0,//tmpRefSpec->getCharge(),
                (tmpRefSpec->getSeq()).c_str(),
                (tmpRefSpec->getPrevAA()).c_str(),
                (tmpRefSpec->getNextAA()).c_str());
        SqliteRoutine::SQL_STMT(zSql, db_);
        
        zSql[0]='\0';
        
        int resultID=(int)sqlite3_last_insert_rowid(db_);
        sprintf(zSql, "insert into BiblioSpecSearchResult "
                "values(%d,%d,%d,%d,%f,%f,'%s')",
                resultID,
                tmpRefSpec->getLibSpecID(),
                tmpMatch.getMatchLibID(),
                i+1,
                tmpMatch.getScore(DOTP),
                pValue,
                tmpRefSpec->getMods().c_str());
        SqliteRoutine::SQL_STMT(zSql, db_);
        zSql[0]='\0';
    } // next match

}


void PsmFile::commit(){
    SqliteRoutine::SQL_STMT("COMMIT", db_);
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
