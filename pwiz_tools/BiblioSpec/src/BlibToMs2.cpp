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
/*
 * Main for BiblioSpec utility BlibToMs2 for converting sqlite format
 * libraries to modified .ms2 format.
 */


#include "Verbosity.h"
#include "boost/program_options.hpp"
#include "CommandLine.h"
#include "LibReader.h"
#include "BlibUtils.h"
#include "Ms2Writer.h"

using namespace std;
namespace ops = boost::program_options;
using namespace BiblioSpec;

// Private functions
void ParseCommandline(const int argc,
                      char** const argv,
                      ops::variables_map& options_table);

int main(int argc, char* argv[])
{

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
    LibReader library(libName.c_str());

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
