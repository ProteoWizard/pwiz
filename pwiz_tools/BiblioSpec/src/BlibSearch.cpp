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
 *BlibSearch main program using sqlite3 format input and library files.
 *
 * Renamed from SearchMain.cpp
 * $Id$
 */


#include <cstdio>
#include <iostream>
#include <string>
#include <vector>
#include <sstream>
#include "RefSpectrum.h"
#include "Match.h"
#include "PeakProcess.h"
#include "Reportfile.h"
#include "LibReader.h"
#include "SearchLibrary.h"
#include "Verbosity.h"
#include "boost/program_options.hpp"
#include "BlibUtils.h"
#include "CommandLine.h"
#include "PsmFile.h"
#include "PwizReader.h"
#include "SpecFileReader.h"
#include "pwiz/utility/misc/Filesystem.hpp"

namespace ops = boost::program_options;
using namespace BiblioSpec;

// Private functions
void runSearch(BiblioSpec::Spectrum& s, 
               vector<string>& libfiles, 
               BiblioSpec::Reportfile& log, 
               BiblioSpec::PsmFile* psmFile,
               const ops::variables_map& options_table);

void checkFileExtensions(string specFileName, vector<string> libraryNames);

void ParseCommandline(const int argc, 
                           char** const argv,         
                           ops::variables_map& options_table);
string getTargetReportName(const string& specFileName, 
                           const ops::variables_map& options_table);

/**
 * The starting point for BlibSearch.
 *
 * Compares all spectra in the given file to spectra in the library
 * whose m/z is with in +/- mzWindow.  Stores results in an sqlite db
 * and prints to text file.
 */
int main(int argc, char* argv[])
{
    bnw::args utf8ArgWrapper(argc, argv); // modifies argv in-place with UTF-8 version on Windows
    pwiz::util::enable_utf8_path_operations();

    // declare storage for options values
    ops::variables_map options_table;

    ParseCommandline(argc, argv, options_table);

    // get input files
    string specFileName = options_table["spectrum-file"].as<string>();
    vector<string> libraryNames = options_table["library"].as< vector<string> >();
    checkFileExtensions(specFileName, libraryNames);

    // print status
    ostringstream stringBuilder;
    stringBuilder << libraryNames;
    string concatLibNames = stringBuilder.str();
    BiblioSpec::Verbosity::status("Using library(s) %s.", 
                                  concatLibNames.c_str());

    // open the report files
    BiblioSpec::Reportfile targetReport(options_table);
    BiblioSpec::Reportfile decoyReport(options_table);
    string reportFileName = getTargetReportName(specFileName, options_table);
    string finalReport = reportFileName;
    string tmpReport = reportFileName;
    tmpReport += ".tmp";
    reportFileName = tmpReport;
    targetReport.open(reportFileName.c_str());
    if( options_table["decoys-per-target"].as<int>() > 0 ){
        string decoyReportName = finalReport;
        BiblioSpec::replaceExtension(decoyReportName,"decoy.report");
        decoyReport.open(decoyReportName.c_str());
    }

    // Initialize a .psm file (sqlite db), if requested
    BiblioSpec::PsmFile* psmFile = NULL;
    if( options_table.count("psm-result-file") ){
        psmFile = 
            new BiblioSpec::PsmFile(options_table["psm-result-file"].as<string>().c_str(),
                        options_table );
    }

    // Initialize a searcher with libraries and options
    BiblioSpec::SearchLibrary searcher(libraryNames, options_table);

    // TODO replace with a SpecFileReader
    PwizReader* fileReader = new PwizReader();
    fileReader->setIdType(INDEX_ID); // for getNextSpectrum look up
    bool mzSort = (options_table.count("preserve-order") == 0);
    fileReader->openFile(specFileName.c_str(), mzSort);

    BiblioSpec::Verbosity::status("Searching spectra in '%s'.", 
                                  specFileName.c_str());

    // TODO include a progress indicator
    BiblioSpec::Spectrum curSpectrum;
    while( fileReader->getNextSpectrum(curSpectrum) ) {
        
        searcher.searchSpectrum(curSpectrum);

        const vector<BiblioSpec::Match>& targetMatches = searcher.getTargetMatches();
        const vector<BiblioSpec::Match>& decoyMatches = searcher.getDecoyMatches();
        
        if(targetMatches.size() == 0){
            curSpectrum.clear();
            continue;
        }

        // write to the .report file
        targetReport.writeMatches(targetMatches);
        decoyReport.writeMatches(decoyMatches);

        // write to the .psm file
        if(psmFile) {
            psmFile->insertMatches(targetMatches);
            psmFile->insertMatches(decoyMatches);
            // restore this eventually
            //psmFile->insertSpecData(curSpectrum, allMatches, searcher);
        }
        curSpectrum.clear();
    } // next spectrum

    if( psmFile )
        psmFile->commit();
    
    // todo close report file
    delete fileReader;
    delete psmFile;
    
    rename(tmpReport.c_str(), finalReport.c_str());
    
    return 0;

}// end main


/**
 * Return the correct name of the report file for the target matches.
 */
string getTargetReportName(const string& specFileName, 
                           const ops::variables_map& options_table){
    string reportFileName = specFileName;
    if( options_table.count("report-file") ){
        reportFileName = options_table["report-file"].as<string>();
    } else {
        BiblioSpec::replaceExtension(reportFileName, "report");
    }
    return reportFileName;
}




using namespace ops;

/**
 * Read the given command line and store option and argument values in
 * the given variables_map.  Also reads parameter file with command
 * line values overriding file values. 
 *
 * Exits on error if unexpected option found, if option is missing its
 * argument, if required argument is missing.
 */
void ParseCommandline(const int argc, 
                      char** const argv,         
                      ops::variables_map& options_table){

    // define the optional command line args 
    options_description optionsDescription("Options");
    // define some hidden options for developers only
    options_description devOptionsDescription("Developer Options");

    try{

        optionsDescription.add_options()
            ("clear-precursor,c",
             value<bool>()->default_value(true),
             "Remove the peaks in a X m/z window around the precursor from the query and library spectrum.")

            // change name
            ("topPeaksForSearch",
             value<int>()->default_value(100),
             "Use ARG of the highest intensity peaks.")

            ("mz-window,w",
             value<double>()->default_value(3),
             "Compare query to library spectra with precursor m/z +/- ARG.")

            ("min-peaks,n",
             value<int>()->default_value(20),
             "Search only spectra with charge no less than ARG.")

            ("low-charge,L",
             value<int>()->default_value(1),
             "Search only spectra with charge no less than ARG.")

            ("high-charge,H",
             value<int>()->default_value(5),
             "Search only spectra with charge no higher than ARG.")

            ("report-matches,m",
             value<int>()->default_value(5),
             "Return ARG of the best matches for each query.  Use -1 to report all.")
            
            ("psm-result-file",
             value<string>(),
             "Return results in a .psm file named ARG.")

            ("report-file,R",
             value<string>(),
             "Return results in report file named ARG.  Default is <spectrum file name>.report.")

            ("preserve-order",
             "Search spectra in the order they appear in the file.  Default to search as sorted by precursor m/z."
             )

            /*
            ("",
             value<>(),
             "")
            */
            ;
        // create a separate set of private options not printed to usage
        devOptionsDescription.add_options()
            ("compute-p-values,P",
             value<bool>()->default_value(false),
             "Compute q-values for matches based on the Weibull distribution.  Default TRUE.")

            ("fraction-to-fit,f",
             value<double>()->default_value(0.5),
             "Fraction of scores to use in Weibull parameter estimation.")

            ("weibull-param-file,W",
             value<string>(),
             "Return estimated Weibull params for each spectrum in a file named ARG.")

            ("correlation-tolerance,t",
             value<double>()->default_value(0.11),
             "The amount by which the correlation can decrease from the max before halting the Weibull shift parameter estimation.")

            ("print-all-params,a",
             value<bool>()->default_value(false),
             "Print to stdout estimated parameters at each shift value.")
            ("decoys-per-target",
             value<int>()->default_value(0),
             "Search ARG randomized library spectra for each real library spectrum. Default 0.")

            ("circ-shift",
             value<double>()->default_value(3),
             "Generate randomized spectra by adding ARG m/z to each peak.  Default 3.")

            ("bin-size",
             value<double>()->default_value(1.0),
             "Width of peak bins used in pre-processing.  Default 1.0.")

            ("bin-offset",
             value<double>()->default_value(0.0),
             "Value of the left (low) edge of the smallest peak m/z bin. Default 0.")

            ("remove-noise-first",
             value<bool>()->default_value(true),
             "Process spectrum peaks by first removing noise, then normalizing intensity. False reverses the order. Default true.")

            ("shift-raw-spectrum",
             value<bool>()->default_value(true),
             "When generating randomized spectra, shift the m/z before processing peaks.  Default true.")

            ("min-weibull-scores",
             value<int>()->default_value(500),
             "The minimum number of scores required to estimate weibull parameters.  Default 500.")
            ;

        // define the required command line args
        vector<const char*> argNames;
        argNames.push_back("spectrum-file");
        argNames.push_back("library");

        // create a CommandLine object to do the dirty work
        BiblioSpec::CommandLine parser("BlibSearch", optionsDescription, 
                           argNames, true);// last arg can have multiple values
        parser.addHiddenOptions(devOptionsDescription);

        // read command line and param file (if given)
        parser.parse(argc, argv, options_table);

    } catch(std::exception& e) {
        cerr << "ERROR: " << e.what() << "." << endl << endl;  
        exit(1);
    } catch(...) {
        BiblioSpec::Verbosity::error("Encountered exception of unknown type while "
                         "parsing command line.");
    }


}


/**
 * Confirm that the spectrum filename ends in a legitimate extension
 * and that all of the library names end in .blib.  Die on error.
 */
void  checkFileExtensions(string specFileName, vector<string> libraryNames){

    // check spec file
    // eventually allow more file types
    if( !BiblioSpec::hasExtension(specFileName, ".ms2") &&
        !BiblioSpec::hasExtension(specFileName, ".cms2") &&
        !BiblioSpec::hasExtension(specFileName, ".bms2") &&
        !BiblioSpec::hasExtension(specFileName, ".mzML") &&
        !BiblioSpec::hasExtension(specFileName, ".mzXML") &&
        !BiblioSpec::hasExtension(specFileName, ".MGF") &&
        !BiblioSpec::hasExtension(specFileName, ".wiff")
        ) {
        BiblioSpec::Verbosity::error("Spectrum file '%s' must be of type .ms2,"
                         " .cms2, .bms2, .mzML, .mzXML, .MGF, or .wiff.",
                         specFileName.c_str());
    }

    // check libraries
    for(size_t i = 0; i < libraryNames.size(); i++) {
        string libraryName = libraryNames.at(i);
        if( !BiblioSpec::hasExtension(libraryName, ".blib")){
            BiblioSpec::Verbosity::error("Library '%s' must be of type .blib.",
                             libraryName.c_str());
        }
    }
}


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
