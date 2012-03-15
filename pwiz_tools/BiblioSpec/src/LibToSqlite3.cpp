/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
/**
 *@author: Ning Zhang
 *@date: Oct. 2008
 *@purpose: To change current library format. Re-write the binary format
 *          into sqlite3 format.
 */


#include <string.h>
#include <sqlite3.h>
#include <algorithm>
#include <string>
#include <iostream>
#include <sstream>
#include <vector>
#include <cstdio>
#include <cstdlib>
/*
  #include "Library.h"
  #include "../Spectrum/Spectrum.h"
  #include "../Spectrum/RefSpectrum.h"
  #include "../RefFile/Modifications.h"
*/
#include "original-Library.h"
#include "original-Spectrum.h"
#include "original-RefSpectrum.h"
#include "original-Modifications.h"
#include "zlib.h"
#include <iomanip>
#include <map>

using namespace std;

void sql_stmt(sqlite3* db, const char* stmt);
string getPeptideModSeq(string pepSeq, string modString, map<int,double>& specMods);
void add2Table(RefSpectrum* tmpSpec, sqlite3* db);

int main(int argc, char* argv[])
{
// TODO: add option for redundant/not redundant
    if(argc < 3) {
        cerr << "Usage: LibToSqlite3 <old version lib> <new lib name>" <<endl;
        exit(1);
    }
    
    string oldName = argv[1];
    string newName = argv[2];
/*
    string libName = argv[1];
    string partLibName;
    
    //first parse the string in order to get output db name
    size_t found = libName.find_last_of('.');
    size_t found_slash = libName.find_last_of('/');
    int len;
    if(found_slash != string::npos) {
        len=found-found_slash-1;
        partLibName = libName.substr(found_slash+1, len);
    } else
        partLibName=libName.substr(0,found);
    
    
    string outLibName(partLibName);
*/    
    
    //Library thisLib(argv[1],2);
    Library oldLib(oldName.c_str(), 2);
    //outLibName += "."+thisLib.getVersion_str()+".blib";
    
    //LIBHEAD_T header = thisLib.getHeader();
    LIBHEAD_T header = oldLib.getHeader();
    
    //LibIterator allSpec = thisLib.getAllSpec();
    LibIterator allSpec = oldLib.getAllSpec();
    
    vector<RefSpectrum*> libSpecs;
    
    while( allSpec.hasSpec() ) {
        RefSpectrum* tmpSpec = allSpec.getSpecp();
        libSpecs.push_back(tmpSpec);
    }

    sort( libSpecs.begin(), libSpecs.end(), compRefSpecPtrId());

    // CONSIDER: Use BlibMaker instead?
    // Now all the RefSpecs are in the vector,create database
    // and create two tables

    // for meta_table
    time_t t= time(NULL);
    char* date = ctime(&t);

    char blibLSID[2048];
    //bool redundant = false;
    bool redundant = true;
    // TODO: Need a way to specify the library type
    const char* libType = (redundant ? "redundant" : "nr");
    //sprintf(blibLSID,"urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:%s:%s",libType,partLibName.c_str());
    sprintf(blibLSID,"urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:%s:%s",libType,newName.c_str());


    sqlite3* db;
    //sqlite3_open(outLibName.c_str(), &db);
    sqlite3_open(newName.c_str(), &db);

    if (db == 0) {
        printf("Could not open database.");
        return 1;
    }
    
    sql_stmt(db,"PRAGMA cache_size=750000");
    sql_stmt(db,"PRAGMA synchronous=OFF");
    sql_stmt(db,"PRAGMA temp_store=MEMORY");
    
    char zSql[1024];
    strcpy(zSql, "CREATE TABLE LibInfo(libLSID TEXT, createTime TEXT, numSpecs INTEGER, majorVersion INTEGER, minorVersion INTEGER)");
    sql_stmt(db,zSql);
    zSql[0]='\0';

    sprintf(zSql, "INSERT INTO LibInfo values('%s','%s',%d,%d,%d)",
            blibLSID,
            date,
            header.numSpec,
            header.specVersion,
            header.annotVersion);
    sql_stmt(db,zSql);


    const char* stmt1=
        "CREATE TABLE RefSpectra (id INTEGER primary key autoincrement not null, "
        "peptideSeq VARCHAR(150),"
        "precursorMZ REAL,"
        "precursorCharge INTEGER,"
        "peptideModSeq VARCHAR(200), "
        "prevAA CHAR(1), "
        "nextAA CHAR(1), "
        "copies INTEGER, "
        "numPeaks INTEGER)";

    sql_stmt(db, stmt1);

    strcpy(zSql,"CREATE TABLE RefSpectraPeaks(RefSpectraID INTEGER, peakMZ BLOB, peakIntensity BLOB)");
    sql_stmt(db, zSql);
    zSql[0]='\0';



    //a modification table
    const char* stmt =
        "CREATE TABLE Modifications ("
        "id INTEGER primary key autoincrement not null,"
        "RefSpectraID INTEGER, "
        "position INTEGER, "
        "mass REAL)";
    sql_stmt(db,stmt);


    sql_stmt(db, "BEGIN");

    for(int i=0; i<(int)libSpecs.size(); i++) {
        if (i > 0) {
            if(i % 1000 == 0) {
                cout<<i<<" ";
                cout.flush();
            }
            
            if(i % 10000 == 0) {
                cout<<"\n";
                cout.flush();
            }
        }
        
        RefSpectrum* tmpSpec = libSpecs.at(i);
        add2Table(tmpSpec,db);
    }
    
    strcpy(zSql, "CREATE INDEX idxPeptide ON RefSpectra (peptideSeq, precursorCharge)");
    sql_stmt(db, zSql);

    strcpy(zSql, "CREATE INDEX idxPeptideMod ON RefSpectra (peptideModSeq, precursorCharge)");
    sql_stmt(db, zSql);

    strcpy(zSql, "CREATE INDEX idxRefIdPeaks ON RefSpectraPeaks (RefSpectraID)");
    sql_stmt(db,zSql);

    sql_stmt(db, "COMMIT");

    return 0;
}

void add2Table(RefSpectrum* tmpSpec, sqlite3* db)
{
    //now get each RefSpec information and fill in the table
    int libID;
    string pepSeq;
    string modString;
    int copies;
    float preMZ;
    int preChg;
    string pepModSeq;
    map<int, double> specMods;
    vector<PEAK_T> peaks;

    libID = tmpSpec->getID();
    pepSeq = tmpSpec->getSeq();


    modString = tmpSpec->getMods();
    copies = tmpSpec -> getCopies();
    preMZ = tmpSpec -> getMz();
    preChg = tmpSpec -> getCharge();

    //cout<<"libID="<<libID<<" pepSeq="<<pepSeq<<"modString="<<modString<<endl;
    pepModSeq = getPeptideModSeq(pepSeq, modString,specMods);



    peaks = tmpSpec->getPeaks();

    int j;

    //file compression
    int err;
    uLong len;
    Byte *comprM, *comprI;
    uLong comprLenM, comprLenI;
    double *pD;
    float *pF;
    uLong sizeM;
    uLong sizeI;

    //Build arrays to hold scan prior to compression

    pD = new double[peaks.size()];
    pF = new float[peaks.size()];
    for(j=0;j<(int)peaks.size();j++) {
        pD[j]=(double)(peaks.at(j).mass);
        pF[j]=peaks.at(j).intensity;
    }

    //compress mz
    len = (uLong)peaks.size()*sizeof(double);
    sizeM = len;
    comprLenM = compressBound(len);
    comprM = (Byte*)calloc((uInt)comprLenM, 1);
    err = compress(comprM, &comprLenM, (const Bytef*)pD, len);
    if (comprLenM >= sizeM) {
        // no mz compression
        free(comprM);
        comprM = (Byte*)pD;
        comprLenM = sizeM;
    }

    //compress intensity
    len = (uLong)peaks.size()*sizeof(float);
    sizeI = len;
    comprLenI = compressBound(len);
    comprI = (Byte*)calloc((uInt)comprLenI, 1);
    err = compress(comprI, &comprLenI, (const Bytef*)pF, len);
    if (comprLenI >= sizeI) {
        // no intensity compression
        free(comprI);
        comprI = (Byte*)pF;
        comprLenI = sizeI;
    }


    char zSql[8192];

    sprintf(zSql,
            "INSERT INTO RefSpectra(peptideSeq,precursorMZ,precursorCharge, "
            "peptideModSeq,prevAA, nextAA, copies,numPeaks) "
            "VALUES ('%s', %.2f, %d,'%s','-','-', %d,%d)",
            pepSeq.c_str(),
            preMZ,
            preChg,
            pepModSeq.c_str(),
            copies,
            (int)peaks.size());

    sql_stmt(db,zSql);
    zSql[0]='\0';

    int spectraID = (int)sqlite3_last_insert_rowid(db);



    sprintf(zSql, "INSERT INTO RefSpectraPeaks VALUES(%d,?,?)", spectraID);


    //cout<<zSql<<endl;
    sqlite3_stmt *pStmt;
    int rc;

    rc = sqlite3_prepare(db, zSql, -1, &pStmt, 0);
    if( rc!=SQLITE_OK ) {
        cout<<"can't prepare SQL statement!"<<rc<<endl;
        exit(1);
    }


    sqlite3_bind_blob(pStmt, 1, comprM, (int)comprLenM, SQLITE_STATIC);
    sqlite3_bind_blob(pStmt, 2, comprI, (int)comprLenI, SQLITE_STATIC);

    rc = sqlite3_step(pStmt);
    rc = sqlite3_finalize(pStmt);

    //clean up memory
    if (comprLenM != sizeM)
        free(comprM);
    if (comprLenI != sizeI)
        free(comprI);
    delete [] pD;
    delete [] pF;

    //insert into modifications
    map<int,double>::iterator it;
    for(it=specMods.begin(); it != specMods.end(); it++) {
        sprintf(zSql,
                "INSERT INTO Modifications(RefSpectraID, position,mass) "
                "VALUES(%d,%d,%.1f)",
                spectraID,
                (*it).first,
                (*it).second);
        sql_stmt(db,zSql);
        zSql[0]='\0';
    }
    
}


string getPeptideModSeq(string pepSeq, string modString, map<int,double>& specMods)
{
    string returnSeq("");
    for(int i=0; i<(int)modString.size(); i++) {
        char modChar = modString.at(i);
        returnSeq += pepSeq.at(i);
        if(modChar != '0' || pepSeq.at(i) == 'C') { //check if a C +57 mod. 
            float mass = Modifications::getMass(modChar);
            
            specMods[i+1] = (double)mass;
            
            char sign = Modifications::getSign(modChar);
            
            stringstream ss;
            ss<<"["<<sign<<fixed<<setprecision(1)<<mass<<"]";
            returnSeq += ss.str();
        }
    }
    
    return returnSeq;
}

void sql_stmt(sqlite3* db, const char* stmt)
{
    char *errmsg;
    int   ret;

    ret = sqlite3_exec(db, stmt, 0, 0, &errmsg);

    if (ret != SQLITE_OK)
        {
            printf("Error in statement: %s [%s].\n", stmt, errmsg);
        }
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
