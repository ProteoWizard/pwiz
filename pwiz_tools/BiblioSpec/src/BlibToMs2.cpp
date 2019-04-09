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
 * Main for BiblioSpec utility BlibToMs2 for converting sqlite format
 * libraries to modified .ms2 format.
 */

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "Verbosity.h"
#include "boost/program_options.hpp"
#include "CommandLine.h"
#include "LibReader.h"
#include "BlibUtils.h"
#include "Ms2Writer.h"

namespace ops = boost::program_options;
using namespace BiblioSpec;

// Private functions
void ParseCommandline(const int argc,
                      char** const argv,
                      ops::variables_map& options_table);

int main(int argc, char* argv[])
{
    bnw::args utf8ArgWrapper(argc, argv); // modifies argv in-place with UTF-8 version on Windows
    pwiz::util::enable_utf8_path_operations();

    // declare storage for options values
    ops::variables_map options_table;
    
    ParseCommandline(argc, argv, options_table);
    
    // get file names
    string libName = options_table["library"].as<string>();
    string ms2Name = options_table["file-name"].as<string>();
    if( ms2Name.empty() ){
        ms2Name = libName;
        replaceExtension(ms2Name, "ms2");
    }
    
    // open library reader
    Verbosity::status("Opening library %s.", libName.c_str());
    LibReader library(libName.c_str(), options_table["mod-precision"].as<int>());

    // open a spectrum writer
    Verbosity::status("Writing spectra to %s.", ms2Name.c_str());
    Ms2Writer fileWriter(options_table);
    fileWriter.openFile(ms2Name.c_str());

    // write header
    string fullLibName = getAbsoluteFilePath(libName);
    fileWriter.writeLibName(fullLibName.c_str()); 

    // for each spectrum
    RefSpectrum curSpectrum;
    while( library.getNextSpectrum(curSpectrum) ){
        fileWriter.writeSpectrum(curSpectrum);        
        curSpectrum.clear();
    } // next spectrum

    // close files
}

void ParseCommandline(const int argc,
                      char** const argv,
                      ops::variables_map& options_table)
{
    // define the optional command line options
    ops::options_description optionsDescription("Options");
    
    try{
        optionsDescription.add_options()
            ("file-name,f",
             value<string>()->default_value(""),
             "Name the output ms2 file.  Default is <library name>.ms2."
             )

            ("mz-precision,m",
             value<int>()->default_value(2),
             "Precision for peak m/z printed to ms2.  Default 2."
             )

            ("intensity-precision,i",
             value<int>()->default_value(1),
             "Precision for peak intensities.  Default 1."
             )

            ("mod-precision,p",
             value<int>()->default_value(-1),
             "Precision for modification masses.  Default -1 (use value in PeptideModSeq column)."
             )
            ;

        // define the required command line args
        vector<const char*> argNames(1, "library");

        // create a CommandLine object to do the parsing
        CommandLine parser("BlibToMs2", optionsDescription, argNames,
                           false); // last arg cannot be repeated
        parser.parse(argc, argv, options_table);

    } catch(exception& e) {
        cerr << "ERROR: " << e.what() << "." << endl << endl;  
        exit(1);
    } catch(...) {
        cerr << "Encountered exception of unknown type while parsing command "
             << "line." << endl;
        exit(1);
    }
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
