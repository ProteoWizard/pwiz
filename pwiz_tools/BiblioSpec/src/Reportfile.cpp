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

//class definition for LogFile

#include "Reportfile.h"
#include <time.h>

using namespace std;

namespace BiblioSpec {

/**
 * Create a Reportfile object with no associated file.  Creater can
 * later call open() to associate with a named file.
 */
Reportfile::Reportfile(const ops::variables_map& options_table)
: topMatches_(options_table["report-matches"].as<int>())
{
    // extract values for header
    optionsString_ = optionsHeaderString(options_table); 

    // translate topMatches==-1 to print all
    if( topMatches_ == -1 ){
        topMatches_ = numeric_limits<int>::max();
    }
}

/**
 * Open a file for writing and write header.
 */
void Reportfile::open(const char* filename) {
    
    file_.open(filename);
    if( ! file_.is_open() ) {
        Verbosity::error("Could not open report file %s.", filename);
    }
    writeHeader();
}

Reportfile::~Reportfile()
{
    if( file_.is_open() )
        file_.close();
}

/**
 * Write the options string and the column header to the open file.
 */
void Reportfile::writeHeader()
{

    file_ << optionsString_ << endl;

    // write column titles for results
    const char* header = "Query\t"
    "LibId\t"
    "LibSpec\t"
    "rank\t"
    "dotp\t"
    "query-mz\t"
    "query-z\t"
    "lib-mz\t"
    "lib-z\t"
    "copies\t"
    "candidates\t"
    "sequence\t"
    "TIC-raw\t"
    "bp-mz-raw\t"
    "bp-raw\t"
    "lbp-mz-raw\t"
    "num-peaks\t" 
    "matched-ions";
  /*
  //"TIC-proc\t"
  //"bp-proc\t"
    "bp-mz-proc\t"
    "lTIC-raw\t"
    "lTIC-proc\t"
    "lbp-mz-proc\t"
    "lbp-raw\t"
    "lbp-proc\t"
    "lnum-peaks" ;
    */
    
    file_ << header << endl;
}

/**
 * Convert the options values to a string that can be printed to the
 * header file.
 */
string Reportfile::optionsHeaderString(const ops::variables_map& options_table){
    ostringstream strBuilder; // write to here

    time_t t=time(NULL);
    char* date=ctime(&t);
    string queryFileName = options_table["spectrum-file"].as<string>();    
    const vector<string>& libfiles = 
        options_table["library"].as< vector<string> >();    

    // Start with date, filenames 
    strBuilder << "# Search results from BilbSearch" << endl //version?
         << "# Date: " << date << endl
         << "# query file: " << queryFileName << endl;
    
    strBuilder << "# Library file list:"<<endl;
    for(size_t i = 0; i < libfiles.size(); i++) {
        strBuilder << "# libID" << i+1 << "\t" << libfiles.at(i) << endl;
    }

    // Next, all options values relevant to the search
    strBuilder << "# Options:" << endl;
    strBuilder << "# clear-precursor = " 
               << (options_table.count("clear-precursor") ? "true" : "false")
               << endl;
    strBuilder << "# topPeaksForSearch = " 
               << options_table["topPeaksForSearch"].as<int>() << endl;
    strBuilder << "# mz-window = " << options_table["mz-window"].as<double>()
               << endl;
    strBuilder << "# low-charge = " << options_table["low-charge"].as<int>()
               << endl;
    strBuilder << "# high-charge = " << options_table["high-charge"].as<int>() 
               << endl;
    strBuilder << "# report-matches = " ;
    if( options_table["report-matches"].as<int>() == -1 ){
        strBuilder << "all";
    } else {
        strBuilder << options_table["report-matches"].as<int>();
    }
    strBuilder << endl;

    return strBuilder.str();
}

/**
 * Write to file all matches whose rank is no greater than
 * topMatches_. If two matches have the same score, they will also
 * have the same rank.  Check rank of each match instead of printing
 * the first n.  Uses the size of the results vector as the number of
 * candidates for this spectrum.
 */
void Reportfile::writeMatches(const vector<Match>& results)
{
#ifdef _MSC_VER
#ifdef _TWO_DIGIT_EXPONENT // VS2015 got rid of this function
        _set_output_format(_TWO_DIGIT_EXPONENT);
#endif        
#endif

    if(! file_.is_open()){
        return;
    }

    file_.precision(6);
    int numCandidates = results.size();

    vector<Match>::const_iterator it;
    for(it = results.begin(); it != results.end(); it++) {
        if( (*it).getRank() > topMatches_ ) {
            break;
        }
        const Match& curMatch = *it;
        const Spectrum* querySpec = curMatch.getExpSpec();
        const RefSpectrum* refSpec = curMatch.getRefSpec();

        file_ << querySpec->getScanNumber() << "\t"
             << refSpec->getLibID() << "\t"
             << refSpec->getLibSpecID() << "\t"
             << curMatch.getRank() << "\t"
             << curMatch.getScore(DOTP) << "\t"
             << querySpec->getMz() << "\t";
        const vector<int>& charges = querySpec->getPossibleCharges();
        // print first charge state with no comma, in case only one
        if( charges.empty() ){
            file_ << "0";
        } else {
            file_ << charges.front() ;
        }
        for(size_t i=1; i< charges.size(); i++){
            file_ << "," << charges.at(i) ;
        }
        file_ << "\t";
        file_ << refSpec->getMz() << "\t"
             << refSpec->getCharge() << "\t"
             << refSpec->getCopies() << "\t"
             << numCandidates << "\t"
             << refSpec->getMods();

        // tic, base peak, num peaks
        file_ << "\t" << querySpec->getTotalIonCurrentRaw();
        file_ << "\t" << querySpec->getBasePeakMzRaw();
        file_ << "\t" << querySpec->getBasePeakIntensityRaw();
        file_ << "\t" << refSpec->getBasePeakMzRaw();
        file_ << "\t" << querySpec->getNumRawPeaks();
        /*
        file_ << "\t" << querySpec->getTotalIonCurrentProcessed();
        file_ << "\t" << querySpec->getBasePeakIntensityProcessed();
        file_ << "\t" << querySpec->getBasePeakMzProcessed();

        file_ << "\t" << refSpec->getTotalIonCurrentRaw();
        file_ << "\t" << refSpec->getTotalIonCurrentProcessed();
        file_ << "\t" << refSpec->getBasePeakIntensityRaw();
        file_ << "\t" << refSpec->getBasePeakMzProcessed();
        file_ << "\t" << refSpec->getBasePeakIntensityProcessed();
        file_ << "\t" << refSpec->getNumRawPeaks();
        */
        file_ << "\t" << curMatch.getScore(MATCHED_IONS);
        file_ << endl;
        
    }// next match
}

} // namespace
/*

    
        char lineBuf[1024];
        sprintf(lineBuf,
                "%d\t%d\t%d\t%d\t"//scan, lib, libid, rank
                "%.4f\t%.f\t%.2f\tcharge\t"// dotp, pval, qmz
                "%.2f\t%d\t%d\t%d\t%s\t%.f\t"//lmz, lz, copies, cand, seq, pval
                "%.4g\t%.4g\t%.4g\t%.4g\t%d",//tic, tic, base, base, num peaks
                querySpec->getScanNumber(),
                refSpec->getLibID(),
                refSpec->getLibSpecID(), 
                curMatch.getRank() ,
                curMatch.getScore(DOTP), 
                curMatch.getScore(BONF_PVAL), 
                querySpec->getMz(),
                refSpec->getMz(),
                refSpec->getCharge(), 
                refSpec->getCopies(),
                numCandidates ,
                refSpec->getMods().c_str(),
                curMatch.getScore(RAW_PVAL),
                querySpec->getTotalIonCurrentRaw(),
                querySpec->getTotalIonCurrentProcessed(),
                querySpec->getBasePeakIntensityRaw(),
                querySpec->getBasePeakIntensityProcessed(),
                querySpec->getNumRawPeaks()
                );

        file_ << lineBuf << endl;

 */

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
