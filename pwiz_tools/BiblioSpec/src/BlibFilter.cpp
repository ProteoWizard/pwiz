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

/*
 * This programs takes a redundant library and
 * compute all_vs_all, select one final spec into
 * the non_redundant library.
 */

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <cstdio>
#include <cstdlib>
#include <sqlite3.h>
#include <time.h>
#include "zlib.h"
#include "RefSpectrum.h"
#include "PeakProcess.h"
#include "DotProduct.h"
#include "Match.h"
#include "BlibMaker.h"
#include "ProgressIndicator.h"
#include "BlibUtils.h"
#include "Verbosity.h"
#include "CommandLine.h"
#include "SqliteRoutine.h"
#include "boost/program_options.hpp"
#include "boost/log/detail/snprintf.hpp"

namespace ops = boost::program_options;

namespace BiblioSpec {

class BlibFilter : public BlibMaker {
 public:
    BlibFilter();
    ~BlibFilter();
    void parseCommandLine(const int argc, char** const argv,
                          ops::variables_map& options_table);
    void init();
    virtual void commit();
    virtual void attachAll();
    void buildNonRedundantLib();

 protected:
    virtual string getLSID();
    virtual void getNextRevision(int* major, int* minor);
    vector<PEAK_T> getUncompressedPeaks(int& numPeaks,
                                        int& mzLen, Byte* comprM, 
                                        int& intensityLen, Byte* comprI);
    void compAndInsert(vector<RefSpectrum*>& oneIon, vector<pair<int, int>>& bestSpectraIdAndCount);
    map< int, vector<RefSpectrum*> > groupByScoreType(const vector<RefSpectrum*>& oneIon, map<RefSpectrum*, int>* outIndices);
    vector<RefSpectrum*> getBestScores(const vector<RefSpectrum*>& group, bool higherIsBetter);

 private:
    string redundantFileName_;  // The name of the file being filtered
    // filtered lib name stored by BlibMaker
    const char* redundantDbName_; // The name it's given as an attached db
    int minPeaks_;        // Spectrum must have this many peaks to be included
    double minAverageScore_; // don't include best spec if average dotp is lower

    int tableVersion_;
    bool useBestScoring_;
    map<int, bool> higherIsBetter_;
    char zSql[ZSQLBUFLEN];
    sqlite3_stmt* insertStmt_;

    void getCommandLineValues(ops::variables_map& options_table);
};
} // namespace

using namespace BiblioSpec;

/**
 * The starting point for BlibFilter, program to select one spectrum
 * for each peptide ion as the best representative of a redundant
 * library and store it in a new library.
 */
int main(int argc, char* argv[]) {
    bnw::args utf8ArgWrapper(argc, argv); // modifies argv in-place with UTF-8 version on Windows
    pwiz::util::enable_utf8_path_operations();

    // declare storage for options values
    ops::variables_map options_table;

    BlibFilter filter;
    filter.parseCommandLine(argc, argv, options_table);

    try {
        filter.init();

        filter.beginTransaction();
        Verbosity::debug("About to begin filtering.");
        filter.buildNonRedundantLib();
        Verbosity::debug("Finished filtering.");
        filter.endTransaction();
        filter.commit();
    } catch (BlibException& e) {
        cerr << "ERROR: " << e.what();
    } catch (exception& e) {
        cerr << "ERROR: " << e.what();
    } catch (...) {
        cerr << "ERROR: Unknown error";
    }
}

BlibFilter::BlibFilter() {
    redundantDbName_ = "redundant";
    minPeaks_ = 20; 
    minAverageScore_ = 0;
    tableVersion_ = 0;
    useBestScoring_ = false;
    insertStmt_ = NULL;
    // Never append to a non-redundant library
    setOverwrite(true);
    setRedundant(false);
}

BlibFilter::~BlibFilter() {
    sqlite3_finalize(insertStmt_);
}

/**
 * Read the given command line and store option and argument values in
 * the given variables_map.  Also reads parameter file with command
 * line values overriding file values.  Sets appropriate values for
 * the BlibFilter object (filenames, options).
 *
 * Exits on error if unexpected option found, if option is missing its
 * argument, if required argument is missing.
 */
void BlibFilter::parseCommandLine(const int argc, 
                                  char** const argv,
                                  ops::variables_map& options_table){

    // define the optional command line args 
    options_description optionsDescription("Options");
    try{

        optionsDescription.add_options()
            ("memory-cache,m",
             value<int>()->default_value(250),
             "SQLite memory cache size in Megs.  Default 250M.")

            ("min-peaks,n",
             value<int>()->default_value(1),
             "Only include spectra with at least this many peaks.  Default 1.")

            ("min-score,s",
             value<double>()->default_value(0),
             "Best spectrum must have at least this average score to be included.  Default 0.")

            ("best-scoring,b",
             value<bool>()->default_value(false),
             "Description of option.  Default false.")

            ;

        // define the required command line args
        vector<const char*> argNames;
        argNames.push_back("redundant-library");
        argNames.push_back("filtered-library");

        // create a CommandLine object to do the dirty work
        BiblioSpec::CommandLine parser("BlibFilter", 
                                       optionsDescription,
                                       argNames, 
                                       false);// no multiple values
                                              // for last arg

        // read command line and param file (if given)
        parser.parse(argc, argv, options_table);

    } catch(std::exception& e) {
        cerr << "ERROR: " << e.what() << "." << endl << endl;
        exit(1);
    } catch(...) {
        BiblioSpec::Verbosity::error("Encountered exception of unknown type "
                                     "while parsing command line.");
    }

    getCommandLineValues(options_table);
}

/**
 * Save the needed values parsed from the command line.
 */
void BlibFilter::getCommandLineValues(ops::variables_map& options_table){
    redundantFileName_ = options_table["redundant-library"].as<string>();
    minPeaks_ = options_table["min-peaks"].as<int>();
    minAverageScore_ = options_table["min-score"].as<double>();
    setLibName(options_table["filtered-library"].as<string>());
    useBestScoring_ = options_table["best-scoring"].as<bool>();
}


void BlibFilter::attachAll()
{
    Verbosity::status("Filtering redundant library '%s'.",
                      redundantFileName_.c_str());
    boost::log::aux::snprintf(zSql, ZSQLBUFLEN, "ATTACH DATABASE '%s' as %s", SqliteRoutine::ESCAPE_APOSTROPHES(redundantFileName_).c_str(),
            redundantDbName_);
    sql_stmt(zSql);

    createUpdatedRefSpectraView(redundantDbName_);
}

void BlibFilter::commit()
{
    BlibMaker::commit();
    
    string detachCmd = "DETACH DATABASE ";
    detachCmd += redundantDbName_;
    sql_stmt(detachCmd.c_str());
}

string BlibFilter::getLSID()
{
    // Use the same LSID as the redundant version, but replace
    // 'redundant' with 'nr'.
    boost::log::aux::snprintf(zSql, ZSQLBUFLEN, "SELECT libLSID FROM %s.LibInfo", redundantDbName_);
    
    int iRow, iCol;
    char** result;
    int rc = sqlite3_get_table(getDb(), zSql, &result, &iRow, &iCol, 0);
    
    check_rc(rc, zSql);
    
    string libLSID = result[1];
    const string redundant = ":redundant:";
    size_t idx = libLSID.find(redundant);
    if (idx == string::npos)
    {
        throw BlibException(false, "The library %s does not appear to be a redundant library.\n"
                            "'%s' was not found in LSID '%s'.",
                            redundantFileName_.c_str(), redundant.c_str(), libLSID.c_str());
    }

    libLSID.replace(idx, redundant.length(), ":nr:");
    
    sqlite3_free_table(result);
    return libLSID;
}

void BlibFilter::getNextRevision(int* major, int* minor)
{
    // Use same revision as the redundant version
   boost::log::aux::snprintf(zSql, ZSQLBUFLEN,   "SELECT majorVersion, minorVersion FROM %s.LibInfo", 
            redundantDbName_);
    
    int iRow, iCol;
    char** result;
    int rc = sqlite3_get_table(getDb(), zSql, &result, &iRow, &iCol, 0);
    
    check_rc(rc, zSql);  // does this also check that there is at least one row returned?
    
    *major = atoi(result[2]);
    *minor = atoi(result[3]);
    
    sqlite3_free_table(result);
}

/**
 *  Initialize a new filtered library.  First call the parent classes
 *  init method which opens a new file (taking care of overwriting) and
 *  creates standard tables.  Here add additional tables required for
 *  filtered libraries: PeptideIons and RetentionTimes.
 */
void BlibFilter::init(){
    BlibMaker::init();
    strcpy(zSql,
           "CREATE TABLE RetentionTimes (RefSpectraID INTEGER, "
           "RedundantRefSpectraID INTEGER, "
           "SpectrumSourceID INTEGER, "
           "ionMobility REAL, "
           "collisionalCrossSectionSqA REAL, "
           "ionMobilityHighEnergyOffset REAL, "
           "ionMobilityType TINYINT, "
           "retentionTime REAL, "
           "startTime REAL, "
           "endTime REAL, "
           "score REAL, "
           "bestSpectrum INTEGER, " // boolean
           "FOREIGN KEY(RefSpectraID) REFERENCES RefSpectra(id) )" );
    sql_stmt(zSql);

    if (sqlite3_prepare(getDb(),
        "INSERT INTO RetentionTimes (RefSpectraID, RedundantRefSpectraID, "
        "SpectrumSourceID, ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, "
        "retentionTime, startTime, endTime, score, bestSpectrum) "
        "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        -1, &insertStmt_, NULL) != SQLITE_OK) {
        throw BlibException(false, "Error preparing insert statement: %s", sqlite3_errmsg(getDb()));
    }
}

void BlibFilter::buildNonRedundantLib() {
    Verbosity::debug("Starting buildNonRedundant.");

    if (useBestScoring_) {
        sqlite3_stmt* scoreStmt;
        if (sqlite3_prepare(getDb(), "SELECT id, scoreType FROM ScoreTypes", -1, &scoreStmt, 0) != SQLITE_OK)
            Verbosity::error("Failed to prepare statement to get scores types");
        
        sqlite3_stmt* checkForScore;
        sprintf(zSql, "SELECT EXISTS(SELECT 1 FROM %s.RefSpectra WHERE scoreType = ?)", redundantDbName_);
        if (sqlite3_prepare(getDb(), zSql, -1, &checkForScore, 0) != SQLITE_OK)
            Verbosity::error("Failed to prepare statement to check for score type");

        while (sqlite3_step(scoreStmt) == SQLITE_ROW) {
            int scoreTypeId = sqlite3_column_int(scoreStmt, 0);
            string scoreType = (const char*)sqlite3_column_text(scoreStmt, 1);
            if (scoreType == "PERCOLATOR QVALUE" ||
                scoreType == "IDPICKER FDR" ||
                scoreType == "MASCOT IONS SCORE" ||
                scoreType == "TANDEM EXPECTATION VALUE" ||
                scoreType == "OMSSA EXPECTATION SCORE" ||
                scoreType == "PROTEIN PROSPECTOR EXPECTATION SCORE" ||
                scoreType == "SEQUEST XCORR" ||  // This is actually the associated qvalue, not the raw xcorr
                scoreType == "MAXQUANT SCORE" ||
                scoreType == "MORPHEUS SCORE" ||
                scoreType == "MSGF+ SCORE" ||
                scoreType == "PEAKS CONFIDENCE SCORE" ||
                scoreType == "BYONIC SCORE" ||
                scoreType == "GENERIC Q-VALUE") {
                higherIsBetter_[scoreTypeId] = false;
            } else if (
                scoreType == "SPECTRUM MILL" || // not actually a probablilty value
                scoreType == "WATERS MSE PEPTIDE SCORE" || // not actually a probablilty value
                scoreType == "PEPTIDE PROPHET SOMETHING" ||
                scoreType == "PROTEIN PILOT CONFIDENCE" ||
                scoreType == "SCAFFOLD SOMETHING" ||
                scoreType == "PEPTIDE SHAKER CONFIDENCE") {
                higherIsBetter_[scoreTypeId] = true;
            } else {
                Verbosity::warn("Don't know if higher or lower is better: %s", scoreType.c_str());
                sqlite3_bind_int(checkForScore, 1, scoreTypeId);
                if (sqlite3_step(checkForScore) != SQLITE_ROW)
                    Verbosity::error("Failed to execute statement to check for score type");
                if (sqlite3_column_int(checkForScore, 0) == 1) {
                    // Cannot filter by score if we don't know whether a higher/lower score is better
                    // Revert to normal behavior
                    Verbosity::warn("Cannot filter by score, reverting to normal behavior");
                    useBestScoring_ = false;
                    break;
                }
                sqlite3_reset(checkForScore);
            }
        }
        sqlite3_finalize(scoreStmt);
        sqlite3_finalize(checkForScore);
    }

    string msg = "ERROR: Failed building library ";
    msg += getLibName();
    setMessage(msg.c_str());

    // first copy over all of the spectrum source files
    transferSpectrumFiles(redundantDbName_);
    // copy over all of the proteins
    transferProteins(redundantDbName_);

    // find out if we have retention times and other additional columns
    tableVersion_ = 0;
    string optional_cols;
    string order_by;
    if (tableColumnExists(redundantDbName_, "RefSpectra", "retentionTime")) {
        ++tableVersion_;
        optional_cols = ", SpecIDinFile, retentionTime";
        if (tableColumnExists(redundantDbName_, "RefSpectra", "collisionalCrossSectionSqA")) {
            if (tableColumnExists(redundantDbName_, "RefSpectra", "ionMobilityHighEnergyOffset")) {
                tableVersion_ = MIN_VERSION_IMS_UNITS;
                optional_cols += ", ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset";
                if (tableColumnExists(redundantDbName_, "RefSpectra", "startTime")) {
                    tableVersion_ = MIN_VERSION_RT_BOUNDS;
                    optional_cols += ", startTime, endTime";
                    if (tableExists(redundantDbName_, "Proteins")) {
                        tableVersion_ = MIN_VERSION_PROTEINS;
                        if (tableColumnExists(redundantDbName_, "RefSpectra", "totalIonCurrent")) {
                            tableVersion_ = MIN_VERSION_TIC;
                            optional_cols += ", totalIonCurrent";
                        }
                    }
                } else if (tableExists(redundantDbName_, "RefSpectraPeakAnnotations")) {
                    tableVersion_ = MIN_VERSION_PEAK_ANNOT;
                }
            } else {
                tableVersion_ = MIN_VERSION_CCS;
                optional_cols += ", driftTimeMsec, collisionalCrossSectionSqA, driftTimeHighEnergyOffsetMsec";
            }
            if (tableColumnExists(redundantDbName_, "RefSpectra", "inchiKey")) {
                // May contain small molecules
                order_by = "peptideModSeq, moleculeName, chemicalFormula, inchiKey, otherKeys, precursorCharge, precursorAdduct" + optional_cols;
                optional_cols += SmallMolMetadata::sql_col_names_csv();
                if (tableVersion_ >= MIN_VERSION_IMS_UNITS)
                    optional_cols += ", ionMobilityType";
                else
                    tableVersion_ = MIN_VERSION_SMALL_MOL;
            }
        } else if (tableColumnExists(redundantDbName_, "RefSpectra", "ionMobilityValue")) {
            ++tableVersion_;
            optional_cols += ", ionMobilityValue, ionMobilityType";
            if (tableColumnExists(redundantDbName_, "RefSpectra", "ionMobilityHighEnergyDriftTimeOffsetMsec")) {
                ++tableVersion_;
                optional_cols += ", ionMobilityHighEnergyDriftTimeOffsetMsec";
            }
        }
    }

    vector<RefSpectrum*> oneIon;
    std::string lastPepModSeq, pepModSeq;

    int lastCharge=0;

    Verbosity::debug("Counting Spectra.");
    ProgressIndicator progress(getSpectrumCount(redundantDbName_));

    Verbosity::debug("Sorting spectra by sequence and charge.");
    if (order_by.empty())
    {
        //first Order by peptideModSeq and charge, filter by num peaks
        order_by = "peptideModSeq, precursorCharge " + optional_cols;
    }
   boost::log::aux::snprintf(zSql, ZSQLBUFLEN,  
            "SELECT id,peptideSeq,precursorMZ,precursorCharge,peptideModSeq,"
            "prevAA, nextAA, numPeaks, score, scoreType, "
            "ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, "
            "moleculeName, chemicalFormula, inchiKey, otherKeys, precursorAdduct, "
            "startTime, endTime, totalIonCurrent, retentionTime, specIDinFile "
            "FROM RefSpectraTransfer "
            "WHERE numPeaks >= %i "
            "ORDER BY %s", minPeaks_,
            order_by.c_str());

    smart_stmt pStmt;
    int rc = sqlite3_prepare(getDb(), zSql, -1, &pStmt, 0);

    check_rc(rc, zSql, 
             "Failed selecting redundant spectra for comparison.");
    Verbosity::debug("Successfully sorted.");

    rc = sqlite3_step(pStmt);
    // Construct a column dictionary for easier backward compatibilty
    std::map<std::string, int> columns;
    int num_fields = sqlite3_column_count(pStmt);
    for (int index = 0; index < num_fields; index++)
    {
        columns[sqlite3_column_name(pStmt, index)] = index;
    }
    int molNameIndex = columns["moleculeName"];
    int formulaIndex = columns["chemicalFormula"];
    int adductIndex = columns["precursorAdduct"];
    int inchiKeyIndex = columns["inchiKey"];
    int otherKeysIndex = columns["otherKeys"];
    int ccsIndex =  columns["collisionalCrossSectionSqA"];
    int ionMobilityTypeIndex = columns["ionMobilityType"];
    int ionMobilityValueIndex = columns["ionMobilityValue"]; // V3 and earlier
    int ionMobilityIndex = columns["ionMobility"];
    int highEnergyOffsetIndex = columns["ionMobilityHighEnergyOffset"];
    int scoreIndex = columns["score"];
    int scoreTypeIndex = columns["scoreType"];
    int scanNumberIndex = columns["SpecIDinFile"];
    int retentionTimeIndex = columns["retentionTime"];
    int startTimeIndex = columns["startTime"];
    int endTimeIndex = columns["endTime"];
    int ticIndex = columns["totalIonCurrent"];
    int numPeaksIndex = columns["numPeaks"];

    // setup for getting peak data
    sqlite3* peakConnection;
    int peakRc = sqlite3_open(redundantFileName_.c_str(), &peakConnection);
    if (peakRc != SQLITE_OK) {
        Verbosity::error("Could not open connection to database '%s'", redundantFileName_.c_str());
    }
    sqlite3_stmt* peakStmt;
    char zSqlPeakQuery[ZSQLBUFLEN];
    strncpy(zSqlPeakQuery,
           "SELECT peakMZ, peakIntensity "
           "FROM RefSpectraPeaks "
           "WHERE RefSpectraId = ",
           ZSQLBUFLEN);
    int qlen = strlen(zSqlPeakQuery);
    char* idPos = zSqlPeakQuery + qlen;

    // for best scoring mode, keep track of best scoring id and its corresponding count, so these spectra and their peaks can be transferred in bulk
    vector<pair<int, int>> bestSpectraIdAndCount;

    // for each spectrum entry in table
    while( rc==SQLITE_ROW ) {
        progress.increment();
        
        // create a RefSpectrum object and populate all fields
        // then get charge and seq
        // TODO: RefSpectrum* tmpRef = nextRefSpec(pStmt);

        RefSpectrum* tmpRef = new RefSpectrum();

        pepModSeq = reinterpret_cast<const char*>(sqlite3_column_text(pStmt,4));
        int charge = sqlite3_column_int(pStmt,3);
        
        tmpRef->setLibSpecID(sqlite3_column_int(pStmt,0));
        tmpRef->setSeq(reinterpret_cast<const char*>(sqlite3_column_text(pStmt, 1)));
        tmpRef->setMz(sqlite3_column_double(pStmt,2));
        tmpRef->setCharge(charge);
        // if not selected, value == 0
        if (ionMobilityValueIndex > 0) { // Records drift time or ccs but not both
            double ionMobilityValue = ionMobilityValueIndex > 0 ? sqlite3_column_double(pStmt, ionMobilityValueIndex) : 0;
            int ionMobilityType = ionMobilityTypeIndex > 0 ? sqlite3_column_int(pStmt, ionMobilityTypeIndex) : 0;
            tmpRef->setIonMobility(ionMobilityType == 1 ? ionMobilityValue : 0, ionMobilityType == 1 ? IONMOBILITY_DRIFTTIME_MSEC : IONMOBILITY_NONE);
            tmpRef->setCollisionalCrossSection(ionMobilityType == 2 ? ionMobilityValue : 0);
        } else if (ionMobilityIndex > 0) {
            double ionMobilityValue = sqlite3_column_double(pStmt, ionMobilityIndex);
            IONMOBILITY_TYPE ionMobilityType = (ionMobilityTypeIndex > 0) ? (IONMOBILITY_TYPE)sqlite3_column_int(pStmt, ionMobilityTypeIndex) : IONMOBILITY_DRIFTTIME_MSEC;
            tmpRef->setIonMobility(ionMobilityValue, ionMobilityType);
            tmpRef->setCollisionalCrossSection(sqlite3_column_double(pStmt, ccsIndex));
            if (molNameIndex > 0) {
                // moleculeName, chemicalFormula, precursorAdduct, inchiKey, otherKeys
                tmpRef->setMoleculeName(reinterpret_cast<const char*>(sqlite3_column_text(pStmt, molNameIndex)));
                tmpRef->setChemicalFormula(reinterpret_cast<const char*>(sqlite3_column_text(pStmt, formulaIndex)));
                tmpRef->setPrecursorAdduct(reinterpret_cast<const char*>(sqlite3_column_text(pStmt, adductIndex)));
                tmpRef->setInchiKey(reinterpret_cast<const char*>(sqlite3_column_text(pStmt, inchiKeyIndex)));
                tmpRef->setotherKeys(reinterpret_cast<const char*>(sqlite3_column_text(pStmt, otherKeysIndex)));
            }
        } else {
            tmpRef->setIonMobility(0, IONMOBILITY_NONE);
            tmpRef->setCollisionalCrossSection(0);
        }

        if (startTimeIndex > 0 && endTimeIndex > 0) {
            tmpRef->setStartTime(sqlite3_column_double(pStmt, startTimeIndex));
            tmpRef->setEndTime(sqlite3_column_double(pStmt, endTimeIndex));
        }

        if (ticIndex > 0)
            tmpRef->setTotalIonCurrentRaw(sqlite3_column_double(pStmt, ticIndex));

        tmpRef->setIonMobilityHighEnergyOffset(highEnergyOffsetIndex > 0 ? sqlite3_column_double(pStmt, highEnergyOffsetIndex) : 0);
        tmpRef->setRetentionTime(sqlite3_column_double(pStmt, retentionTimeIndex));
        tmpRef->setMods(pepModSeq.c_str());
        tmpRef->setPrevAA("-");
        tmpRef->setNextAA("-");
        tmpRef->setScore(scoreIndex > 0 ? sqlite3_column_double(pStmt, scoreIndex) : 0);
        tmpRef->setScoreType(scoreTypeIndex > 0 ? sqlite3_column_int(pStmt, scoreTypeIndex) : 0);
        tmpRef->setScanNumber(scanNumberIndex>0 ? sqlite3_column_int(pStmt, scanNumberIndex) : 0); // NB this isn't necessarily an integer value column, but nobody seems to care

        // If this is a small molecule, note that for change-check purposes
        string smallMoleculeIonID;
        tmpRef->getSmallMoleculeIonID(smallMoleculeIonID); 
        pepModSeq += smallMoleculeIonID; // One or the other of pepModSeq or smallMoleculeIonID will be empty

        int numPeaks = sqlite3_column_int(pStmt, numPeaksIndex);

        // get peaks for this spectrum
        if (!useBestScoring_) // peaks not necessary in best scoring mode
        {
            int refSpectraId = sqlite3_column_int(pStmt, 0);
            boost::log::aux::snprintf(idPos, ZSQLBUFLEN - qlen, "%i", refSpectraId);
            peakRc = sqlite3_prepare(peakConnection, zSqlPeakQuery, -1, &peakStmt, NULL);
            check_rc(peakRc, zSqlPeakQuery, "Failed selecting peaks.");
            peakRc = sqlite3_step(peakStmt);
            if (peakRc != SQLITE_ROW) {
                Verbosity::error("Did not find peaks for spectrum %d.", refSpectraId);
            }
            int numBytes1 = sqlite3_column_bytes(peakStmt, 0);
            Byte* comprM = (Byte*)sqlite3_column_blob(peakStmt, 0);
            int numBytes2 = sqlite3_column_bytes(peakStmt, 1);
            Byte* comprI = (Byte*)sqlite3_column_blob(peakStmt, 1);

            // is this slow for copying the peak vector? better to return a ptr?
            vector<PEAK_T> peaks = getUncompressedPeaks(numPeaks, numBytes1, comprM, numBytes2, comprI);
            sqlite3_finalize(peakStmt);
            if (peaks.empty()) {
                Verbosity::error("Unable to read peaks for redundant library "
                    "spectrum %i, sequence %s, charge %i.",
                    tmpRef->getLibSpecID(), (tmpRef->getSeq()).c_str(),
                    tmpRef->getCharge());
            }
            tmpRef->setRawPeaks(peaks);
        }
        // TODO end nextRefSpec

        // if this spec has same seq and charge, add to the collection
        if(pepModSeq.compare(lastPepModSeq) == 0 && lastCharge == charge) {
            oneIon.push_back(tmpRef);
        } else {// filter & start new collection for a different seq and charge
            if(!oneIon.empty()) {
                Verbosity::comment(V_DETAIL, "Selecting spec for %s, charge %i from %i spectra.",
                                   lastPepModSeq.c_str(), lastCharge, oneIon.size());
                compAndInsert(oneIon, bestSpectraIdAndCount);
                clearVector(oneIon);
            }

            oneIon.push_back(tmpRef);
            lastPepModSeq = pepModSeq;
            lastCharge = charge;
            Verbosity::comment(V_DETAIL, "Collecting spec for %s, charge %i,",
                               pepModSeq.c_str(), charge);
        }

        rc = sqlite3_step(pStmt);
    }// next table entry

    // Insert the last spectrum
    if (!oneIon.empty()) {
        progress.increment();
        Verbosity::comment(V_DETAIL, "Selecting spec for %s, charge %i from %i spectra.",
                           lastPepModSeq.c_str(), lastCharge, oneIon.size());
        compAndInsert(oneIon, bestSpectraIdAndCount);
        clearVector(oneIon);
    }

    if (useBestScoring_)
        transferSpectra(redundantDbName_, bestSpectraIdAndCount, tableVersion_);

    // we may have selected fewer spectra than were in the library
    // update the progress indicator
    progress.finish();
}

vector<PEAK_T> BlibFilter::getUncompressedPeaks(int& numPeaks,
                                                int& mzLen, Byte* comprM, 
                                                int& intensityLen, Byte* comprI)
{
    //variables for compressed files
    uLong uncomprLen;
    double *mz;
    float *intensity;

    uncomprLen=numPeaks*sizeof(double);
    if ((int)uncomprLen == mzLen)
        mz = (double*) comprM;
    else {
        mz = new double[numPeaks];
        uncompress((Bytef*)mz, &uncomprLen, comprM, mzLen);
    }

    uncomprLen=numPeaks*sizeof(float);
    if ((int)uncomprLen == intensityLen)
        intensity = (float*) comprI;
    else {
        intensity = new float[numPeaks];
        uncompress((Bytef*)intensity, &uncomprLen, comprI, intensityLen);
    }
    
    vector<PEAK_T> peaks(numPeaks);
    PEAK_T p;
    for(int i=0;i<numPeaks;i++) {
        p.mz = mz[i];
        p.intensity = intensity[i];
        // Check for corruption
        if (p.mz != p.mz || p.mz < 0 || p.mz > 100000 ||
            p.intensity != p.intensity || p.intensity < 0) {
            // Corrupted peaks
            peaks.clear();
            return peaks;
        }
        peaks[i] = p;
    }

    if (mz != (double*)comprM)
        delete [] mz;
    if (intensity != (float*)comprI)
        delete [] intensity;

    return peaks;
}

/**
 * Given a vector containing RefSpectrum for the same sequence and
 * charge, find the best representative and insert into the current table.
 * The "best representative" is currently defined as the spectrum that
 * has the highest average dot product when compared to all other
 * spectra.  
 *
 * When the collection contains exactly one spectrum, add it.  When the
 * spectrum contains exactly two spectra, the average dot product will
 * be the same for both so use a different criterion to choose.
 * Eventually, when a quailty-of-match score (e.g. p-value) is stored,
 * use the spec with the higher score.  For now, use the one with more
 * peaks. 
 */
void BlibFilter::compAndInsert(vector<RefSpectrum*>& oneIon, vector<pair<int, int>>& bestSpectraIdAndCount)
{
    int num_spec = oneIon.size();
    int specID = 0; // id RefSpec table in filtered index
    int bestIndex = 0;

    if(!useBestScoring_) { // choose the one with more peaks

        if (num_spec == 1) { // add that one spectrum
            specID = transferSpectrum(redundantDbName_,
                oneIon.at(0)->getLibSpecID(),
                num_spec,
                tableVersion_);
        } else if (num_spec == 2) {
            // in the future, pick the one with the best search score
            if( oneIon.at(0)->getNumRawPeaks() < oneIon.at(1)->getNumRawPeaks() ) {
                bestIndex = 1;
            }
            // cerr << "selecting index " << bestIndex << ", scan " << oneIon.at(bestIndex)->getScanNumber() << " from " << oneIon.at(0)->getScanNumber() << " and " << oneIon.at(1)->getScanNumber() << endl;

            specID = transferSpectrum(redundantDbName_, 
                                      oneIon.at(bestIndex)->getLibSpecID(), 
                                      num_spec,
                                      tableVersion_);
        } else { // compute all-by-all dot-products

                // preprocess all RefSpectrum in oneIon
                PeakProcessor proc;
                proc.setClearPrecursor(true);
                proc.setNumTopPeaksToUse(100);
                // TODO (BF Aug-12-09): all processing should be controlled by
                // parameters available to the user
            
                RefSpectrum* tmpRef;
                for(int i=0; i<(int)oneIon.size(); i++) {
                    tmpRef=oneIon.at(i);
                    proc.processPeaks(tmpRef);
                }
            
                // create an array where we'll sum scores for each spectrum
                // initialize to 0
                vector<double> scores(oneIon.size(), 0);

                // for each spectrum
                for(int i=0; i<(int)oneIon.size(); i++) {
                    RefSpectrum* tmpRef1 = oneIon.at(i);

                    // compare to all subsequent spectrum
                    for(int j=i+1; j<(int)oneIon.size(); j++) {

                        RefSpectrum* tmpRef2 = oneIon.at(j);
                        Match thisMatch(tmpRef1, tmpRef2);
                        DotProduct::compare(thisMatch);
                        double dotProduct = thisMatch.getScore(DOTP);

                        // add the score to the running total for both spec
                        scores[i] += dotProduct;
                        scores[j] += dotProduct;
                    }
                } // next spectrum

                // find the best score and keep the spectrum associated with it
                bestIndex = getMaxElementIndex(scores);
                double bestScore = scores[bestIndex];
                double bestAverageScore = bestScore / (double)oneIon.size() ;

                // If best average score is too low, don't include it 
                if ( bestAverageScore >= minAverageScore_ ){
                    specID = transferSpectrum(redundantDbName_, 
                                              oneIon.at(bestIndex)->getLibSpecID(), 
                                              oneIon.size(),
                                              tableVersion_);
                } else {
                    Verbosity::warn("Best score is %f for %s, charge %d after "
                                    "comparing %i spectra.  This sequence will not be "
                                    "included in the filtered library.", 
                                    bestAverageScore, (oneIon.at(0)->getSeq()).c_str(),
                                    oneIon.at(0)->getCharge(), oneIon.size());
                    return;
                }
        }
    } else {
        if (num_spec == 1) {
            bestSpectraIdAndCount.push_back(make_pair(oneIon.at(0)->getLibSpecID(), 1));
        } else {
            map<RefSpectrum*, int> indices;
            map< int, vector<RefSpectrum*> > groups = groupByScoreType(oneIon, &indices);
            RefSpectrum* winner = NULL;

            vector<RefSpectrum*> possibleWinners;
            for (map< int, vector<RefSpectrum*> >::const_iterator i = groups.begin(); i != groups.end(); ++i) {
                map<int, bool>::const_iterator directionLookup = higherIsBetter_.find(i->first);
                if (directionLookup == higherIsBetter_.end()) {
                    Verbosity::error("Don't know if higher or lower is better for score type %d", i->first);
                }
                vector<RefSpectrum*> bestScores = getBestScores(i->second, directionLookup->second);
                possibleWinners.insert(possibleWinners.end(), bestScores.begin(), bestScores.end());
            }

            if (possibleWinners.size() == 1) {
                winner = possibleWinners.front();
            }
            else {
                // find highest TIC to determine final winner
                double winningValue = -1.0;
                for (vector<RefSpectrum*>::iterator i = possibleWinners.begin(); i != possibleWinners.end(); ++i) {
                    double specValue = (*i)->getTotalIonCurrentRaw();
                    if (specValue > winningValue) {
                        winner = *i;
                        winningValue = specValue;
                    }
                }

                /* cross-cross score among possible winners to determine final winner
                PeakProcessor proc;
                proc.setClearPrecursor(true);
                proc.setNumTopPeaksToUse(100);
                for (vector<RefSpectrum*>::iterator i = oneIon.begin(); i != oneIon.end(); ++i)
                    proc.processPeaks(*i);

                double winningScore = -1.0;
                for (vector<RefSpectrum*>::iterator i = possibleWinners.begin(); i != possibleWinners.end(); ++i) {
                    double crossScore = 0.0;
                    for (vector<RefSpectrum*>::iterator j = possibleWinners.begin(); j != possibleWinners.end(); ++j) {
                        if (*i == *j)
                            continue;
                        Match thisMatch(*i, *j);
                        DotProduct::compare(thisMatch);
                        crossScore += thisMatch.getScore(DOTP);
                    }
                    if (crossScore > winningScore) {
                        winner = *i;
                        winningScore = crossScore;
                    }
                }*/
            }

            bestSpectraIdAndCount.push_back(make_pair(winner->getLibSpecID(), oneIon.size()));

            bestIndex = indices[winner];
        }

        //specID = transferSpectrum(redundantDbName_, winner->getLibSpecID(), oneIon.size(), tableVersion_);
        specID = bestSpectraIdAndCount.back().first;
    }

    // add rt, RefSpectraId for all refspec
    for (int i = 0; i < num_spec; i++) {
        const RefSpectrum* spectrum = oneIon[i];
        int specIdRedundant = spectrum->getLibSpecID();
        double startTime = spectrum->getStartTime();
        double endTime = spectrum->getEndTime();
        sqlite3_bind_int(insertStmt_, 1, specID);
        sqlite3_bind_int(insertStmt_, 2, specIdRedundant);
        sqlite3_bind_int(insertStmt_, 3, getNewFileId(redundantDbName_, specIdRedundant));
        sqlite3_bind_double(insertStmt_, 4, spectrum->getIonMobility());
        sqlite3_bind_double(insertStmt_, 5, spectrum->getCollisionalCrossSection());
        sqlite3_bind_double(insertStmt_, 6, spectrum->getIonMobilityHighEnergyOffset());
        sqlite3_bind_int(insertStmt_, 7, (int)spectrum->getIonMobilityType());
        sqlite3_bind_double(insertStmt_, 8, spectrum->getRetentionTime());
        if (startTime != 0 && endTime != 0) {
            sqlite3_bind_double(insertStmt_, 9, startTime);
            sqlite3_bind_double(insertStmt_, 10, endTime);
        } else {
            sqlite3_bind_null(insertStmt_, 9);
            sqlite3_bind_null(insertStmt_, 10);
        }
        sqlite3_bind_double(insertStmt_, 11, spectrum->getScore());
        sqlite3_bind_int(insertStmt_, 12, i == bestIndex ? 1 : 0);
        if (sqlite3_step(insertStmt_) != SQLITE_DONE) {
            throw BlibException(false, "Error inserting row into RetentionTimes: %s", sqlite3_errmsg(getDb()));
        } else if (sqlite3_reset(insertStmt_) != SQLITE_OK) {
            throw BlibException(false, "Error resetting insert statement: %s", sqlite3_errmsg(getDb()));
        }
    }
}

map< int, vector<RefSpectrum*> > BlibFilter::groupByScoreType(const vector<RefSpectrum*>& oneIon, map<RefSpectrum*, int>* outIndices) {
    map< int, vector<RefSpectrum*> > groups;
    int index = -1;
    for (vector<RefSpectrum*>::const_iterator i = oneIon.begin(); i != oneIon.end(); ++i) {
        int scoreType = (*i)->getScoreType();
        map< int, vector<RefSpectrum*> >::iterator lookup = groups.find(scoreType);
        if (lookup == groups.end())
            groups[scoreType] = vector<RefSpectrum*>(1, *i);
        else
            groups[scoreType].push_back(*i);
        if (outIndices)
            (*outIndices)[*i] = ++index;
    }
    return groups;
}

vector<RefSpectrum*> BlibFilter::getBestScores(const vector<RefSpectrum*>& group, bool higherIsBetter) {
    vector<RefSpectrum*> best;
    if (group.empty())
        return best;

    best.push_back(group.front());
    if (group.size() == 1)
        return best;

    double bestScore = best.front()->getScore();
    
    for (vector<RefSpectrum*>::const_iterator i = group.begin() + 1; i != group.end(); ++i) {
        double score = (*i)->getScore();
        if ((!higherIsBetter && score < bestScore) ||
            (higherIsBetter && score > bestScore)) {
            best.clear();
            best.push_back(*i);
            bestScore = score;
        } else if (score == bestScore) {
            best.push_back(*i);
        }
    }
    return best;
}


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
