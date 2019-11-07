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

//class definition for LibReader.h

#include "LibReader.h"
#include <map>


namespace BiblioSpec {

LibReader::LibReader() :
    expLowMZ_(0.0),
    expHighMZ_(0.0),
    expPreChg_(-1),
    expLowChg_(-1),
    expHighChg_(-1),
    totalCount_(-1),
    curSpecId_(1),
    maxSpecId_(0),
    modPrecision_(-1)
{
}

LibReader::LibReader(const char* libName, int modPrecision) :
    expLowMZ_(0.0),
    expHighMZ_(0.0),
    expPreChg_(-1),
    expLowChg_(-1),
    expHighChg_(-1),
    totalCount_(-1),
    curSpecId_(1),
    maxSpecId_(0),
    modPrecision_(modPrecision)
{
    strcpy(libraryName_, libName);
    initialize();
}


LibReader::~LibReader() {sqlite3_close(db_);}

void LibReader::initialize()
{
    sqlite3_open(libraryName_, &db_);

    if (db_ == 0) {
        Verbosity::error("Could not open database %s." , libraryName_);
    }

    setMaxLibId();
}

void LibReader::setMaxLibId(){

    sqlite3_stmt* statement;
    int resultCode = sqlite3_prepare(db_, 
                                     "SELECT max(id) FROM RefSpectra",
                                     -1, // read to nul term
                                     &statement, 
                                     NULL); // statement tail null
    if(resultCode != SQLITE_OK) {
        string dbMsg = sqlite3_errmsg(db_);
        Verbosity::debug("SQLITE error message: %s", dbMsg.c_str() );
        Verbosity::error("LibReader::setMaxLibId cannot prepare "
                         "SQL statement for finding maxLibId from %s (%s)",
                         libraryName_, dbMsg.c_str());
    }

    if( sqlite3_step(statement) != SQLITE_ROW ){
        Verbosity::error("Couldn't find the max specId for %s.", libraryName_);
    }

    maxSpecId_ = sqlite3_column_int(statement, 0);

    Verbosity::debug("Highest lib spec ID is %d.", maxSpecId_);
}
/** 
 * Select from the library all RefSpectra with precursor m/z between
 * minMz and maxMz, inclusive.  Get spectra of all charge states.
 * Only add spec with the at least the minimum number of peaks. Adds
 * to the given vector of spectra.  
 * \Returns The number of spectra added.
 */
int LibReader::getSpecInMzRange(double minMz, 
                                double maxMz,
                                int minPeaks,
                                vector<RefSpectrum*>& returnedSpectra ){
    char sqlStmtBuffer[1024];
    sprintf(sqlStmtBuffer,
            "SELECT id, peptideSeq, precursorMZ, precursorCharge, "
            "peptideModSeq, copies, numPeaks, peakMZ, "
            "peakIntensity FROM RefSpectra, RefSpectraPeaks "
            "WHERE precursorMZ >= %f and precursorMZ <= %f "
            "AND numPeaks > %d "
            "AND id = RefSpectraId",
            minMz, maxMz, minPeaks);

    sqlite3_stmt* statement;
    int resultCode = sqlite3_prepare(db_, sqlStmtBuffer, -1, // read to nul term
                                  &statement, NULL); // statement tail null

    if(resultCode != SQLITE_OK) {
        Verbosity::debug("SQLITE error message: %s", sqlite3_errmsg(db_) );
        Verbosity::error("LibReader::getSpecInMzRange(vector) cannot prepare "
                         "SQL select statement for fetching "
                         "spectra (m/z %.3f-%.3f) from %s", 
                         minMz, maxMz, libraryName_);
    }

    // turn each row of returned table into a spectrum
    int numSpec = 0;
    resultCode = sqlite3_step(statement);
    while( resultCode == SQLITE_ROW ){

        RefSpectrum* tmpSpec = new RefSpectrum();
        tmpSpec->setLibSpecID(sqlite3_column_int(statement,0));
        tmpSpec->setSeq(reinterpret_cast<const char*>(sqlite3_column_text(statement, 1)));
        tmpSpec->setMz(sqlite3_column_double(statement, 2));
        tmpSpec->setCharge(sqlite3_column_int(statement, 3));
        tmpSpec->setMods(reinterpret_cast<const char*>(sqlite3_column_text(statement, 4)));
        tmpSpec->setCopies(sqlite3_column_int(statement, 5));

        int numPeaks = sqlite3_column_int(statement, 6);
        int numBytes1 = sqlite3_column_bytes(statement, 7);
        Byte* comprM = (Byte*)sqlite3_column_blob(statement, 7);
        int numBytes2 = sqlite3_column_bytes(statement, 8);
        Byte* comprI = (Byte*)sqlite3_column_blob(statement, 8);

        tmpSpec->setRawPeaks(getUncompressedPeaks(numPeaks, numBytes1, comprM, 
                                                 numBytes2, comprI));

        returnedSpectra.push_back(tmpSpec);

        resultCode = sqlite3_step(statement);
        numSpec++;
    } // next row

    resultCode = sqlite3_finalize(statement);

    return numSpec;
}

/** 
 * Select from the library all RefSpectra with precursor m/z between
 * minMz and maxMz, inclusive.  Get spectra of all charge states.
 * Only add spec with at least minPeaks. Adds to the given deque of
 * spectra.  
 * \Returns The number of spectra added.
 */
int LibReader::getSpecInMzRange(double minMz, 
                                double maxMz,
                                int minPeaks,
                                deque<RefSpectrum*>& returnedSpectra ){
    char sqlStmtBuffer[1024];
    sprintf(sqlStmtBuffer,
            "SELECT id, peptideSeq, precursorMZ, precursorCharge, "
            "peptideModSeq, copies, numPeaks, peakMZ, "
            "peakIntensity FROM RefSpectra, RefSpectraPeaks "
            "WHERE precursorMZ > %f and precursorMZ <= %f "
            "AND numPeaks > %d "
            "AND id = RefSpectraId",
            minMz, maxMz, minPeaks);
    // NOTE: it's not faster to sort here (in the select statement) than
    // to sort the cache after new spec are added

    sqlite3_stmt* statement;
    int resultCode = sqlite3_prepare(db_, sqlStmtBuffer, -1, // read to nul term
                                  &statement, NULL); // statement tail null

    if(resultCode != SQLITE_OK) {
        Verbosity::debug("SQLITE error message: %s", sqlite3_errmsg(db_) );
        Verbosity::error("LibReader::getSpecInMzRange(deque) cannot prepare "
                         "SQL select statement for fetching "
                         "spectra (m/z %.3f-%.3f) from %s", 
                         minMz, maxMz, libraryName_);
    }

    // turn each row of returned table into a spectrum
    int numSpec = 0;
    resultCode = sqlite3_step(statement);
    while( resultCode == SQLITE_ROW ){

        RefSpectrum* tmpSpec = new RefSpectrum();
        tmpSpec->setLibSpecID(sqlite3_column_int(statement,0));
        tmpSpec->setSeq(reinterpret_cast<const char*>(sqlite3_column_text(statement, 1)));
        tmpSpec->setMz(sqlite3_column_double(statement, 2));
        tmpSpec->setCharge(sqlite3_column_int(statement, 3));
        tmpSpec->setMods(reinterpret_cast<const char*>(sqlite3_column_text(statement, 4)));
        tmpSpec->setCopies(sqlite3_column_int(statement, 5));

        int numPeaks = sqlite3_column_int(statement, 6);
        int numBytes1 = sqlite3_column_bytes(statement, 7);
        Byte* comprM = (Byte*)sqlite3_column_blob(statement, 7);
        int numBytes2 = sqlite3_column_bytes(statement, 8);
        Byte* comprI = (Byte*)sqlite3_column_blob(statement, 8);

        tmpSpec->setRawPeaks(getUncompressedPeaks(numPeaks, numBytes1, comprM, 
                                                 numBytes2, comprI));

        Verbosity::comment(V_DETAIL, "Adding spectrum %d, precursor %.2f.", 
                           tmpSpec->getLibSpecID(), tmpSpec->getMz());
        returnedSpectra.push_back(tmpSpec);

        resultCode = sqlite3_step(statement);
        numSpec++;
    } // next row

    resultCode = sqlite3_finalize(statement);

    return numSpec;
}
/*
void LibReader::setLibName(const char* libName)
{
    strcpy(libraryName, libName);
    initialize();
}
*/

void LibReader::setLowMZ(double lowMZ)
{
    expLowMZ_ = lowMZ;
}

void LibReader::setHighMZ(double highMZ)
{
    expHighMZ_ = highMZ;
}

void LibReader::setCharge(int chg)
{
    expPreChg_ = chg;
}

void LibReader::setLowChg(int lowChg)
{
    expLowChg_ = lowChg;
}

void LibReader::setHighChg(int highChg)
{
    expHighChg_ = highChg;
}

double LibReader::getLowMZ()
{
    return expLowMZ_;
}

double LibReader::getHighMZ()
{
    return expHighMZ_;
}

int LibReader::getCharge()
{
    return expPreChg_;
}

int LibReader::getLowChg()
{
    return expLowChg_;
}

int LibReader::getHighChg()
{
    return expHighChg_;
}

int LibReader::countAllSpec()
{
    const char* szSqlStmt = "Select count(*) from RefSpectra";
    
    int iRow, iCol;
    char** result;
    
    int success = sqlite3_get_table(db_, szSqlStmt, &result, &iRow, &iCol, 0);

    if(success != SQLITE_OK) {
        Verbosity::error("Can't execute SQL statement to count all spectra");
    }

    int totalCount = atoi(result[1]);

    return totalCount;
}
/*
int LibReader::getTotalCount()
{
    char szSqlStmt[8192];

    sprintf(szSqlStmt, "select count(*) from RefSpectra where precursorMZ >= %f and precursorMZ <= %f",
            expLowMZ,
            expHighMZ);

    if(expPreChg != -1) {
        sprintf(szSqlStmt+strlen(szSqlStmt), " and precursorCharge = %d", expPreChg);
    } else {
        sprintf(szSqlStmt+strlen(szSqlStmt), " and precursorCharge >= %d and precursorCharge <= %d", expLowChg, expHighChg);
    }
    
    int rc,iRow, iCol;
    char** result;


    rc = sqlite3_get_table(db, szSqlStmt, &result, &iRow, &iCol, 0);

    if(rc == SQLITE_OK) {
        totalCount = atoi(result[1]);
    } else {
        cout<<"Can't execute the SQL statement"<<szSqlStmt<<endl;
        
    }

    szSqlStmt[0]='\0';
    return totalCount;
}
*/

//call RefSpectrum destructor
RefSpectrum LibReader::getRefSpec(int libID)
{
    char szSqlStmt[8192];
    sprintf(szSqlStmt, "select id, peptideSeq,precursorMZ, precursorCharge,"
            "peptideModSeq,prevAA, nextAA, copies, numPeaks, peakMZ, peakIntensity, retentionTime "
            "from RefSpectra, RefSpectraPeaks where id=%d AND id=RefSpectraID", libID);

    RefSpectrum tmpRef;

    sqlite3_stmt *pStmt;
    int rc;

    rc = sqlite3_prepare(db_, szSqlStmt, -1, &pStmt, 0);

    if( rc != SQLITE_OK ) {
        Verbosity::error("Cannot prepare SQL statement for selecting spectrum "
                         "with ID number %d.", libID);
    }

    rc = sqlite3_step(pStmt);
    if( rc==SQLITE_ROW ) {

        tmpRef.setLibSpecID(sqlite3_column_int(pStmt,0));
        tmpRef.setSeq(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,1)));
        tmpRef.setMz(sqlite3_column_double(pStmt,2));
        tmpRef.setCharge(sqlite3_column_int(pStmt,3));
        tmpRef.setMods(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,4)));
        tmpRef.setPrevAA(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,5)));
        tmpRef.setNextAA(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,6)));
        tmpRef.setCopies(sqlite3_column_int(pStmt,7));
        int numPeaks = sqlite3_column_int(pStmt,8);

        int numBytes1=sqlite3_column_bytes(pStmt,9);
        Byte* comprM = (Byte*)sqlite3_column_blob(pStmt,9);
        int numBytes2=sqlite3_column_bytes(pStmt,10);
        Byte* comprI = (Byte*)sqlite3_column_blob(pStmt,10);

        tmpRef.setRetentionTime(sqlite3_column_double(pStmt, 11));

        tmpRef.setRawPeaks(getUncompressedPeaks(numPeaks, numBytes1,comprM, numBytes2,comprI));

        //tmpRef->printMe();
        //rc = sqlite3_step(pStmt);

    }

    rc = sqlite3_finalize(pStmt);

    szSqlStmt[0]='\0';
    return tmpRef;
}

bool LibReader::getRefSpec(int libID, RefSpectrum& spec)
{
    char szSqlStmt[8192];
    sprintf(szSqlStmt, "SELECT id, peptideSeq,precursorMZ, precursorCharge,"
            "peptideModSeq,prevAA, nextAA, copies, numPeaks, peakMZ, "
            "peakIntensity, retentionTime "
            "FROM RefSpectra, RefSpectraPeaks where id=%d "
            "AND id=RefSpectraID", libID);

    sqlite3_stmt *pStmt;
    int rc = sqlite3_prepare(db_, szSqlStmt, -1, &pStmt, 0);

    if( rc != SQLITE_OK ) {
        Verbosity::debug("SQLITE error message: %s", sqlite3_errmsg(db_) );
        Verbosity::warn("LibReader::getRefSpec cannot prepare SQL statement "
                           "for selecting spectrum %d from %s.",
                           libID, libraryName_);
    }

    rc = sqlite3_step(pStmt);
    if( rc==SQLITE_ROW ) {

        spec.setLibSpecID(sqlite3_column_int(pStmt,0));
        spec.setSeq(reinterpret_cast<const char*>(sqlite3_column_text(pStmt, 1)));
        spec.setMz(sqlite3_column_double(pStmt,2));
        spec.setCharge(sqlite3_column_int(pStmt,3));
        spec.setMods(reinterpret_cast<const char*>(sqlite3_column_text(pStmt, 4)));
        spec.setPrevAA("-");
        spec.setNextAA("-");
        spec.setCopies(sqlite3_column_int(pStmt,7));
        int numPeaks = sqlite3_column_int(pStmt,8);

        int numBytes1=sqlite3_column_bytes(pStmt,9);
        Byte* comprM = (Byte*)sqlite3_column_blob(pStmt,9);
        int numBytes2=sqlite3_column_bytes(pStmt,10);
        Byte* comprI = (Byte*)sqlite3_column_blob(pStmt,10);

        spec.setRetentionTime(sqlite3_column_double(pStmt, 11));

        spec.setRawPeaks(getUncompressedPeaks(numPeaks, numBytes1, comprM, numBytes2, comprI));

    } else {
        Verbosity::debug("SQLITE error message: %s", sqlite3_errmsg(db_) );
        Verbosity::debug("LibReader::getRefSpec cannot find "
                           "spectrum %d in %s.", libID, libraryName_);
        return false;
    }

    sqlite3_finalize(pStmt);

    if (modPrecision_ >= 0) {
        sprintf(szSqlStmt, "SELECT position, mass FROM Modifications WHERE RefSpectraId = %d", libID);
        sqlite3_stmt* modStmt;
        rc = sqlite3_prepare(db_, szSqlStmt, -1, &modStmt, NULL);
        if (rc != SQLITE_OK) {
            Verbosity::error("Cannot prepare SQL statement: %s ", sqlite3_errmsg(db_));
        }
        string seq = spec.getSeq();
        map<int, double> mods;
        while (sqlite3_step(modStmt) == SQLITE_ROW) {
            int position = sqlite3_column_int(modStmt, 0);
            double mass = sqlite3_column_double(modStmt, 1);
            if (position > seq.length()) {
                position = seq.length();
            }
            map<int, double>::iterator j = mods.find(position);
            if (j == mods.end()) {
                mods[position] = mass;
            } else {
                j->second += mass;
            }
        }
        sqlite3_finalize(modStmt);

        char modBuf[256];
        for (map<int, double>::const_reverse_iterator j = mods.rbegin(); j != mods.rend(); j++) {
            sprintf(modBuf, "[%s%.*f]", j->second >= 0 ? "+" : "", modPrecision_, j->second);
            seq.insert(j->first, modBuf);
        }
        spec.setMods(seq.c_str());
    }

    return true;
}

vector<PEAK_T> LibReader::getUncompressedPeaks(int& numPeaks, 
                                               int& mzLen, 
                                               Byte* comprM, 
                                               int& intensityLen, 
                                               Byte* comprI)
{
    vector<PEAK_T> peaks;

    int i;
    PEAK_T p;

    //variables for compressed files
    uLong uncomprLenM, uncomprLenI;
    double *mz;
    float *intensity;

    uncomprLenM = numPeaks*sizeof(double);
    if ((int)uncomprLenM == mzLen)
        mz = (double*) comprM;
    else {
        mz = new double[numPeaks];
        uncompress((Bytef*)mz, &uncomprLenM, comprM, mzLen);
    }
    
    uncomprLenI=numPeaks*sizeof(float);
    if ((int)uncomprLenI == intensityLen)
        intensity = (float*) comprI;
    else {
        intensity = new float[numPeaks];
        uncompress((Bytef*)intensity, &uncomprLenI, comprI, intensityLen);
    }
    
    for(i=0;i<numPeaks;i++) {
        p.mz = mz[i];
        p.intensity = intensity[i];
        peaks.push_back(p);
    }


    if ((int)uncomprLenM != mzLen)
        delete [] mz;
    if ((int)uncomprLenI != intensityLen)
        delete [] intensity;

    return peaks;

}


vector<RefSpectrum> LibReader::getRefSpecsInRange(int lowLibID, int highLibID)
{

    char szSqlStmt[8192];
    sprintf(szSqlStmt, "select id, peptideSeq, precursorMZ,precursorCharge, "
            "peptideModSeq, prevAA, nextAA, copies, numPeaks, peakMZ, peakIntensity, retentionTime "
            "from RefSpectra,RefSpectraPeaks where "
            "id >= %d and id <=%d AND id=RefSpectraID",
            lowLibID,
            highLibID);

    vector<RefSpectrum> specs;

    sqlite3_stmt *pStmt;
    int rc;

    rc = sqlite3_prepare(db_, szSqlStmt, -1, &pStmt, 0);

    if( rc!=SQLITE_OK ) {
        Verbosity::error("Cannot prepare SQL statement for getting spectra "
                         "of IDs %d-%d from library %s.",
                         lowLibID, highLibID, libraryName_ );
    }

    rc = sqlite3_step(pStmt);
    while( rc==SQLITE_ROW ) {

        RefSpectrum tmpRef;
        tmpRef.setLibSpecID(sqlite3_column_int(pStmt,0));
        tmpRef.setSeq(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,1)));
        tmpRef.setMz(sqlite3_column_double(pStmt,2));
        tmpRef.setCharge(sqlite3_column_int(pStmt,3));
        tmpRef.setMods(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,4)));
        tmpRef.setPrevAA(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,5)));
        tmpRef.setNextAA(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,6)));
        tmpRef.setCopies(sqlite3_column_int(pStmt,7));
        int numPeaks = sqlite3_column_int(pStmt,8);

        int numBytes1=sqlite3_column_bytes(pStmt,9);
        Byte* comprM = (Byte*)sqlite3_column_blob(pStmt,9);
        int numBytes2=sqlite3_column_bytes(pStmt,10);
        Byte* comprI = (Byte*)sqlite3_column_blob(pStmt,10);

        tmpRef.setRetentionTime(sqlite3_column_double(pStmt, 11));

        tmpRef.setRawPeaks(getUncompressedPeaks(numPeaks, numBytes1,comprM, numBytes2,comprI));
        specs.push_back(tmpRef);

        rc = sqlite3_step(pStmt);

    }


    rc = sqlite3_finalize(pStmt);
    szSqlStmt[0]='\0';
    return specs;
}

/**
 * Select library spectra from the open file based on the min and max
 * precursor m/z and charge state limits.  Allocate a RefSpectrum for
 * each and return via the vector argument.  Does not remove any
 * spectra already in the vector.  Caller is responsible for freeing the
 * spectra.
 *
 * \returns The number of spectra added to the vector.
 */
int LibReader::getAllRefSpec(vector<RefSpectrum*>& specs)
{
    // write the select statement here
    char szSqlStmt[8192];

    sprintf(szSqlStmt, 
            "select id, peptideSeq, precursorMZ, precursorCharge, "
            "peptideModSeq, prevAA, nextAA, copies, numPeaks, peakMZ, "
            "peakIntensity, retentionTime "
            "from RefSpectra, RefSpectraPeaks "
            "where precursorMZ >= %f and precursorMZ <= %f",
            expLowMZ_,
            expHighMZ_);

    if(expPreChg_ != -1) {
        sprintf(szSqlStmt+strlen(szSqlStmt), 
                " and precursorCharge = %d", expPreChg_);
    } else {
        sprintf(szSqlStmt+strlen(szSqlStmt), 
                " and precursorCharge >= %d and precursorCharge <= %d", 
                expLowChg_, expHighChg_);
    }
    
    sprintf(szSqlStmt+strlen(szSqlStmt), " AND id=RefSpectraID");
    //cerr << "szSqlStmt=" << szSqlStmt << endl;

    sqlite3_stmt *pStmt;
    int resultCode = sqlite3_prepare(db_, szSqlStmt, -1, &pStmt, 0);

    if( resultCode != SQLITE_OK ) {
        Verbosity::error("Cannot prepare SQT select statement for fetching "
                         "library spectra from %s.", libraryName_);
    }

    resultCode = sqlite3_step(pStmt);
    int numSpec = 0;
    while( resultCode == SQLITE_ROW ) {

        RefSpectrum* tmpRef = new RefSpectrum;
        tmpRef->setLibSpecID(sqlite3_column_int(pStmt,0));
        tmpRef->setSeq(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,
                                                                         1)));
        tmpRef->setMz(sqlite3_column_double(pStmt,2));
        tmpRef->setCharge(sqlite3_column_int(pStmt,3));
        tmpRef->setMods(reinterpret_cast<const char*>(sqlite3_column_text(pStmt,
                                                                         4)));
        tmpRef->setPrevAA("-");
        tmpRef->setNextAA("-");
        tmpRef->setCopies(sqlite3_column_int(pStmt,7));

        int numPeaks = sqlite3_column_int(pStmt,8);

        int numBytes1 = sqlite3_column_bytes(pStmt,9);
        Byte* comprM = (Byte*)sqlite3_column_blob(pStmt,9);
        int numBytes2 = sqlite3_column_bytes(pStmt,10);
        Byte* comprI = (Byte*)sqlite3_column_blob(pStmt,10);

        tmpRef->setRetentionTime(sqlite3_column_double(pStmt, 11));

        tmpRef->setRawPeaks(getUncompressedPeaks(numPeaks, numBytes1, comprM, 
                                              numBytes2, comprI));

        specs.push_back(tmpRef);

        resultCode = sqlite3_step(pStmt);

        numSpec++;
    }// next spec


    resultCode = sqlite3_finalize(pStmt);
    szSqlStmt[0]='\0';

    return numSpec;
}

/**
 * When called repeatedly, returns all spectra in the library in order
 * by LibId.  Spectrum returned via spec argument.  Returns true if
 * there was a spectrum to return, false if all have been fetched
 * already.
 */
bool LibReader::getNextSpectrum(RefSpectrum& spec){
    // keep trying spec ids until we find one or get to the end
    while( curSpecId_ <= maxSpecId_  && !getRefSpec(curSpecId_, spec) ){
        Verbosity::debug("Skipping spectrum %d.", curSpecId_);
        curSpecId_++;
    }

    if( curSpecId_ > maxSpecId_ ){
        Verbosity::debug("Returned the last spec from the library.");
        return false;
    }
    curSpecId_++;
    
    return true;
}


} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
