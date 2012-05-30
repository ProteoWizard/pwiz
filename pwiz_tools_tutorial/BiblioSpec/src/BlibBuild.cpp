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
 * The main function for BlibBuild.  Uses a BlibBuilder and the given
 * input files (any mix of .sqt, .pep.xml, .idpXML, .blib) and creates
 * a new library of given name.  
 *
 * $ BlibBuild,v 1.0 2009/01/07 15:53:52 Ning Zhang Exp $
 */
#ifdef _WIN23
#define _MATRIX_USE_STATIC_LIB
#endif

#include "BlibBuilder.h"
#include "AllBuildParsers.h"

using namespace std;
using namespace BiblioSpec;

int main(int argc, char* argv[])
{
#ifdef _MSC_VER
#ifdef _DEBUG
    // Add memory dump on exit
    _CrtSetDbgFlag(_CrtSetDbgFlag(_CRTDBG_REPORT_FLAG) | _CRTDBG_LEAK_CHECK_DF);
    //    _crtBreakAlloc = 624219;
#endif
#endif

    BlibBuilder builder;
    builder.parseCommandArgs(argc, argv);

    builder.init();
  
    vector<char*> inFiles = builder.getInputFiles();
 
    // for progress of score files 
    ProgressIndicator progress(inFiles.size());
    const ProgressIndicator* progress_cptr = &progress; // read-only pointer

    bool success = true;

    // process each .sqt, .pepxml, .idpXML, .xtan.xml, .dat, .blib file
    for(int i=0; i<(int)inFiles.size(); i++) {
        try{
            
            char* result_file = inFiles.at(i);
            
            Verbosity::comment(V_STATUS, "Reading results from %s.", 
                               result_file);
            progress.increment();
            
            if(has_extension(result_file, ".pep.xml") || 
               has_extension(result_file, ".pep.XML") ||
               has_extension(result_file, ".pepXML")) {
                PepXMLreader tmpXMLreader(builder, 
                                          result_file,
                                          progress_cptr);
                success = tmpXMLreader.parseFile();
            } else if(has_extension(result_file, ".sqt")) {
                
                SQTreader tmpSQTreader(builder, result_file, progress_cptr);
                success = tmpSQTreader.parseFile();
                
            } else if(has_extension(result_file, ".perc.xml")) {
                PercolatorXmlReader PercolatorXmlReader(builder, result_file, 
                                                        progress_cptr);
                success = PercolatorXmlReader.parseFile();
            } else if (has_extension(result_file, ".blib")) {
                builder.transferLibrary(i, progress_cptr);
            } else if (has_extension(result_file, ".idpXML")) {
                
                IdpXMLreader tmpXMLreader(builder, result_file, progress_cptr);
                success = tmpXMLreader.parseFile();
                
            } else if (has_extension(result_file, ".dat")) {
                MascotResultsReader tmpMascotReader(builder, result_file, 
                                                    progress_cptr);
                success = tmpMascotReader.parseFile();
                
            } else if (has_extension(result_file, ".ssl")) {
                SslReader tmpSslReader(builder, result_file, progress_cptr);
                success = tmpSslReader.parseFile(); 
                
            } else if (has_extension(result_file, ".xtan.xml")) {
                TandemNativeParser tandemReader(builder, result_file, 
                                                progress_cptr);
                success = tandemReader.parseFile();
                
            } else if (has_extension(result_file, ".group.xml")) {
                ProteinPilotReader pilotReader(builder, result_file, 
                                               progress_cptr);
                success = pilotReader.parseFile();
                
            } else if (has_extension(result_file, ".mzid")) {
                MzIdentMLReader mzidReader(builder, result_file, progress_cptr);
                success = mzidReader.parseFile();
 
            } else if (has_extension(result_file, "final_fragment.csv")) {
                WatersMseReader mseReader(builder, result_file, progress_cptr);
                success = mseReader.parseFile();
 
             } else { 
                // shouldn't get to here b/c cmd line parsing checks, but...
                Verbosity::error("Unknown input file type '%s'.", result_file);
            }      
            
            if( !success ){ // in the unlikely event a reader returns false instead of throwing an error
                string errorMsg = "Failed to parse ";
                errorMsg += result_file;
                throw errorMsg;
            }
        } catch(BlibException& e){
            cerr << "ERROR: " << e.what() << endl;
            if( ! e.hasFilename() ){
                cerr << "ERROR: reading file " << inFiles.at(i) << endl;
            }
            success = false;
        } catch(std::exception& e){
            cerr << "ERROR: " << e.what() 
                 << " in file '" << inFiles.at(i) << "'." << endl;
            success = false;
        } catch(string s){ // in case a throwParseError is not caught
            cerr << "ERROR: " << s << endl;
            success = false;
        } catch(...){
            cerr << "ERROR: reading file '" << inFiles.at(i) << "'" << endl;
            success = false;
        }
    }
    
    if( ! success ){
        // try saving the library
        builder.undoActiveTransaction();
    }

    // check that library contains spectra
    if( builder.is_empty() ){
        builder.abort_current_library();
        Verbosity::error("No spectra were found for the new library.");
    }

    builder.commit();
    
    Verbosity::close_logfile();
    return !success;
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
